using Windows.Media.SpeechSynthesis;
using Windows.Media.Playback;
using Windows.Media.Core;

namespace YouTubeVoiceController{
    /// <summary>
    /// IUserNotifier backed by Windows.Media.SpeechSynthesis (WinRT).
    /// Uses the first available English voice; falls back to the system default voice if no English voice is installed.
    /// Runs TTS asynchronously - fire-and-forget, never blocks the caller.
    /// Warnings get a short "Warning" prefix so the user can tell them apart.
    /// Also delegates to an inner notifier (the log-box) so the UI stays updated
    /// </summary>
    sealed class TtsNotifier : IUserNotifier, IDisposable{
        private readonly SpeechSynthesizer _synth;
        private readonly MediaPlayer _player;
        private readonly IUserNotifier _inner; // log-box notifier - still shows text in the UI

        public TtsNotifier(IUserNotifier inner){
            _inner = inner;
            _synth = new SpeechSynthesizer();
            _player = new MediaPlayer();

            // Pick the first English voice. Fall back to system default
            var englishVoice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));

            if (englishVoice != null){
                _synth.Voice = englishVoice;
                AppLogger.Info($"TtsNotifier: using voice '{englishVoice.DisplayName}' ({englishVoice.Language})");
            }
            else{
                AppLogger.Warning("TtsNotifier: no English voice found, using system default " + $"({_synth.Voice?.DisplayName ?? "unknown"})");
            }
        }

        public void Notify(string message, NotifyLevel level = NotifyLevel.Info, string? speechText = null, bool muteMic = true){
            _inner.Notify(message, level); // always update the log box

            // Empty string = caller explicitly suppresses TTS for this notification
            if (speechText == "") return;

            string toSpeak;
            if (speechText != null){
                toSpeak = speechText; // use caller-supplied human-friendly phrase
            }
            else{
                // Fallback: strip emoji/symbols from the log message
                toSpeak = StripEmoji(message);
                if (string.IsNullOrWhiteSpace(toSpeak)) return;
            }

            if (level == NotifyLevel.Warn) toSpeak = $"Warning. {toSpeak}";
            _ = SpeakAsync(toSpeak, muteMic);
        }

        private async Task SpeakAsync(string text, bool muteMic = true){
            try{
                var stream = await _synth.SynthesizeTextToStreamAsync(text);

                var tcs = new TaskCompletionSource<bool>();

                void OnEnded(MediaPlayer mp, object? _){
                    mp.MediaEnded -= OnEnded;
                    tcs.TrySetResult(true);
                }
                _player.MediaEnded += OnEnded;

                // muteMic=false: short cue phrase with no grammar keywords (e.g. "ok").
                // Leaving the mic live lets the user speak a command right after the signal without it being dropped
                if(muteMic) VoiceListener.MicMuted = true;
                _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                _player.Play();

                // Wait for playback to finish, then a short tail to let room echo decay
                // Short phrases (≤ 4 chars, like "ok") decay faster => smaller tail
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30)); // safety timeout
                await Task.Delay(text.Length <= 4 ? 100 : 300);
            }
            catch (Exception ex){
                AppLogger.Debug($"TtsNotifier.SpeakAsync failed: {ex.Message}");
            }
            finally{
                if (muteMic) VoiceListener.MicMuted = false;
            }
        }

        /// <summary>Removes emoji, leading non-letter symbols, and extra whitespace</summary>
        private static string StripEmoji(string text){
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text){
                // Keep basic Latin, Cyrillic, digits, punctuation; skip emoji...
                int cat = (int)char.GetUnicodeCategory(c);
                if (cat is >= 0 and <= 11 || c == ' ' || c == '.' || c == ',' || c == '!' || c == '?')
                    sb.Append(c);
            }
            return sb.ToString().Trim(' ', '.', ',');
        }

        public void Dispose(){
            _player.Dispose();
            _synth.Dispose();
        }
    }
}