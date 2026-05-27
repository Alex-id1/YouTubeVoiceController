using YouTubeVoiceController.DirectMLPredictor;

namespace YouTubeVoiceController{
    /// <summary>
    /// Dispatches recognized voice commands. Current strategy is keyboard-shortcut only:
    /// find the browser window with YouTube => activate it => send the shortcut.
    /// YOLO-based clicking (for like/dislike/settings) is intentionally not wired in here yet
    /// </summary>
    sealed class CommandDispatcher : IAsyncDisposable{
        private readonly InferenceScheduler _scheduler;
        private readonly IUserNotifier _notifier;

        public CommandDispatcher(InferenceScheduler scheduler, IUserNotifier notifier){
            _scheduler = scheduler;
            _notifier  = notifier;
        }

        // --- Video-frame focus cache (for volume commands) ---------------------------
        // Volume keys (ArrowUp/Down) only work when the YouTube <video> element has DOM focus.
        // To force focus we click the player. To avoid running YOLO on every "volume up" we
        // cache the focus point per video URL - the like button moves with the video frame,
        // so as long as the URL is the same the cached point stays valid
        private string? _focusCacheUrl;
        private Point? _focusCachePoint;

        /// <summary>
        /// Called by VoiceListener (or by the debug text input) when a command arrives
        /// </summary>
        public async Task DispatchAsync(string voiceCommand, string? searchQuery = null){
            string cmd = voiceCommand.Trim().ToLowerInvariant();

            // --- Open / focus YouTube ---
            if (YouTubeButtonMap.IsOpenCommand(cmd)) {
                await EnsureYouTubeFocusedAsync();
                return;
            }

            // --- Search command (always valid regardless of page state) ---
            if(YouTubeButtonMap.IsSearchCommand(cmd)) {
                if (!string.IsNullOrWhiteSpace(searchQuery))
                    await ExecuteSearchAsync(searchQuery);
                return;
            }

            // --- Cancel commands are page-agnostic (handled by VoiceListener in search mode,
            //ignored here - but must not be blocked by the SearchResults page guard) ---
            if(YouTubeButtonMap.CancelCommands.Contains(cmd)) return;

            // --- Read page state before executing anything ---
            var page = ReadCurrentPageState();

            // --- Ordinal command (play first/second/...) ---
            if(YouTubeButtonMap.IsOrdinalCommand(cmd)) {
                await ExecuteOrdinalAsync(cmd, page);
                return;
            }

            // --- YOLO visual-click commands (like / dislike / close-ad) ---
            if(YouTubeButtonMap.IsYoloCommand(cmd)){
                // Resolve the YOLO class name ("like", "dislike", "close-ad")
                string? yoloClass = YouTubeButtonMap.Resolve(cmd);
                if (yoloClass == null){
                    _notifier.Notify($"⚠ No YOLO class mapping for '{voiceCommand}'", NotifyLevel.Warn);
                    return;
                }

                if (!await EnsureYouTubeFocusedAsync()) return;
                await Task.Delay(250); // let the browser repaint after focus switch

                await YoloClickExecutor.ExecuteAsync(cmd, yoloClass, _scheduler, _notifier);
                return;
            }

            // --- Keyboard-shortcut commands (play/pause/fullscreen/etc) ---
            var shortcut = YouTubeShortcuts.Resolve(cmd);
            if (shortcut == null) {
                _notifier.Notify($"⚠ Command '{voiceCommand}' has no shortcut yet", NotifyLevel.Warn);
                AppLogger.Debug($"No shortcut for '{voiceCommand}'");
                return;
            }

            // Playback/interaction commands only make sense on a video page.
            // Global commands (back/previous) bypass this check - they work on any page
            if (page.Type == YouTubePageType.SearchResults &&
                !YouTubeButtonMap.IsGlobalCommand(cmd)) {
                _notifier.Notify("⚠ No video is open - open a video first",
                    NotifyLevel.Warn, speechText: "no video is open");
                return;
            }

            await ExecuteShortcutAsync(cmd, shortcut.Value);
        }

