using YouTubeVoiceController.DirectMLPredictor;

namespace YouTubeVoiceController{
    public partial class MainForm: Form {
        private CommandDispatcher? _dispatcher;
        private VoiceListener? _voice;
        private bool _running;

        private readonly IUserNotifier _notifier;
        private TtsNotifier? _tts;
        private DebugCommandServer? _debugServer;

        public MainForm() {
            InitializeComponent();

            // Window icon (title bar + taskbar + system tray)
            string iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if(File.Exists(iconPath)) {
                var appIcon = new Icon(iconPath);
                Icon = appIcon;
                notifyIcon.Icon = appIcon;
            }

            // Help button icon - loaded from embedded resource, scaled to button size
            var infoStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("YouTubeVoiceController.info.ico");
            if(infoStream != null) {
                int size = helpBtn.Height - 6;
                using var ico = new Icon(infoStream);
                using var original = ico.ToBitmap();
                helpBtn.Image = new Bitmap(original, new Size(size, size));
                helpBtn.Text = "";
                helpBtn.Padding = Padding.Empty;
                helpBtn.ImageAlign = ContentAlignment.MiddleCenter;
            }

            AppLogger.Initialize();
            AppSettings.Load();

            VoiceListener.GainGrammar = AppSettings.MicGain;
            micGainSlider.Value = AppSettings.MicGain;
            micGainLabel.Text = $"Mic gain: {AppSettings.MicGain}x";

            var logBoxNotifier = new LogBoxNotifier(this);
            _tts = new TtsNotifier(logBoxNotifier);
            _notifier = _tts;

            apiKeyInput.Text = AppSettings.YouTubeApiKey;

            StartDebugServer();
        }

        // --- Start / Stop ----------------------------------------------------

        private void StartStopBtn_Click(object sender, EventArgs e) {
            if(_running)
                StopController();
            else
                StartController();
        }

        private async void StartController() {
            startStopBtn.Enabled = false;
            startStopBtn.Text = "Loading...";
            statusLabel.Text = "Loading Vosk model...";
            loadingBar.Visible = true;

            try {
                var scheduler = new InferenceScheduler(AppSettings.ModelPath, AppSettings.PreferredExecMode);
                scheduler.Start();

                _dispatcher = new CommandDispatcher(scheduler, _notifier);

                var keywords = YouTubeButtonMap.AllKeywords.ToArray();
                _voice = await Task.Run(() => new VoiceListener(keywords));
                _voice.CommandRecognized += async (cmd, query) => await _dispatcher.DispatchAsync(cmd, query);
                _voice.SearchModeChanged += OnSearchModeChanged;
                _voice.WakeStateChanged += OnWakeStateChanged;
                _voice.Start();

                _running = true;
                startStopBtn.Text = "Stop";
                startStopBtn.Enabled = true;
                statusLabel.Text = "Listening...";

                AppLogger.Info($"Started. YOLO: {AppSettings.ModelPath}, " + $"Vosk: {Path.GetFileName(AppSettings.VoskModelPath)}, " +
                               $"Mode: {AppSettings.PreferredExecMode}");
            } catch(Exception ex) {
                AppLogger.Error("StartController failed", ex);
                _notifier.Notify($"✖ {ex.Message}", NotifyLevel.Warn);

                startStopBtn.Text = "Start";
                startStopBtn.Enabled = true;
            } finally {
                loadingBar.Visible = false;
            }
        }

        private async void StopController() {
            _voice?.Stop();
            _voice?.Dispose();
            _voice = null;

            if(_dispatcher != null) {
                await _dispatcher.DisposeAsync();
                _dispatcher = null;
            }

            _running = false;
            startStopBtn.Text = "Start";
            statusLabel.Text = "Stopped";

            AppLogger.Info("Stopped");
        }

        // --- Voice event handlers ----------------------------------------------------------
        private void OnWakeStateChanged(bool active) {
            if(active)
                // muteMic:false - "ok" has no grammar keywords, keeping mic live lets
                // the user's command spoken right after the cue be captured without dropping
                _notifier.Notify("🟢 Listening for commands (15 sec)...", speechText: "ok", muteMic: false);
            else
                _notifier.Notify("⚪ Command window closed", speechText: "");
        }

        private void OnSearchModeChanged(bool active) {
            if(active)
                _notifier.Notify("🎤 Say your search query...", speechText: "ok");
            else
                _notifier.Notify("⚠ Search cancelled - no query heard", NotifyLevel.Warn, speechText: "search cancelled");
        }

        // ---- Mic gain slider ------------------------------------------------------
        private void MicGainSlider_Scroll(object? sender, EventArgs e) {
            int gain = micGainSlider.Value;
            VoiceListener.GainGrammar = gain;
            AppSettings.MicGain = gain;
            AppSettings.Save();
            micGainLabel.Text = $"Mic gain: {gain}x";
        }

