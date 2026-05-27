using System.Text.Json;

namespace YouTubeVoiceController{
    /// <summary>
    /// Wrapper around the YouTube Data API v3 search endpoint.
    /// Returns up to <c>maxResults</c> videos matching a query.
    /// Requires a valid API key in <see cref="AppSettings.YouTubeApiKey"/>
    /// </summary>
    static class YouTubeApiClient{
        public record VideoResult(string VideoId, string Title, string ChannelTitle);

        private static readonly HttpClient _http = new();

        /// <summary>
        /// Returns up to <paramref name="maxResults"/> videos for <paramref name="query"/>.
        /// Returns an empty list if the API key is missing or an error occurs
        /// </summary>
        public static async Task<List<VideoResult>> SearchVideosAsync(string query, int maxResults = 10){
            string key = AppSettings.YouTubeApiKey;
            if (string.IsNullOrWhiteSpace(key)){
                AppLogger.Debug("YouTubeApiClient: no API key configured - skipping search");
                return new();
            }

            string url = "https://www.googleapis.com/youtube/v3/search" + $"?part=snippet&q={Uri.EscapeDataString(query)}" +
                $"&type=video&maxResults={maxResults}&key={key}";

            try{
                string json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                // Check for API error response
                if (doc.RootElement.TryGetProperty("error", out var err)){
                    string msg = err.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "unknown";
                    AppLogger.Warning($"YouTubeApiClient: API error - {msg}");
                    return new();
                }

                var results = new List<VideoResult>();
                if (!doc.RootElement.TryGetProperty("items", out var items)) return results;

                foreach (var item in items.EnumerateArray()){
                    // Some items (channels, playlists) lack videoId even with type=video filter
                    if (!item.TryGetProperty("id", out var idEl)) continue;
                    if (!idEl.TryGetProperty("videoId", out var vidEl)) continue;
                    string videoId = vidEl.GetString() ?? "";
                    if (string.IsNullOrEmpty(videoId)) continue;

                    if (!item.TryGetProperty("snippet", out var snippet)) continue;
                    string title = snippet.TryGetProperty("title", out var t) ? t.GetString() ?? "(no title)" : "(no title)";
                    string channel = snippet.TryGetProperty("channelTitle", out var c) ? c.GetString() ?? "" : "";

                    results.Add(new(videoId, title, channel));
                }

                AppLogger.Info($"YouTubeApiClient: {results.Count} results for \"{query}\"");
                return results;
            }
            catch (Exception ex){
                AppLogger.Warning($"YouTubeApiClient: request failed - {ex.Message}");
                return new();
            }
        }
    }
}