        /// <summary>Reads the current YouTube page state from the browser address bar</summary>
        private static YouTubePageState ReadCurrentPageState(){
            string? url = BrowserUrlReader.ReadYouTubeUrl();
            var state = YouTubePageState.FromUrl(url);
            AppLogger.Info($"PageState: {state.Type}  query=\"{state.SearchQuery ?? ""}\"  url={url ?? "(null)"}");
            return state;
        }

        private async Task ExecuteShortcutAsync(string cmd, KeyCombo combo){
            if (!await EnsureYouTubeFocusedAsync()) return;

            // The OS needs time to: (1) finish the foreground switch, (2) let the
            // browser repaint, and (3) deliver any pending input to the DOM.
            // 250 ms is empirically reliable across Chrome/Edge/Firefox
            await Task.Delay(250);

            var fg = BrowserWindowFinder.GetForegroundWindow();
            AppLogger.Debug($"Dispatching {combo} for '{cmd}'. Foreground HWND=0x{fg:X}");

            // Volume keys (ArrowUp/Down) need the YouTube <video> element to have DOM focus -
            // otherwise the browser scrolls the page. The reliable way to give it focus is
            // a real mouse click inside the video frame. We use YOLO to find the like button
            // and compute the click point ~10% screen-height above it (always inside video).
            // The point is cached per URL so repeated volume commands don't re-run YOLO
            if (IsVolumeCommand(cmd)){
                await DispatchVolumeAsync(combo);
            } else {
                KeyboardSimulator.PressKey(combo);
            }

            string speech = YouTubeSpeechResponses.Get(cmd) ?? cmd;
            _notifier.Notify($"✔ {cmd} => {combo}", speechText: speech);
            AppLogger.Info($"Sent {combo} for '{cmd}'");
        }

        /// <summary>
        /// Ensures a YouTube tab is in the foreground:
        /// 1)A browser window whose title contains "YouTube" => YouTube is the active tab, activate it.
        /// 2)No such window => scan every browser window via UI Automation for a background YouTube tab; if found, select that tab and bring its window forward.
        /// 3)No YouTube tab anywhere => open youtube.com (new tab), wait 1.5s, retry once
        /// </summary>
        private async Task<bool> EnsureYouTubeFocusedAsync(){
            // Fast path: YouTube is the active tab somewhere (its title is in a window caption)
            var hWnd = BrowserWindowFinder.FindYouTubeWindow();
            if (hWnd != null) {
                BrowserWindowFinder.Activate(hWnd.Value);
                return true;
            }

            // YouTube may be a background tab - search each browser window's tab strip via UIA
            foreach (var browser in BrowserWindowFinder.FindAllBrowserWindows()){
                if (BrowserTabSwitcher.TrySelectYouTubeTab(browser)){
                    BrowserWindowFinder.Activate(browser);
                    await Task.Delay(150); // let the tab switch settle before the shortcut
                    return true;
                }
            }

            // No YouTube tab anywhere -open one
            _notifier.Notify("⏳ Opening YouTube...");
            AppLogger.Info("EnsureYouTubeFocused: no YouTube tab found, opening one");
            OpenYouTube();

            await Task.Delay(1500);

            hWnd = BrowserWindowFinder.FindYouTubeWindow();
            if (hWnd != null) {
                BrowserWindowFinder.Activate(hWnd.Value);
                return true;
            }

            _notifier.Notify("⚠ YouTube did not open in time - repeat the command once loaded",
                NotifyLevel.Warn, speechText: "YouTube is opening, please repeat");
            AppLogger.Warning("EnsureYouTubeFocused: timed out");
            return false;
        }

        /// <summary>
        /// Focus the YouTube video (without toggling play/pause) then send the volume keys.
        /// Uses ClickToFocus (mousedown => drag => mouseup) so the browser sees a drag, not a
        /// click - the video element gets DOM focus from mousedown, but YouTube's play/pause
        /// click-handler doesn't fire because mouseup happens at a different position.
        /// </summary>
        private async Task DispatchVolumeAsync(KeyCombo combo){
            var focus = await GetVideoFocusPointAsync();
            if (focus == null){
                AppLogger.Warning("DispatchVolume: no focus point - falling back to bare keys");
                KeyboardSimulator.PressKey(combo);
                KeyboardSimulator.PressKey(combo);
                return;
            }

            // Save cursor so it doesn't appear to leap onto the player
            var savedCursor = MouseHelper.GetCursorPosition();

            ClickSimulator.ClickToFocus(focus.Value);
            await Task.Delay(80); // let DOM focus settle before sending keys

            KeyboardSimulator.PressKey(combo); // 5%
            KeyboardSimulator.PressKey(combo); // 10%

            MouseHelper.SetCursorPosition(savedCursor);
        }

