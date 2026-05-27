namespace YouTubeVoiceController{
    /// <summary>
    /// Holds the video results from the most recent YouTube API search together with the search query that produced them.
    ///
    /// The stored query is used by ordinal commands ("first", "second", ...) to detect whether the browser has navigated
    /// to a different search-results page since the last fetch, so the cache can be refreshed automatically
    /// </summary>
    static class SearchResultsStore{
        private static List<YouTubeApiClient.VideoResult> _last  = new();
        private static string _query = "";

        public static IReadOnlyList<YouTubeApiClient.VideoResult> LastResults => _last;

        /// <summary>The search query that produced the current cache. Empty if no results yet</summary>
        public static string LastQuery => _query;

        public static void Set(List<YouTubeApiClient.VideoResult> results, string query = ""){
            _last = results;
            _query = query.Trim();
        }

        /// <summary>Returns the video at 0-based <paramref name="index"/>, or null if out of range</summary>
        public static YouTubeApiClient.VideoResult? Get(int index) => index >= 0 && index < _last.Count ? _last[index] : null;

        public static bool HasResults => _last.Count > 0;

        /// <summary>
        /// Returns true when the cache was built from <paramref name="query"/> (case-insensitive, whitespace-normalised)
        /// </summary>
        public static bool MatchesQuery(string? query){
            if (string.IsNullOrWhiteSpace(query)) return false;
            return string.Equals(_query, query.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static void Clear(){
            _last  = new();
            _query = "";
        }
    }
}