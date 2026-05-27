namespace YouTubeVoiceController{
    /// <summary>Type of the currently active YouTube page</summary>
    enum YouTubePageType{
        Unknown, // not a YouTube page or couldn't read URL
        VideoPage, // youtube.com/watch?v=....
        SearchResults,// youtube.com/results?search_query=....
        Other,// home, channel, playlist, etc
    }

    /// <summary>Parsed state of the active YouTube browser tab</summary>
    record YouTubePageState(YouTubePageType Type, string? VideoId, string? SearchQuery){
        public static readonly YouTubePageState Unknown = new(YouTubePageType.Unknown, null, null);

        /// <summary>Parse a raw browser URL into a YouTubePageState</summary>
        public static YouTubePageState FromUrl(string? url){
            if (string.IsNullOrWhiteSpace(url)) return Unknown;

            Uri uri;
            try { uri = new Uri(url); }
            catch { return Unknown; }

            if (!uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
                return Unknown;

            string path = uri.AbsolutePath;

            // Video page: /watch?v=VIDEO_ID
            if (path.Equals("/watch", StringComparison.OrdinalIgnoreCase)){
                string? vid = GetQueryParam(uri.Query, "v");
                return new YouTubePageState(YouTubePageType.VideoPage, vid, null);
            }

            // Search results: /results?search_query=...
            if (path.Equals("/results", StringComparison.OrdinalIgnoreCase)){
                string? q = GetQueryParam(uri.Query, "search_query");
                return new YouTubePageState(YouTubePageType.SearchResults, null, q);
            }

            return new YouTubePageState(YouTubePageType.Other, null, null);
        }

        /// <summary>Minimal query-string parser - avoids System.Web dependency</summary>
        private static string? GetQueryParam(string query, string key){
            // query is like "?v=abc&list=xyz" or "v=abc&list=xyz"
            ReadOnlySpan<char> q = query.AsSpan().TrimStart('?');
            while (!q.IsEmpty){
                int amp = q.IndexOf('&');
                ReadOnlySpan<char> pair = amp < 0 ? q : q[..amp];
                int eq = pair.IndexOf('=');
                if (eq > 0){
                    string k = Uri.UnescapeDataString(pair[..eq].ToString());
                    if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                        return Uri.UnescapeDataString(pair[(eq + 1)..].ToString().Replace('+', ' '));
                }
                q = amp < 0 ? ReadOnlySpan<char>.Empty : q[(amp + 1)..];
            }
            return null;
        }
    }
}