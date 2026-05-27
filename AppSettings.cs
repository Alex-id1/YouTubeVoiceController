namespace YouTubeVoiceController{
    /// <summary>
    /// Central store for all application settings.
    /// <para>
    /// User-configurable values (mic gain) are persisted to <c>user_settings.cfg</c>
    /// next to the executable and loaded at startup via <see cref="Load"/>.
    /// </para>
    /// <para>
    /// A YouTube Data API v3 key is required for the voice search feature.
    /// See the project README for instructions on how to obtain and configure one.
    /// </para>
    /// </summary>
    using DirectMLPredictor;
    public static class AppSettings
    {
        private static readonly string _settingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YouTubeVoiceController", "user_settings.cfg");

        // Path to the YOLO model for YouTube UI elements (like/dislike/close-ad)
        public static string ModelPath { get; set; } =
            Path.Combine(AppContext.BaseDirectory, "Models", "YouTubeVoiceCtrl(8tiles_256)n.onnx");

        // YOLO inference thresholds
        public const float YoloConfThreshold = 0.25f;
        public const float YoloIouThreshold = 0.4f;//0.6f;

        // Minimum confidence to act on a detection
        public const float DetectionMinConfidence    = 0.5f;
        public const float TileDetectionMinConfidence = 0.3f;

        // Early-exit threshold: stop tiling as soon as we find a detection this confident
        public const float EarlyExitConfidence = 0.6f;

        // Use tiled inference (recommended for widescreen monitors)
        public const bool UseTiledInference = true;

        // Path to the Vosk model folder - bundled with the app, not user-configurable
        public static string VoskModelPath => Path.Combine(AppContext.BaseDirectory, "Models", "vosk-model-small-en-us-0.15");

        // Path to the Whisper model - bundled with the app, not user-configurable
        public static string WhisperModelPath => Path.Combine(AppContext.BaseDirectory, "Models", "ggml-tiny.en-q5_1.bin");

        // Mic gain for Vosk grammar mode (1 = no boost, 8 = max). Default 1
        public static int MicGain { get; set; } = 1;

        // Preferred YOLO execution provider. Defaults to GPU; automatically switched to
        // CPU and persisted if the GPU is unavailable on this machine.
        public static ExecutionProviderMode PreferredExecMode { get; set; } = ExecutionProviderMode.GPU_ONLY;

        // YouTube Data API v3 key - loaded from encrypted api_keys.cfg at startup.
        // The file is excluded from source control via .gitignore and ships only in the installer.
        // To use from source: generate your own key and run GenerateApiKeysCfg
        public static string YouTubeApiKey { get; set; } = "";

        // ---- Persistence -------------------------------------------------------------

        /// <summary>Loads saved settings from disk. Call once at startup</summary>
        public static void Load(){
            // Encrypted api_keys.cfg ships with the installer - overrides the embedded default
            string? encryptedKey = ApiKeyStore.ReadYouTubeApiKey();
            if (!string.IsNullOrWhiteSpace(encryptedKey))
                YouTubeApiKey = encryptedKey;

            if (!File.Exists(_settingsFile)) return;
            foreach (var line in File.ReadAllLines(_settingsFile)){
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;
                switch (parts[0].Trim()){
                    case "MicGain":
                        if (int.TryParse(parts[1].Trim(), out int g) && g >= 1 && g <= 8)
                            MicGain = g;
                        break;
                    case "ExecMode":
                        if (Enum.TryParse<ExecutionProviderMode>(parts[1].Trim(), out var mode))
                            PreferredExecMode = mode;
                        break;
                }
            }
        }

        /// <summary>Saves user-configurable settings to disk</summary>
        public static void Save(){
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllLines(_settingsFile, new[]{
                $"MicGain={MicGain}",
                $"ExecMode={PreferredExecMode}",
            });
        }
    }
}