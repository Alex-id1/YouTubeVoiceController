using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using NAudio.Wave;
using Vosk;

namespace YouTubeVoiceController{
    /// <summary>
    /// Offline command recognition via Vosk in grammar-constrained mode.
    /// Vosk's recognizer accepts a JSON list of allowed phrases - it will only emit words from that list (or "[unk]"),
    /// so misfires like "the screen" from Whisper are impossible by design.
    /// Pipeline:
    ///     => NAudio WaveInEvent (16 kHz mono) 
    ///     => VoskRecognizer. AcceptWaveform (Vosk does its own VAD/endpointing)
    ///     => keyword match
    ///     => CommandRecognized
    /// </summary>
    sealed class VoiceListener : IDisposable{
        /// <summary>Fires when a command keyword is recognized. Second arg is the search query (non-null only for search commands)</summary>
        public event Action<string, string?>? CommandRecognized;

        /// <summary>Fires when search-capture mode is entered (true) or cancelled by timeout (false)</summary>
        public event Action<bool>? SearchModeChanged;

        /// <summary>When true: any non-"[unk]" recognized text is emitted, no keyword match required</summary>
        public static bool FreeFormDebug { get; set; } = false;

        /// <summary>
        /// When true the microphone is silenced - audio callbacks are dropped.
        /// Set by TtsNotifier while speech is playing to prevent TTS feedback loops
        /// </summary>
        public static volatile bool MicMuted = false;

        // -- Wake-word gate --------------------------------------------------------------
        // Commands are only dispatched while the gate is open (i.e. within WakeWindowMs after the last "youtube" or accepted command)

        /// <summary>Fires when the wake-word gate opens (true) or closes by timeout (false)</summary>
        public event Action<bool>? WakeStateChanged;

        private const int WakeWindowMs = 15_000;
        private volatile bool _wakeActive  = false;
        private System.Threading.Timer? _wakeTimer;

        // Debounce: ignore repeated same command within this window
        private const int CommandDebounceMs = 2_000;
        private string _lastCommand = "";
        private long _lastCommandTick = 0; // Environment.TickCount64

        private readonly string[] _keywords;
        private readonly WaveInEvent _waveIn;
        private readonly Model _model;
        private readonly VoskRecognizer _recognizer;

        // Whisper free-form transcription - fed a complete audio buffer captured after "search"
        private WhisperTranscriber? _whisper;
        private MemoryStream? _searchBuffer; // gain-boosted audio accumulates here
        private volatile bool _awaitingSearchQuery;
        private bool _searchSpeechStarted;
        private int _searchSilenceMs;
        private int _searchElapsedMs;
        private int _searchFinalized; // Interlocked 0/1 - finalize-once guard
        private System.Threading.Timer? _searchTimeoutTimer;

        private const int SearchMaxMs = 8_000; // hard cap on capture duration
        private const int SearchEndSilenceMs = 700; // trailing silence => user finished speaking
        // Adaptive speech detection:
        // During normal mode we track ambient RMS via EMA, then at search-mode entry
        // compute a dynamic threshold = ambient x SpeechToNoiseRatio.
        // This makes Whisper detection self-calibrate to any microphone without manual tuning
        private double _ambientRms = 30.0; // EMA of background noise. Safe default until mic data arrives
        private const double AmbientAlpha = 0.02; // EMA smoothing (~50 chunks ~0.5 sec to fully adapt)
        // In a quiet room (low ambient) a high SNR ratio avoids false triggers from noise.
        // When speakers are playing (high ambient) the ratio must drop so the user's voice
        // - which only adds a small delta on top of the speaker signal - can be detected.
        // Formula: snr = clamp(BaseSnr - ambient/AmbientScale, MinSnr, BaseSnr)
        // Examples: ambient=30 => snr=3.95 => threshold~118 (quiet room)
        //           ambient=1868 => snr=1.15 => threshold~2148 (laptop speakers)
        private const double SpeechSnrBase = 4.0; // multiplier at zero ambient
        private const double SpeechSnrMin = 1.15; // never go below 15% above ambient
        private const double SpeechSnrScale = 600.0; // ambient RMS at which snr halves
        private const double SpeechRmsFloor = 20.0;  // absolute minimum above ambient
        private double _dynamicSpeechThreshold = 120.0; // recomputed in EnterSearchMode()

