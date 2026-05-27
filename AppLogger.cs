namespace YouTubeVoiceController{
    public static class AppLogger{
        private static string? _logFilePath;
        private static readonly object _lock = new();

        public static void Initialize(){
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "YouTubeVoiceController", "Logs");
            Directory.CreateDirectory(logDir);

            _logFilePath = Path.Combine(logDir, $"log_{DateTime.Now:yyyy-MM-dd}.txt");

            foreach (var file in Directory.GetFiles(logDir, "log_*.txt")) {
                if (File.GetCreationTime(file) < DateTime.Now.AddDays(-7))
                    File.Delete(file);
            }

            Info($"=== Session started. Log: {_logFilePath} ===");
        }

        public static void Debug(string message) => Log("DEBUG", message);
        public static void Info(string message) => Log("INFO ", message);
        public static void Warning(string message) => Log("WARN ", message);
        public static void Error(string message) => Log("ERROR", message);
        public static void Error(string message, Exception ex) => Log("ERROR", $"{message}: {ex}");

        private static void Log(string level, string message){
            string line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
            System.Diagnostics.Debug.WriteLine(line);
            if (_logFilePath == null) return;
            try {
                lock (_lock) {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            } catch { }
        }
    }
}