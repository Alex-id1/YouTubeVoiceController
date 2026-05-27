using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace YouTubeVoiceController{
    public class TileInfo : IDisposable{
        public required Image<Rgb24> Image;
        public System.Drawing.Point Offset; // tile origin in the stitched-canvas coordinate space
        public ImageType ImgType;

        public void Dispose() => Image?.Dispose();
    }
}