        // Pre-roll: circular buffer of the last 1.5s of raw audio
        // When search mode is entered the pre-roll is prepended to the search buffer so that
        // speech which began before or during the TTS "listening" signal is not lost
        private const int PreRollBytes = 16_000 * 2 * 2; // 1.5s x 16kHz x 16-bit mono = 96 000. Round up to 2sec = 128 000 to be safe
        private readonly byte[] _preRoll = new byte[PreRollBytes];
        private int _preRollHead = 0; // next write position (circular)
        private int _preRollFill = 0; // bytes currently valid (0...PreRollBytes)

        // --- Vosk pipeline (decoupled from audio callback) -----------------------------
        // The audio callback dumps gain-boosted chunks into _voskChannel and returns immediately.
        // VoskLoopAsync drains the channel in a background thread, so Vosk inference never
        // blocks the audio thread - Whisper capture stays clean even on slow CPUs.
        // A null chunk in the channel is a "mute gap" sentinel: VoskLoop resets the recognizer
        // on the next real chunk so stale acoustic context doesn't corrupt the first post-TTS word.
        private readonly Channel<byte[]?> _voskChannel = Channel.CreateBounded<byte[]?>(
            new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        private Task _voskTask = Task.CompletedTask;
        private readonly CancellationTokenSource _voskCts = new();

        public VoiceListener(IEnumerable<string> keywords){
            if (!Directory.Exists(AppSettings.VoskModelPath))
                throw new DirectoryNotFoundException($"Vosk model folder not found: {AppSettings.VoskModelPath}\n" +
                    "Download a model (e.g. vosk-model-small-en-us-0.15) from " +
                    "https://alphacephei.com/vosk/models and select its folder via the Browse button");

            // Sort longest-first so "full screen" wins over "screen"
            _keywords = keywords.OrderByDescending(k => k.Length).ToArray();

            Vosk.Vosk.SetLogLevel(-1); // silence Vosk's stdout chatter

            _model = new Model(AppSettings.VoskModelPath);

            // Grammar mode: recognizer is locked to these phrases + "[unk]" sink.
            // The "[unk]" token is critical - without it Vosk would force-fit every utterance into one of our keywords
            var grammarList = _keywords.Append("[unk]").ToArray();
            string grammar = JsonSerializer.Serialize(grammarList);

            _recognizer = new VoskRecognizer(_model, 16_000f, grammar);
            _recognizer.SetWords(false);

            _waveIn = new WaveInEvent{
                DeviceNumber= 0,
                WaveFormat = new WaveFormat(rate: 16_000, bits: 16, channels: 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += (_, _) => AppLogger.Info("VoiceListener: recording stopped");

            // Whisper for free-form search queries - non-fatal if the model is missing
            try{
                _whisper = new WhisperTranscriber(AppSettings.WhisperModelPath);
                AppLogger.Info($"VoiceListener: Whisper model loaded ({Path.GetFileName(AppSettings.WhisperModelPath)})");
            } catch (Exception ex){
                AppLogger.Warning($"VoiceListener: Whisper unavailable - voice search disabled. {ex.Message}");
            }

            AppLogger.Info($"VoiceListener ready. Model: {Path.GetFileName(AppSettings.VoskModelPath)}, " + $"{_keywords.Length} keywords");
        }

        public void Start(){
            _voskTask = Task.Run(VoskLoopAsync);
            _waveIn.StartRecording();
            AppLogger.Info("VoiceListener: listening...");
        }

        public void Stop() => _waveIn.StopRecording();

        // --- Audio callback -----------------------------------------------------------------------

        /// <summary>
        /// Software gain factor applied to incoming PCM. Some mics (Realtek with default Windows level) produce extremely quiet raw signal
        /// for example RMS ~30 when Vosk wants ~1000+.
        /// Legacy waveIn API doesn't apply Windows' modern auto-gain, so we boost it in software
        /// </summary>
        // Grammar mode tolerates heavy gain (tiny word list). Whisper needs clean,
        // non-clipped audio - a gentler boost keeps loud speech from loud sppech
        /// <summary>Software gain applied to mic input before Vosk. 1 = no boost, 8 = maximum. User-configurable via UI slider</summary>
        public static float GainGrammar { get; set; } = 1.0f;
        private const float GainSearch = 1.0f; // Whisper normalises internally - no boost needed

        // Keywords that cancel an in-progress search capture.
        // "stop" is already in YouTubeButtonMap._map (=> play_pause) so it's in the Vosk grammar
        // "cancel" is located in YouTubeButtonMap.CancelCommands and is added to AllKeywords from there
        private static readonly HashSet<string> _cancelWords = new(StringComparer.OrdinalIgnoreCase) { "stop", "cancel" };

        private void OnDataAvailable(object? sender, WaveInEventArgs e){
            // Pre-roll always captures raw audio so a "find" command brings ~2 s of context with it.
            WritePreRoll(e.Buffer, e.BytesRecorded);

            // Ambient RMS for adaptive Whisper threshold (raw signal, before any gain)
            if (!_awaitingSearchQuery)
                _ambientRms = _ambientRms * (1 - AmbientAlpha) + Rms(e.Buffer, e.BytesRecorded) * AmbientAlpha;

            // --- Pipeline 1: Vosk (always running, decoupled) -----------------------------
            // During TTS in normal mode we push a null sentinel so VoskLoop resets acoustic state
            // when audio resumes (prevents TTS audio bleeding into the next command).
            // BUT during search mode we MUST keep feeding Vosk even while TTS plays - the user
            // often starts saying "cancel" before the "ok" TTS finishes, and we'd miss it otherwise.
            // TTS in search mode only says "ok" (not a grammar keyword), so no false positives
            if (MicMuted && !_awaitingSearchQuery){
                _voskChannel.Writer.TryWrite(null);
            } else {
                byte[] voskCopy = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, voskCopy, 0, e.BytesRecorded);
                ApplyGain(voskCopy, e.BytesRecorded, GainGrammar);
                _voskChannel.Writer.TryWrite(voskCopy);
            }

            // --- Pipeline 2: Whisper search buffer (only during search) -------------------
            // Lightweight: just gain + write + RMS check. No recognition here.
            // Cancel detection is handled independently by VoskLoopAsync; when it fires,
            // FinalizeSearch(false) flips _awaitingSearchQuery so the next chunk skips this
            if (_awaitingSearchQuery){
                ApplyGain(e.Buffer, e.BytesRecorded, GainSearch);
                AccumulateSearchAudio(e.Buffer, e.BytesRecorded);
            }
        }

        /// <summary>
        /// Background loop: drains the Vosk channel and runs recognition independently of
        /// the audio callback. In normal mode => dispatches matched commands.
        /// In search mode => checks partial+final results for "cancel"/"stop" to abort capture
        /// </summary>
        private async Task VoskLoopAsync(){
            bool wasMuted = false;
            try{
                await foreach (var chunk in _voskChannel.Reader.ReadAllAsync(_voskCts.Token)){
                    if (chunk == null){
                        wasMuted = true; // mute-gap sentinel - defer reset until real audio arrives
                        continue;
                    }
                    if (wasMuted){
                        wasMuted = false;
                        _recognizer.Reset();
                        AppLogger.Debug("VoiceListener: recognizer reset after TTS mute gap");
                    }

                    bool complete = _recognizer.AcceptWaveform(chunk, chunk.Length);

                    if (_awaitingSearchQuery){
                        // Only check FINAL results - partial results are unstable in constrained
                        // grammar mode (e.g. "standard" briefly looks like "stop" until the rest
                        // of the phonemes arrive). Whisper safety net catches anything Vosk misses
                        if (complete){
                            string text = ParseVoskText(_recognizer.Result());
                            if (!string.IsNullOrWhiteSpace(text) &&
                                _cancelWords.Any(w => Regex.IsMatch(text, $@"\b{w}\b", RegexOptions.IgnoreCase))){
                                AppLogger.Info($"VoiceListener: search cancelled by Vosk: \"{text}\"");
                                _recognizer.Reset();
                                FinalizeSearch(hasSpeech: false);
                            }
                        }
                    } else if (complete){
                        ProcessResult(_recognizer.Result());
                    }
                }
            } catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex){
                AppLogger.Error("VoskLoopAsync crashed", ex);
            }
        }

        /// <summary>Appends raw(pre-gain) audio to the circular pre-roll buffer</summary>
        private void WritePreRoll(byte[] buf, int count){
            // Write into one or two segments
            int remaining = count;
            int srcOffset = 0;
            while (remaining > 0){
                int space = PreRollBytes - _preRollHead;
                int write = Math.Min(remaining, space);
                Buffer.BlockCopy(buf, srcOffset, _preRoll, _preRollHead, write);
                _preRollHead = (_preRollHead + write) % PreRollBytes;
                _preRollFill = Math.Min(_preRollFill + write, PreRollBytes);
                srcOffset += write;
                remaining -= write;
            }
        }

        /// <summary>Drains the pre-roll into the search buffer, applying search gain</summary>
        private void FlushPreRollToSearchBuffer(){
            if (_preRollFill == 0 || _searchBuffer == null) return;

            // Reconstruct in chronological order(oldest => newest)
            byte[] tmp = new byte[_preRollFill];
            if (_preRollFill < PreRollBytes){                
                Buffer.BlockCopy(_preRoll, 0, tmp, 0, _preRollFill);// Buffer not yet full - data starts at index 0
            } else {
                // Buffer is full - oldest byte is at _preRollHead
                int tail = PreRollBytes - _preRollHead;
                Buffer.BlockCopy(_preRoll, _preRollHead, tmp, 0, tail);
                Buffer.BlockCopy(_preRoll, 0, tmp, tail, _preRollHead);
            }

            ApplyGain(tmp, tmp.Length, GainSearch);
            _searchBuffer.Write(tmp, 0, tmp.Length);
            AppLogger.Debug($"VoiceListener: pre-roll {_preRollFill / 32} ms prepended to search buffer");
        }

        /// <summary>Buffers gain-boosted audio during search mode. Finalizes on end-of-speech or hard cap</summary>
        private void AccumulateSearchAudio(byte[] buffer, int bytesRecorded){
            _searchBuffer?.Write(buffer, 0, bytesRecorded);

            // 16 kHz, 16-bit, mono => 32 bytes per millisecond
            int chunkMs = bytesRecorded / 32;
            _searchElapsedMs += chunkMs;

            if (Rms(buffer, bytesRecorded) > _dynamicSpeechThreshold){
                _searchSpeechStarted = true;
                _searchSilenceMs = 0;
            }
            else if (_searchSpeechStarted)
                _searchSilenceMs += chunkMs;


            if (_searchSpeechStarted && _searchSilenceMs >= SearchEndSilenceMs)
                FinalizeSearch(hasSpeech: true);
            else if (_searchElapsedMs >= SearchMaxMs)
                FinalizeSearch(hasSpeech: _searchSpeechStarted);
        }

        /// <summary>Enters search-capture mode => starts buffering audio for Whisper</summary>
        private void EnterSearchMode(){
            _searchBuffer?.Dispose();
            _searchBuffer = new MemoryStream();
            _searchSpeechStarted = false;
            _searchSilenceMs = 0;
            _searchElapsedMs = 0;
            Interlocked.Exchange(ref _searchFinalized, 0);

            // Prepend pre-roll so speech that started before/during the "listening" command is captured
            FlushPreRollToSearchBuffer();

            // Reset pre-roll so the next search starts clean
            _preRollHead = 0;
            _preRollFill = 0;

            // Compute speech threshold from measured ambient noise.
            // SNR ratio shrinks as ambient rises so speaker bleed doesn't block detection
            double snr = Math.Max(SpeechSnrMin, SpeechSnrBase - _ambientRms / SpeechSnrScale);
            _dynamicSpeechThreshold = Math.Max(_ambientRms * snr, _ambientRms + SpeechRmsFloor);
            AppLogger.Info($"VoiceListener: entered search-capture mode  ambient={_ambientRms:F1}  snr={snr:F2}  threshold={_dynamicSpeechThreshold:F1}");

            // Reset Vosk recognizer so partial-result cancel detection starts clean
            _recognizer.Reset();

            // Suspend the wake-window timer for the entire search session
            // (capture + Whisper inference can take 15+ s on slow CPUs).
            // It will be resumed in FinalizeSearch once the result is dispatched.
            _wakeTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            AppLogger.Debug("VoiceListener: wake timer suspended for search");

            _awaitingSearchQuery = true;
            SearchModeChanged?.Invoke(true);

            // Backstop in case audio callbacks stop arriving
            _searchTimeoutTimer?.Dispose();
            _searchTimeoutTimer = new System.Threading.Timer(_ => {
                if (_awaitingSearchQuery) FinalizeSearch(hasSpeech: _searchSpeechStarted);
            }, null, SearchMaxMs + 2_000, Timeout.Infinite);
        }

        /// <summary>Ends audio capture exactly once; either recognizes the buffer or cancels</summary>
        private void FinalizeSearch(bool hasSpeech){
            if (Interlocked.Exchange(ref _searchFinalized, 1) != 0) return;// already finalized

            _awaitingSearchQuery = false;
            _searchTimeoutTimer?.Dispose();
            _searchTimeoutTimer = null;

            if (!hasSpeech){
                AppLogger.Info("VoiceListener: search-capture - no speech detected");
                ResumeWakeTimer();
                SearchModeChanged?.Invoke(false);
                _searchBuffer?.Dispose();
                _searchBuffer = null;
                return;
            }

            _ = Task.Run(RecognizeSearchBuffer);
        }

        /// <summary>
        /// Runs Vosk on accumulated audio every 500 ms to detect "cancel"/"stop".
        /// Executes on a timer thread - keeps the audio callback free for capture
        /// </summary>
        private void RecognizeSearchBuffer(){
            try{
                var buffer = _searchBuffer;
                if (buffer == null) return;

                if (_whisper == null){
                    AppLogger.Warning("Search: Whisper not available");
                    SearchModeChanged?.Invoke(false);
                    return;
                }

                float[] samples = PcmToFloat(buffer.GetBuffer(), (int)buffer.Length);
                string  text = _whisper.TranscribeAsync(samples).GetAwaiter().GetResult();

                // Remove the initial keyword that may have been captured during the pre-roll recording
                text = Regex.Replace(text, @"^\s*(search|find)\s*[,.]?\s*", "", RegexOptions.IgnoreCase).Trim();

                AppLogger.Info($"Whisper search query: \"{text}\"");

                // Safety net: if Whisper transcribed the whole query as "cancel"/"stop"
                // (Vosk's partial-result detector missed it - usually because the word was spoken
                // during the "ok" TTS), treat it as a cancellation instead of a search query.
                string normalized = Regex.Replace(text, @"[^\w\s]", "").Trim();
                if (_cancelWords.Contains(normalized)){
                    AppLogger.Info($"Whisper transcribed cancel word \"{text}\" - treating as cancelled");
                    SearchModeChanged?.Invoke(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(text))
                    CommandRecognized?.Invoke("search", text);
                else
                    SearchModeChanged?.Invoke(false);
            }
            catch (Exception ex){
                AppLogger.Warning($"Search transcription failed: {ex.Message}");
                SearchModeChanged?.Invoke(false);
            }
            finally{
                // Resume the wake window after Whisper finishes (however long it took)
                ResumeWakeTimer();
                _searchBuffer?.Dispose();
                _searchBuffer = null;
            }
            //finally
            //{
            //}
        }

        /// <summary>Converts 16-bit PCM bytes to normalized float samples (-1..1) for Whisper</summary>
        private static float[] PcmToFloat(byte[] pcm, int byteCount){
            int n = byteCount / 2;
            var samples = new float[n];
            for (int i = 0; i < n; i++){
                short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }
            return samples;
        }

        /// <summary>Root-mean-square amplitude of 16-bit PCM samples in the buffer</summary>
        private static double Rms(byte[] buffer, int bytesRecorded){
            long sumSquares = 0;
            int  count = 0;
            for (int i = 0; i + 1 < bytesRecorded; i += 2){
                short s = (short)(buffer[i] | (buffer[i + 1] << 8));
                sumSquares += (long)s * s;
                count++;
            }
            return count == 0 ? 0 : Math.Sqrt((double)sumSquares / count);
        }

        /// <summary>Multiplies each int16 sample by <paramref name="gain"/> with clipping to int16 range</summary>
        private static void ApplyGain(byte[] buffer, int bytesRecorded, float gain){
            for (int i = 0; i + 1 < bytesRecorded; i += 2){
                short s = (short)(buffer[i] | (buffer[i + 1] << 8));
                int amplified = (int)(s * gain);
                if (amplified > short.MaxValue) amplified = short.MaxValue;
                else if (amplified < short.MinValue) amplified = short.MinValue;
                buffer[i] = (byte)(amplified & 0xFF);
                buffer[i + 1] = (byte)((amplified >> 8) & 0xFF);
            }
        }

        private static string ParseVoskText(string json){
            try{
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("text", out var t) ? t.GetString()?.Trim() ?? "" : "";
            }
            catch { return ""; }
        }

        // PartialResult() returns {"partial":"cancel"} - different key from Result()
        private static string ParseVoskPartial(string json){
            try{
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("partial", out var t) ? t.GetString()?.Trim() ?? "" : "";
            }
            catch { return ""; }
        }

        private void ProcessResult(string json){
            string text = ParseVoskText(json);

            if (string.IsNullOrWhiteSpace(text)) return;

            AppLogger.Debug($"Vosk: \"{text}\"");

            if (FreeFormDebug){
                CommandRecognized?.Invoke(text, null);
                return;
            }

            // --- Wake-word check ----------------------------------------------------------------
            // "youtube" command always opens/resets the command window
            bool hasWakeWord = Regex.IsMatch(text, @"\byoutube\b", RegexOptions.IgnoreCase);
            if (hasWakeWord){
                OpenWakeWindow();

                // Remove "youtube" so the remainder can still be parsed as a command ("youtube play" => "play")
                text = Regex.Replace(text, @"\byoutube\b", "", RegexOptions.IgnoreCase).Trim(' ', ',', '.');
                if (string.IsNullOrWhiteSpace(text)) return; // standalone "youtube" - just open window
            }
            else if (!_wakeActive){
                // Gate is closed and no wake word - ignore everything
                AppLogger.Debug($"Vosk: gate closed, ignored \"{text}\"");
                return;
            }

            // --- Command dispatch ----------------------------------------------------------------
            string? matched = FindKeyword(text);
            if (matched != null){
                // Duplicate Suppression: discard commands that exactly duplicate the previous one during the cooldown period
                long now = Environment.TickCount64;
                if (matched == _lastCommand && (now - _lastCommandTick) < CommandDebounceMs){
                    AppLogger.Debug($"Vosk debounced repeat: \"{matched}\"");
                    return;
                }
                _lastCommand = matched;
                _lastCommandTick = now;

                // Each accepted command resets the wake window
                ResetWakeTimer();

                AppLogger.Info($"Vosk matched: \"{matched}\" in \"{text}\"");
                if (YouTubeButtonMap.IsSearchCommand(matched))
                    Task.Run(async () => { await Task.Delay(170); EnterSearchMode(); });
                else
                    CommandRecognized?.Invoke(matched, null);
            }
            else
                AppLogger.Debug($"Vosk: no keyword matched in \"{text}\"");
        }

        // --- Wake-window helpers ----------------------------------------------------------------

        private void OpenWakeWindow(){
            bool wasActive = _wakeActive;
            _wakeActive = true;
            ResetWakeTimer();
            if (!wasActive){
                AppLogger.Info("VoiceListener: wake window opened");
                WakeStateChanged?.Invoke(true);
            }
        }

        /// <summary>Resumes the wake window after search completes - gives the user WakeWindowMs
        /// to issue a follow-up command (e.g. "first") without repeating the wake word</summary>
        private void ResumeWakeTimer(){
            if (_wakeActive)
                _wakeTimer?.Change(WakeWindowMs, Timeout.Infinite);
            AppLogger.Debug("VoiceListener: wake timer resumed after search");
        }

        private void ResetWakeTimer(){
            _wakeTimer?.Change(WakeWindowMs, Timeout.Infinite);
            if (_wakeTimer == null){
                _wakeTimer = new System.Threading.Timer(_ =>{
                    _wakeActive = false;
                    AppLogger.Info("VoiceListener: wake-up window closed (timeout)");
                    WakeStateChanged?.Invoke(false);
                }, null, WakeWindowMs, Timeout.Infinite);
            }
        }

        /// <summary>Word-boundary match, longest keyword first</summary>
        private string? FindKeyword(string text){
            foreach (string kw in _keywords){
                string pattern = Regex.Escape(kw);
                if (Regex.IsMatch(text, $@"\b{pattern}\b", RegexOptions.IgnoreCase))
                    return kw;
            }
            return null;
        }

        public void Dispose(){
            _waveIn.Dispose(); // stop callbacks first so nothing writes to the channel
            _voskChannel.Writer.TryComplete();
            _voskCts.Cancel();
            try { _voskTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _voskCts.Dispose();

            _wakeTimer?.Dispose();
            _searchTimeoutTimer?.Dispose();
            _searchBuffer?.Dispose();
            _whisper?.Dispose();
            _recognizer.Dispose();
            _model.Dispose();
        }
    }
}