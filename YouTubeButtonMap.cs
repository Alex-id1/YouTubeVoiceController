namespace YouTubeVoiceController{
    /// <summary>
    /// Maps voice command keywords => YOLO class names of the target YouTube UI button.
    /// The comparison is performed as a case-insensitive substring search
    /// </summary>
    static class YouTubeButtonMap{
        private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase){
            //pause/unpause video
            ["pause"] = "play_pause",
            ["play"] = "play_pause",
            ["stop"] = "play_pause",
            ["resume"] = "play_pause",
            ["continue"] = "play_pause",
            ["freeze"] = "play_pause",
            ["hold"] = "play_pause",
            //next video
            ["next"] = "next_video",
            ["next video"] = "next_video",
            ["forward"] = "next_video",
            //volume
            ["louder"] = "volume_btn",
            ["quieter"] = "volume_btn",
            ["quiet"] = "volume_btn",
            ["volume up"] = "volume_btn",
            ["volume down"] = "volume_btn",
            ["softer"] = "volume_btn",
            ["lower"] = "volume_btn",
            //maximize/minimize screen
            ["fullscreen"] = "fullscreen",
            ["full screen"] = "fullscreen",
            ["screen"] = "fullscreen",
            ["maximize"] = "fullscreen",
            ["expand"] = "fullscreen",
            ["minimize"] = "fullscreen",
            //mute/unmute
            ["mute"] = "mute",
            ["unmute"] = "mute",
            ["silence"] = "mute",
            ["silent"] = "mute",
            ["sound off"] = "mute",
            ["sound on"] = "mute",
            ["volume off"] = "mute",
            ["volume on"] = "mute",
            //like
            ["like"] = "like",
            ["thumbs up"] = "like",
            //dislike
            ["dislike"] = "dislike",
            ["thumbs down"] = "dislike",
            //skip ad
            ["skip"] = "close-ad",
            ["skip ad"] = "close-ad",
            ["close ad"] = "close-ad",
            ["skip this"] = "close-ad",
            ["dismiss"] = "close-ad",
            //subtitles
            ["subtitles"] = "subtitles",
            ["captions"] = "subtitles",
            //back button
            ["back"] = "browser_back",
            ["previous"] = "browser_back",
            ["go back"] = "browser_back",
            //rewind
            ["rewind"] = "rewind",
            ["replay"] = "rewind",
            ["again"] = "rewind",
            ["repeat"] = "rewind",
        };

        private static readonly HashSet<string> _openCommands = new(StringComparer.OrdinalIgnoreCase) { "open", "open youtube" };

        private static readonly HashSet<string> _searchCommands = new(StringComparer.OrdinalIgnoreCase) { "search", "find" };

        /// <summary>Words that cancel an in-progress search capture. Must be in Vosk grammar</summary>
        public static readonly HashSet<string> CancelCommands = new(StringComparer.OrdinalIgnoreCase) { "cancel" };

        /// <summary>Wake words that activate the 15-second listenning command period</summary>
        private static readonly HashSet<string> _wakeWords = new(StringComparer.OrdinalIgnoreCase) { "youtube" };

        public static bool IsWakeWord(string word) => _wakeWords.Contains(word);

        /// <summary>Commands that are valid on any YouTube page (not just video pages)</summary>
        private static readonly HashSet<string> _globalCommands = new(StringComparer.OrdinalIgnoreCase) { "back", "previous", "go back" };

        public static bool IsGlobalCommand(string cmd) => _globalCommands.Contains(cmd);

        /// <summary>
        /// Commands handled via YOLO visual detection + click, not keyboard shortcuts.
        /// The value is the YOLO class name to detect on screen
        /// </summary>
        private static readonly HashSet<string> _yoloCommands =
            new(StringComparer.OrdinalIgnoreCase){
                "like", "thumbs up",
                "dislike", "thumbs down",
                "skip", "skip ad", "close ad", "skip this", "dismiss",
            };

        public static bool IsYoloCommand(string cmd) => _yoloCommands.Contains(cmd);

        // Ordinal commands for picking a search result by position (1-based for the user, 0-based index internally)
        private static readonly string[] _ordinals = {
            "first", "second", "third", "fourth", "fifth",
            "sixth", "seventh", "eighth", "ninth", "tenth"
        };
        private static readonly HashSet<string> _ordinalSet = new(_ordinals, StringComparer.OrdinalIgnoreCase);

        public static bool IsOrdinalCommand(string cmd) => _ordinalSet.Contains(cmd);

        /// <summary>Returns 0-based index for an ordinal command, or -1 if not found</summary>
        public static int OrdinalIndex(string cmd) => Array.FindIndex(_ordinals, o => o.Equals(cmd, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns the YOLO class name for the given voice command, or null if unrecognized</summary>
        public static string? Resolve(string voiceCommand){
            // Exact match first (VoiceListener already extracted the keyword)
            if (_map.TryGetValue(voiceCommand, out var exact)) return exact;

            // Fallback: longest-key-first substring (covers future text commands)
            foreach (var (keyword, className) in _map.OrderByDescending(k => k.Key.Length))
                if (voiceCommand.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return className;

            return null;
        }

        public static bool IsOpenCommand(string cmd) => _openCommands.Contains(cmd);
        public static bool IsSearchCommand(string cmd) => _searchCommands.Contains(cmd);

        /// <summary>All keywords passed to Vosk grammar - includes wake word, shortcut, open, search, and ordinal commands</summary>
        public static IEnumerable<string> AllKeywords =>
            _map.Keys.Concat(_openCommands).Concat(_searchCommands).Concat(_ordinals).Concat(_wakeWords).Concat(CancelCommands)
                     .Distinct(StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<string> AllClassNames => _map.Values.Distinct();
    }
}