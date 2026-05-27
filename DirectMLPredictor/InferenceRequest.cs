using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace YouTubeVoiceController.DirectMLPredictor{
    public sealed class InferenceRequest{
        public Image<Rgb24> Image { get; }
        public TaskCompletionSource<IEnumerable<YoloPrediction>> Tcs { get; }

        public InferenceRequest(Image<Rgb24> image) {
            Image = image;
            Tcs   = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}