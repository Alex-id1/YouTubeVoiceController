namespace YouTubeVoiceController{
    /// <summary>
    /// Maps voice command keywords to user-friendly TTS confirmation phrases.
    /// For example "pause" => "paused", "back" => "back button pressed"
    /// </summary>
    static class YouTubeSpeechResponses{
        private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase){
            ["pause"] = "paused",
            ["play"] = "playing",
            ["stop"] = "stopped",
            ["next"] = "next video",
            ["louder"] = "ok",
            ["quieter"] = "ok",
            ["quiet"] = "ok",
            ["fullscreen"] = "fullscreen pressed",
            ["full screen"] = "fullscreen pressed",
            ["screen"] = "fullscreen pressed",
            ["mute"] = "muted",
            ["unmute"] = "unmuted",
            ["like"] = "liked",
            ["dislike"] = "disliked",
            ["skip"] = "ad skipped",
            ["skip ad"] = "ad skipped",
            ["close ad"] = "ad closed",
            ["subtitles"] = "subtitles toggled",
            ["back"] = "back button pressed",
            ["previous"] = "back button pressed",
            ["first"] = "opening first video",
            ["second"] = "opening second video",
            ["third"] = "opening third video",
            ["fourth"] = "opening fourth video",
            ["fifth"] = "opening fifth video",
            ["sixth"] = "opening sixth video",
            ["seventh"] = "opening seventh video",
            ["eighth"] = "opening eighth video",
            ["ninth"] = "opening ninth video",
            ["tenth"] = "opening tenth video",
        };

        /// <summary>
        /// Returns the TTS phrase for the given command keyword, or null if not mapped
        /// </summary>
        public static string? Get(string voiceCommand){
            if (_map.TryGetValue(voiceCommand, out var phrase)) return phrase;

            // Substring fallback. Corresponds to YouTubeButtonMap
            foreach(var (key, val) in _map.OrderByDescending(k => k.Key.Length))
                if (voiceCommand.Contains(key, StringComparison.OrdinalIgnoreCase))
                    return val;

            return null;
        }
    }
}