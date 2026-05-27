using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace YouTubeVoiceController.DirectMLPredictor{
    public class YoloDirectMLPredictor : IDisposable{
        private readonly InferenceSession _session;
        private bool _disposed;

        public readonly Size2Int InputSize;
        public readonly int ClassCount;
        public readonly Dictionary<int, string> ClassNames;

        public YoloDirectMLPredictor(byte[] model, ExecutionProvider provider){
            var opts = new SessionOptions();
            if (provider == ExecutionProvider.GPU)
                opts.AppendExecutionProvider_DML(0);
            else
                opts.AppendExecutionProvider_CPU();

            _session  = new InferenceSession(model, opts);
            ExtractModelParameters(out InputSize, out ClassCount);
            ClassNames = ExtractClassNames();
        }

        public Task<IEnumerable<YoloPrediction>> PredictAsync(Image<Rgb24> image) => Task.Run(() => Predict(image));

        private IEnumerable<YoloPrediction> Predict(Image<Rgb24> image, float confThreshold = AppSettings.YoloConfThreshold, float iouThreshold  = AppSettings.YoloIouThreshold){
            if (_disposed) throw new ObjectDisposedException(nameof(YoloDirectMLPredictor));

            float[] inputData = PreprocessImage(image);
            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 3, InputSize.Height, InputSize.Width });
            string inputName = _session.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            int numDetections = output.Dimensions[2];
            var candidates = new List<YoloPrediction>();

            for (int i = 0; i < numDetections; i++) {
                float x = output[0, 0, i];
                float y = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                for (int c = 0; c < ClassCount; c++) {
                    float conf = output[0, 4 + c, i];
                    if (conf < confThreshold) continue;

                    candidates.Add(new YoloPrediction {
                        X = x - w * 0.5f,
                        Y = y - h * 0.5f,
                        Width = w,
                        Height = h,
                        Confidence = conf,
                        ClassId = c,
                        ClassName = ClassNames[c]
                    });
                }
            }

            return ApplyNms(candidates, iouThreshold);
        }

        // --- NMS ---

        private static List<YoloPrediction> ApplyNms(List<YoloPrediction> boxes, float iouThreshold){
            var result = new List<YoloPrediction>();
            foreach (var group in boxes.GroupBy(b => b.ClassId)) {
                var sorted = group.OrderByDescending(b => b.Confidence).ToList();
                while (sorted.Count > 0) {
                    var best = sorted[0];
                    result.Add(best);
                    sorted.RemoveAt(0);
                    for (int i = sorted.Count - 1; i >= 0; i--)
                        if (ComputeIoU(best, sorted[i]) > iouThreshold)
                            sorted.RemoveAt(i);
                }
            }
            return result;
        }

        public static float ComputeIoU(YoloPrediction a, YoloPrediction b) => ComputeIoU(a.X, a.Y, a.Width, a.Height, b.X, b.Y, b.Width, b.Height);

        private static float ComputeIoU(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh){
            float x1 = Math.Max(ax, bx);
            float y1 = Math.Max(ay, by);
            float x2 = Math.Min(ax + aw, bx + bw);
            float y2 = Math.Min(ay + ah, by + bh);

            float interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float unionArea = aw * ah + bw * bh - interArea;
            return unionArea <= 0 ? 0 : interArea / unionArea;
        }

        // --- Model metadata ---

        private void ExtractModelParameters(out Size2Int inputSize, out int classes){
            var inputDims = _session.InputMetadata.First().Value.Dimensions;
            if (inputDims.Length >= 4)
                inputSize = new Size2Int(inputDims[3], inputDims[2]);
            else
                throw new InvalidOperationException($"Unexpected input shape: [{string.Join(", ", inputDims)}]");

            var outputDims = _session.OutputMetadata.First().Value.Dimensions;
            if (outputDims.Length >= 2) {
                classes = outputDims[1] - 4;
                if (classes <= 0)
                    throw new InvalidOperationException($"Invalid class count: {classes}");
            } else {
                throw new InvalidOperationException($"Unexpected output shape: [{string.Join(", ", outputDims)}]");
            }
        }

        private Dictionary<int, string> ExtractClassNames(){
            var meta = _session.ModelMetadata;
            if (meta.CustomMetadataMap.TryGetValue("names", out string? namesJson)) {
                var parsed = TryParsePythonDict(namesJson);
                if (parsed != null && parsed.Count == ClassCount)
                    return parsed.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            return Enumerable.Range(0, ClassCount).ToDictionary(i => i, i => $"class_{i}");
        }

        private static Dictionary<int, string>? TryParsePythonDict(string raw){
            raw = raw.Trim();
            if (!raw.StartsWith("{") || !raw.EndsWith("}")) return null;
            raw = raw[1..^1];

            var result = new Dictionary<int, string>();
            foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                var parts = entry.Split(':', 2);
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0].Trim(), out int key)) continue;
                var value = parts[1].Trim().Trim('\'', '"');
                result[key] = value;
            }
            return result.Count > 0 ? result : null;
        }

        // --- Image preprocessing ---

        private float[] PreprocessImage(Image<Rgb24> image){
            if (image.Width != InputSize.Width || image.Height != InputSize.Height)
                throw new ArgumentException("Image must match model input size.");

            int area   = InputSize.Width * InputSize.Height;
            float[] data = new float[3 * area];

            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++) {
                    var row      = accessor.GetRowSpan(y);
                    int rowOffset = y * InputSize.Width;
                    for (int x = 0; x < accessor.Width; x++) {
                        int offset = rowOffset + x;
                        data[0 * area + offset] = row[x].R / 255.0f;
                        data[1 * area + offset] = row[x].G / 255.0f;
                        data[2 * area + offset] = row[x].B / 255.0f;
                    }
                }
            });

            return data;
        }

        // --- Dispose ---

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed && disposing) {
                _session?.Dispose();
                _disposed = true;
            }
        }

        ~YoloDirectMLPredictor() => Dispose(false);
    }

    public class YoloPrediction{
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; }
        public required string ClassName { get; set; }
    }
}