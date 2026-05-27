using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Rectangle = System.Drawing.Rectangle;
using Imaging = System.Drawing.Imaging;
using Point = System.Drawing.Point;

namespace YouTubeVoiceController{
    /// <summary>
    /// Grabs the primary screen and returns it as an ImageSharp image.
    /// Uses GDI+ only for the capture. Immediately converts to ImageSharp to avoid locking issues during inference
    /// </summary>
    static class ScreenCapture{
        public static Image<Rgb24> Capture(){
            var bounds = Screen.PrimaryScreen!.Bounds;

            using var bmp = new Bitmap(bounds.Width, bounds.Height, Imaging.PixelFormat.Format32bppArgb);
            using var gfx = Graphics.FromImage(bmp);
            gfx.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            return ToImageSharp(bmp);
        }

        private static unsafe Image<Rgb24> ToImageSharp(Bitmap bmp){
            int w = bmp.Width;
            int h = bmp.Height;
            var result = new Image<Rgb24>(w, h);

            var data = bmp.LockBits(new Rectangle(0, 0, w, h), Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb);

            try {
                result.ProcessPixelRows(accessor => {
                    for (int y = 0; y < h; y++) {
                        byte* srcRow = (byte*)data.Scan0 + y * data.Stride;
                        var dstRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < w; x++) {
                            // GDI BGRA => ImageSharp RGB
                            dstRow[x] = new Rgb24(srcRow[x * 4 + 2], // R
                            srcRow[x * 4 + 1], // G
                            srcRow[x * 4 + 0]); // B
                        }
                    }
                });
            } finally {
                bmp.UnlockBits(data);
            }

            return result;
        }
    }
}