        /// <summary>
        /// Returns a screen-space point that lies inside the YouTube video frame.
        /// Cached per URL - first call runs YOLO; subsequent volume commands on the same
        /// video reuse the cached point. Cache invalidates when the URL changes
        /// <para>
        /// Strategy:
        ///   1) Cache hit (same URL) => return cached point.
        ///   2) YOLO locate "like" => success => cache and return.
        ///   3) Fallback: YOLO locate "dislike" (same row, same height) => cache and return.
        ///   4) Both failed => return null and DO NOT cache (next call retries YOLO)
        /// </para>
        /// </summary>
        private async Task<System.Drawing.Point?> GetVideoFocusPointAsync(){
            string? url = BrowserUrlReader.ReadYouTubeUrl();

            if (_focusCachePoint.HasValue && _focusCacheUrl == url){
                AppLogger.Debug($"VideoFocus: cache hit for url={url}  point={_focusCachePoint.Value}");
                return _focusCachePoint;
            }

            // ~10% of screen height above like/dislike lands safely inside the video frame
            int verticalOffset = (int)(Screen.PrimaryScreen!.Bounds.Height * 0.10);

            // Try "like" first - most reliable detection in our dataset
            var anchor = await YoloClickExecutor.LocateAsync("like", _scheduler);
            string anchorName = "like";

            // Fallback: "dislike" sits in the same row at the same height (e.g. when the
            // mouse cursor was occluding the like icon during the screenshot)
            if (anchor == null){
                AppLogger.Info("VideoFocus: 'like' not found, trying 'dislike' fallback");
                anchor = await YoloClickExecutor.LocateAsync("dislike", _scheduler);
                anchorName = "dislike";
            }

            if (anchor == null){
                // Both failed - return null without caching so the next volume command retries
                AppLogger.Warning("VideoFocus: neither like nor dislike detected - skipping cache");
                return null;
            }

            var point = new System.Drawing.Point(anchor.Value.X, anchor.Value.Y - verticalOffset);
            _focusCacheUrl   = url;
            _focusCachePoint = point;
            AppLogger.Info($"VideoFocus: cached via '{anchorName}'  url={url}  point={point}  (anchor at y={anchor.Value.Y})");
            return point;
        }

        private static readonly HashSet<string> _volumeCommands = new(StringComparer.OrdinalIgnoreCase){
                "louder", "quieter", "quiet",
                "volume up", "volume down", "softer", "lower",
            };

        private static bool IsVolumeCommand(string cmd) => _volumeCommands.Contains(cmd);

        // --- Open YouTube ---

        public static void OpenYouTube(){
            AppLogger.Info("Opening YouTube in default browser");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://www.youtube.com") { UseShellExecute = true });
        }

        // --- Open video by ordinal ("first", "second", ...) ---

