using System.Security.Cryptography;
using System.Text;

namespace YouTubeVoiceController
{
    /// <summary>
    /// Reads/writes the YouTube API key from an AES-encrypted file (api_keys.cfg).
    /// The file is excluded from source control via .gitignore - only the compiled
    /// binary ships with it (inside the installer). The encryption key is embedded
    /// in the binary; this is obfuscation, not true security, but is sufficient to
    /// prevent casual inspection of the file in Program Files.
    /// </summary>
    static class ApiKeyStore
    {
        private static readonly string _path =
            Path.Combine(AppContext.BaseDirectory, "api_keys.cfg");

        // 32-byte AES-256 key and 16-byte IV - embedded in binary (obfuscation only)
        private static readonly byte[] _key = Encoding.UTF8.GetBytes("Yt7$vK!2pQmXnRzW8dLcAeHuBsFjOiTg");
        private static readonly byte[] _iv  = Encoding.UTF8.GetBytes("N3wVoiceCtrl!Key");

        /// <summary>
        /// Reads the YouTube API key from the encrypted file.
        /// Returns null if the file doesn't exist or can't be decrypted.
        /// </summary>
        public static string? ReadYouTubeApiKey()
        {
            if (!File.Exists(_path)){
                AppLogger.Warning($"ApiKeyStore: file not found at {_path}");
                return null;
            }
            try
            {
                byte[] cipher = File.ReadAllBytes(_path);
                AppLogger.Debug($"ApiKeyStore: read {cipher.Length} bytes from {_path}");
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                using var dec = aes.CreateDecryptor();
                using var ms = new MemoryStream(cipher);
                using var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read);
                using var reader = new StreamReader(cs, Encoding.UTF8);
                string text = reader.ReadToEnd();
                AppLogger.Debug($"ApiKeyStore: decrypted text length={text.Length}");
                foreach (var line in text.Split('\n'))
                {
                    var parts = line.Trim().Split('=', 2);
                    if (parts.Length == 2 && parts[0] == "YouTubeApiKey"){
                        AppLogger.Info($"ApiKeyStore: API key loaded (length={parts[1].Trim().Length})");
                        return parts[1].Trim();
                    }
                }
                AppLogger.Warning("ApiKeyStore: decrypted but no 'YouTubeApiKey=' line found");
                return null;
            }
            catch (Exception ex){
                AppLogger.Error("ApiKeyStore: failed to read/decrypt api_keys.cfg", ex);
                return null;
            }
        }

        /// <summary>
        /// Writes the YouTube API key to the encrypted file.
        /// Called during the build/packaging step, not at runtime.
        /// </summary>
        public static void WriteYouTubeApiKey(string apiKey){
            string text = $"YouTubeApiKey={apiKey}";
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            using var enc = aes.CreateEncryptor();
            using var ms  = new MemoryStream();
            using var cs  = new CryptoStream(ms, enc, CryptoStreamMode.Write);
            using var writer = new StreamWriter(cs, Encoding.UTF8);
            writer.Write(text);
            writer.Flush();
            cs.FlushFinalBlock();
            File.WriteAllBytes(_path, ms.ToArray());
        }
    }
}