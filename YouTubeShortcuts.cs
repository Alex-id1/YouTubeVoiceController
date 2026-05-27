namespace YouTubeVoiceController{
    /// <summary>
    /// Maps recognized voice keywords to YouTube native keyboard shortcuts.
    /// These work in any modern browser when the YouTube player has focus
    /// </summary>
    static class YouTubeShortcuts{
        private static readonly Dictionary<string, KeyCombo> _map = new(StringComparer.OrdinalIgnoreCase){
            // Play / Pause
            ["pause"] = new(VirtualKey.K),
            ["play"] = new(VirtualKey.K),
            ["stop"] = new(VirtualKey.K),
            ["resume"] = new(VirtualKey.K),
            ["continue"] = new(VirtualKey.K),
            ["freeze"] = new(VirtualKey.K),
            ["hold"] = new(VirtualKey.K),
            // Next video
            ["next"] = new(VirtualKey.N, shift: true),
            ["next video"] = new(VirtualKey.N, shift: true),
            ["forward"] = new(VirtualKey.N, shift: true),
            // Volume up
            ["louder"] = new(VirtualKey.ArrowUp),
            ["volume up"] = new(VirtualKey.ArrowUp),
            // Volume down
            ["quieter"] = new(VirtualKey.ArrowDown),
            ["quiet"] = new(VirtualKey.ArrowDown),
            ["volume down"] = new(VirtualKey.ArrowDown),
            ["softer"] = new(VirtualKey.ArrowDown),
            ["lower"] = new(VirtualKey.ArrowDown),
            // Fullscreen
            ["fullscreen"] = new(VirtualKey.F),
            ["full screen"] = new(VirtualKey.F),
            ["screen"] = new(VirtualKey.F),
            ["maximize"] = new(VirtualKey.F),
            ["expand"] = new(VirtualKey.F),
            ["minimize"] = new(VirtualKey.F),
            // Mute
            ["mute"] = new(VirtualKey.M),
            ["unmute"] = new(VirtualKey.M),
            ["silence"] = new(VirtualKey.M),
            ["silent"] = new(VirtualKey.M),
            ["sound off"] = new(VirtualKey.M),
            ["sound on"] = new(VirtualKey.M),
            ["volume off"] = new(VirtualKey.M),
            ["volume on"] = new(VirtualKey.M),
            // Subtitles
            ["subtitles"] = new(VirtualKey.C),
            ["captions"] = new(VirtualKey.C),
            // Rewind
            ["rewind"] = new(VirtualKey.Zero),
            ["replay"] = new(VirtualKey.Zero),
            ["again"] = new(VirtualKey.Zero),
            ["repeat"] = new(VirtualKey.Zero),
            // Browser navigation: Alt+Left = Back in Chrome/Edge/Firefox
            ["back"] = new(VirtualKey.ArrowLeft, alt: true),
            ["previous"] = new(VirtualKey.ArrowLeft, alt: true),
            ["go back"] = new(VirtualKey.ArrowLeft, alt: true),
        };

        /// <summary>
        /// Returns the shortcut for an exact recognized keyword, or null if not in the table.
        /// VoiceListener already extracts the canonical keyword, so exact match is enough
        /// </summary>
        public static KeyCombo? Resolve(string command) => _map.TryGetValue(command, out var combo) ? combo : null;
    }
}