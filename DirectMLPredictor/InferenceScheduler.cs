using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Channels;

namespace YouTubeVoiceController.DirectMLPredictor{
    /// <summary>
    /// Single-model inference queue with optional GPU/CPU fallback.
    /// Thread-safe: multiple callers can await PredictAsync concurrently.
    /// </summary>
    public sealed class InferenceScheduler{
        private readonly Channel<InferenceRequest> _queue;
        private ExecutionProviderMode _execMode;

        private YoloDirectMLPredictor? _gpuPredictor;
        private YoloDirectMLPredictor? _cpuPredictor;
        private byte[]? _modelBytes;   // kept for lazy CPU fallback init

        private readonly SemaphoreSlim _gpuLock = new(1, 1);
        private readonly SemaphoreSlim _cpuLock = new(1, 1);

        private readonly CancellationTokenSource _cts = new();
        private Task _schedulerTask = Task.CompletedTask;

        public InferenceScheduler(string modelPath, ExecutionProviderMode execMode){
            _execMode = execMode;
            _queue = Channel.CreateUnbounded<InferenceRequest>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = false
            });
            LoadPredictors(modelPath);
        }

        private void LoadPredictors(string modelPath){
            if (!File.Exists(modelPath)) {
                AppLogger.Warning($"Model not found: {modelPath}");
                return;
            }
            try {
                _modelBytes = File.ReadAllBytes(modelPath);

                if (_execMode is ExecutionProviderMode.GPU_ONLY or ExecutionProviderMode.ALL) {
                    try {
                        _gpuPredictor = new YoloDirectMLPredictor(_modelBytes, ExecutionProvider.GPU);
                    } catch (Exception ex) {
                        // GPU unavailable on this machine - switch to CPU immediately and persist
                        AppLogger.Warning($"GPU predictor init failed: {ex.Message}. Falling back to CPU.");
                        _execMode = ExecutionProviderMode.CPU_ONLY;
                        AppSettings.PreferredExecMode = ExecutionProviderMode.CPU_ONLY;
                        AppSettings.Save();
                    }
                }

                if (_execMode is ExecutionProviderMode.CPU_ONLY or ExecutionProviderMode.ALL)
                    _cpuPredictor = new YoloDirectMLPredictor(_modelBytes, ExecutionProvider.CPU);

            } catch (Exception ex) {
                AppLogger.Error("Failed to load YOLO model", ex);
            }
        }

        public void Start() => _schedulerTask = Task.Run(SchedulerLoopAsync);

        public Task<IEnumerable<YoloPrediction>> PredictAsync(Image<Rgb24> image){
            var request = new InferenceRequest(image);
            if (!_queue.Writer.TryWrite(request))
                throw new InvalidOperationException("Inference queue is closed");
            return request.Tcs.Task;
        }

        public Size2Int GetInputSize(){
            var predictor = _gpuPredictor ?? _cpuPredictor;
            return predictor?.InputSize ?? Size2Int.Zero;
        }

        // --- Scheduler loop ---

        private async Task SchedulerLoopAsync()
        {
            try {
                await foreach (var req in _queue.Reader.ReadAllAsync(_cts.Token))
                    _ = ProcessRequestAsync(req);
            } catch (OperationCanceledException) { }
        }

        private async Task ProcessRequestAsync(InferenceRequest req)
        {
            try   { req.Tcs.SetResult(await RunInferenceAsync(req)); }
            catch (Exception ex) { req.Tcs.SetException(ex); }
        }

        private async Task<IEnumerable<YoloPrediction>> RunInferenceAsync(InferenceRequest req)
        {
            if (_execMode == ExecutionProviderMode.GPU_ONLY) {
                await _gpuLock.WaitAsync(_cts.Token);
                try {
                    return await _gpuPredictor!.PredictAsync(req.Image);
                } catch (Exception ex) {
                    AppLogger.Warning($"GPU inference failed, falling back to CPU: {ex.Message}");
                    FallbackToCpu();
                } finally {
                    _gpuLock.Release();
                }
                // Run on CPU after fallback
                await _cpuLock.WaitAsync(_cts.Token);
                try   { return await _cpuPredictor!.PredictAsync(req.Image); }
                finally { _cpuLock.Release(); }
            }

            if (_execMode == ExecutionProviderMode.CPU_ONLY) {
                await _cpuLock.WaitAsync(_cts.Token);
                try   { return await _cpuPredictor!.PredictAsync(req.Image); }
                finally { _cpuLock.Release(); }
            }

            // ExecutionProviderMode.ALL - non-blocking GPU first, then CPU, then wait
            while (true) {
                if (_gpuLock.Wait(0)) {
                    try   { return await _gpuPredictor!.PredictAsync(req.Image); }
                    finally { _gpuLock.Release(); }
                }
                if (_cpuLock.Wait(0)) {
                    try   { return await _cpuPredictor!.PredictAsync(req.Image); }
                    finally { _cpuLock.Release(); }
                }
                await Task.Delay(10, _cts.Token);
            }
        }

        /// <summary>
        /// Switches permanently to CPU mode on this machine: lazy-loads the CPU predictor,
        /// disposes the GPU predictor, and persists the preference to disk.
        /// </summary>
        private void FallbackToCpu()
        {
            if (_cpuPredictor == null && _modelBytes != null) {
                try {
                    _cpuPredictor = new YoloDirectMLPredictor(_modelBytes, ExecutionProvider.CPU);
                    AppLogger.Info("CPU predictor loaded for fallback");
                } catch (Exception ex) {
                    AppLogger.Error("Failed to load CPU predictor during fallback", ex);
                    throw;
                }
            }
            _execMode = ExecutionProviderMode.CPU_ONLY;
            _gpuPredictor?.Dispose();
            _gpuPredictor = null;

            AppSettings.PreferredExecMode = ExecutionProviderMode.CPU_ONLY;
            AppSettings.Save();
            AppLogger.Info("Switched to CPU_ONLY mode and saved preference");
        }

        // --- Dispose ---

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _queue.Writer.TryComplete();
            try { await _schedulerTask; } catch { }
            _gpuPredictor?.Dispose();
            _cpuPredictor?.Dispose();
            _gpuLock.Dispose();
            _cpuLock.Dispose();
            _cts.Dispose();
        }
    }
}