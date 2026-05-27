using System.Text;
using Whisper.net;

namespace YouTubeVoiceController{
    /// <summary>
    /// Free-form speech-to-text via Whisper (whisper.cpp through Whisper.net).
    /// Used to transcribe spoken YouTube search queries - far more robust to
    /// noise and low-quality microphones than the grammar-constrained Vosk model
    /// </summary>
    sealed class WhisperTranscriber : IDisposable
    {
        private readonly WhisperFactory _factory;
        private readonly WhisperProcessor _processor;
        private readonly SemaphoreSlim _lock = new(1, 1); // one transcription at a time

        public WhisperTranscriber(string modelPath){
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Whisper model not found: {modelPath}");

            _factory = WhisperFactory.FromPath(modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("en")
                .WithNoContext()
                .WithTemperature(0f)
                .Build();
        }

        /// <summary>
        /// Transcribes 16 kHz mono float samples (range -1...1) to text.
        /// Returns an empty string on failure
        /// </summary>
        public async Task<string> TranscribeAsync(float[] samples){
            await _lock.WaitAsync();
            try{
                var sb = new StringBuilder();
                await foreach (var segment in _processor.ProcessAsync(samples))
                    sb.Append(segment.Text);

                return sb.ToString().Trim();
            }
            catch (Exception ex){
                AppLogger.Warning($"WhisperTranscriber failed: {ex.Message}");
                return "";
            }
            finally{
                _lock.Release();
            }
        }

        public void Dispose(){
            _processor.Dispose();
            _factory.Dispose();
        }
    }
}