        private async Task ExecuteOrdinalAsync(string cmd, YouTubePageState page){
            int index = YouTubeButtonMap.OrdinalIndex(cmd);

            // Ordinal commands only make sense when the user is looking at search results
            if (page.Type != YouTubePageType.SearchResults){
                if (page.Type == YouTubePageType.Unknown){
                    // Couldn't read URL - browser may not expose it via UIA
                    // Log the problem so the user can report it
                    AppLogger.Warning("PageState: could not read browser URL via UIA - ordinal command blocked");
                    _notifier.Notify("⚠ Could not read browser URL - open a YouTube search-results page and try again",
                        NotifyLevel.Warn, speechText: "cannot read browser address bar");
                }else{
                    string notOnResults = page.Type == YouTubePageType.VideoPage
                        ? "say search first to get a list of videos"
                        : "open YouTube search results first";
                    _notifier.Notify("⚠ Open a search-results page first",
                        NotifyLevel.Warn, speechText: notOnResults);
                }
                return;
            }

            // The browser is on a search-results page. Check whether the cache is
            // still valid (same query). If the query changed, refresh the cache
            if (!SearchResultsStore.MatchesQuery(page.SearchQuery)){
                string q = page.SearchQuery ?? "";
                _notifier.Notify($"📋 Loading results for \"{q}\"...", speechText: "loading results");
                AppLogger.Info($"Cache mismatch - re-fetching for \"{q}\"");
                var fetched = await YouTubeApiClient.SearchVideosAsync(q);
                SearchResultsStore.Set(fetched, q);
                if (fetched.Count > 0)
                {
                    _notifier.Notify($"📋 {fetched.Count} results ready - say first, second...", speechText: "");
                    for (int i = 0; i < fetched.Count; i++)
                        _notifier.Notify($"  {i + 1}. {fetched[i].Title}", speechText: "");
                }
            }

            var video = SearchResultsStore.Get(index);

            if (video == null){
                string reason = SearchResultsStore.HasResults
                    ? $"only {SearchResultsStore.LastResults.Count} results available"
                    : "no search results yet - say 'search' first";
                _notifier.Notify($"⚠ {reason}", NotifyLevel.Warn, speechText: "no results");
                return;
            }

            string speech = YouTubeSpeechResponses.Get(cmd) ?? $"opening {cmd} video";
            _notifier.Notify($"▶ [{index + 1}] {video.Title}", speechText: speech);
            AppLogger.Info($"Opening video #{index + 1}: {video.VideoId} - {video.Title}");

            // Navigate in the existing YouTube tab via the address bar (Ctrl+L => URL => Enter).
            // Mute the mic for the duration so typed characters don't leak into Vosk.
            if (!await EnsureYouTubeFocusedAsync()) return;
            await Task.Delay(250);

            string url = $"https://www.youtube.com/watch?v={video.VideoId}";
            VoiceListener.MicMuted = true;
            try{
                KeyboardSimulator.PressKey(new KeyCombo(VirtualKey.L, ctrl: true)); // focus address bar
                await Task.Delay(150);
                KeyboardSimulator.TypeText(url);
                await Task.Delay(50);
                KeyboardSimulator.PressKey(VirtualKey.Enter);
                await Task.Delay(500); // let the browser accept the navigation before unmuting
            }finally{
                VoiceListener.MicMuted = false;
            }
        }

        // --- Search via '/' shortcut + YouTube API pre-fetch ---

        public async Task ExecuteSearchAsync(string query){
            AppLogger.Info($"YouTube search: \"{query}\"");

            // Kick off the API search in parallel with browser navigation - no waiting yet
            var apiTask = YouTubeApiClient.SearchVideosAsync(query);

            _notifier.Notify($"🔍 Searching: {query}", speechText: $"searching for {query}");

            try {
                if (!await EnsureYouTubeFocusedAsync()) return;
                await Task.Delay(250);

                // '/' is the universal YouTube hotkey to focus the search box
                KeyboardSimulator.PressKey(VirtualKey.Slash);
                await Task.Delay(200);

                KeyboardSimulator.SelectAll();
                await Task.Delay(50);
                KeyboardSimulator.TypeText(query);
                await Task.Delay(100);
                KeyboardSimulator.PressKey(VirtualKey.Enter);

                // Now wait for the API results (usually already done by this point)
                var results = await apiTask;
                SearchResultsStore.Set(results, query);

                if (results.Count > 0){
                    _notifier.Notify($"📋 {results.Count} results ready - say first, second...", speechText: "");
                    for (int i = 0; i < results.Count; i++)
                        _notifier.Notify($"  {i + 1}. {results[i].Title}", speechText: "");
                }else if (!string.IsNullOrWhiteSpace(AppSettings.YouTubeApiKey)){
                    _notifier.Notify("⚠ YouTube API returned no results", NotifyLevel.Warn, speechText: "");
                }
            } catch (Exception ex) {
                AppLogger.Error("ExecuteSearchAsync failed", ex);
                _notifier.Notify($"✖ Search error: {ex.Message}", NotifyLevel.Warn);
            }
        }

        public async ValueTask DisposeAsync() => await _scheduler.DisposeAsync();
    }
}