        // ---- Help dialog ---------------------------------------------------------
        private void HelpBtn_Click(object? sender, EventArgs e) => new HelpForm().ShowDialog(this);

        // ---- API key (hidden field, loaded from encrypted cfg) -------------------
        private void ApiKeyInput_Leave(object? sender, EventArgs e) {
            string key = apiKeyInput.Text.Trim();
            if(key == AppSettings.YouTubeApiKey)
                return;
            AppSettings.YouTubeApiKey = key;
            AppSettings.Save();
            AppLogger.Info(string.IsNullOrEmpty(key) ? "YouTube API key cleared" : "YouTube API key saved");
        }

        // ---- Notification rendering ----------------------------------------------
        /// <summary>
        /// Updates the status label and appends a colored line to the log box
        /// Called by <see cref="LogBoxNotifier"/>
        /// </summary>
        internal void HandleNotification(string msg, NotifyLevel level) {
            if(InvokeRequired) { Invoke(() => HandleNotification(msg, level)); return; }

            statusLabel.Text = msg;

            var color = level == NotifyLevel.Warn ? Color.Orange : Color.LimeGreen;

            int start = logBox.TextLength;
            logBox.AppendText(msg + Environment.NewLine);
            logBox.Select(start, msg.Length);
            logBox.SelectionColor = color;
            logBox.Select(logBox.TextLength, 0);
            logBox.SelectionColor = logBox.ForeColor;
            logBox.ScrollToCaret();
        }

        private sealed class LogBoxNotifier: IUserNotifier {
            private readonly MainForm _form;
            public LogBoxNotifier(MainForm form) => _form = form;

            public void Notify(string message, NotifyLevel level = NotifyLevel.Info, string? speechText = null, bool muteMic = true) =>
                _form.HandleNotification(message, level);
        }

        // ---- Tray support ------------------------------------------------------------

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if(WindowState == FormWindowState.Minimized)
                MinimizeToTray();
        }

        private void MinimizeToTray() {
            Hide();
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(
                timeout: 1500,
                tipTitle: "YouTube Voice Controller",
                tipText: _running ? "Listening in background..." : "Minimized to tray",
                tipIcon: ToolTipIcon.Info);
        }

        private void RestoreFromTray() {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
            Activate();
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e) => RestoreFromTray();

        private void TrayShowItem_Click(object? sender, EventArgs e) => RestoreFromTray();

        private async void TrayExitItem_Click(object? sender, EventArgs e) {
            notifyIcon.Visible = false;
            if(_running)
                await Task.Run(StopController);
            Application.Exit();
        }

        protected override async void OnFormClosing(FormClosingEventArgs e) {
            if(_running)
                await Task.Run(StopController);
            notifyIcon.Visible = false;
            _tts?.Dispose();
            _debugServer?.Dispose();
            base.OnFormClosing(e);
        }

        // ===== Debug code =========================================================
        // Methods below are used by DebugCommandServer (named pipe) and can also be
        // called programmatically during development / testing
        //
        // PowerShell usage (app must be running and started):
        //   Send-DebugCmd "like" => dispatches voice command
        //   Send-DebugCmd "search:lo-fi" => executes search
        //
        // Helper function to paste into PowerShell:
        //   function Send-DebugCmd($msg) {
        //     $p = New-Object IO.Pipes.NamedPipeClientStream('.','YVCDebug','Out')
        //     $p.Connect(2000)
        //     $w = New-Object IO.StreamWriter($p)
        //     $w.WriteLine($msg); $w.Flush(); $p.Dispose()
        //   }

        /// <summary>Enable or disable free-form Vosk recognition (all text, no keyword filter)</summary>
        private static void SetFreeFormDebug(bool enabled) => VoiceListener.FreeFormDebug = enabled;

        /// <summary>Send a text command as if it were recognized by the voice pipeline</summary>
        internal async Task DebugDispatchAsync(string text) {
            if(string.IsNullOrWhiteSpace(text))
                return;
            if(_dispatcher == null) { _notifier.Notify("⚠ Start the controller first", NotifyLevel.Warn); return; }
            await _dispatcher.DispatchAsync(text);
        }

        /// <summary>Execute a YouTube search query as if spoken by the user</summary>
        internal async Task DebugExecuteSearchAsync(string query) {
            if(string.IsNullOrWhiteSpace(query))
                return;
            if(_dispatcher == null) { _notifier.Notify("⚠ Start the controller first", NotifyLevel.Warn); return; }
            await _dispatcher.ExecuteSearchAsync(query);
        }

        /// <summary>Starts the debug named-pipe server. Called once at startup</summary>
        private void StartDebugServer() {
            _debugServer = new DebugCommandServer(
                dispatchCmd: text => Invoke<Task>(() => DebugDispatchAsync(text))!,
                dispatchSearch: query => Invoke<Task>(() => DebugExecuteSearchAsync(query))!);
            _debugServer.Start();
        }

        // ===== End of debug code ==================================================
    }
}