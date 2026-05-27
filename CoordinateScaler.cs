using System.Runtime.CompilerServices;

namespace YouTubeVoiceController{
    /// <summary>
    /// Maps YOLO bounding boxes (in model/canvas space) back to screen pixel coordinates.
    /// Mirrors the letterbox/tile geometry applied in TileHelper and ScreenCapture
    /// </summary>
    static class CoordinateScaler{
        /// <summary>
        /// Scale a detection from model-canvas space to screen coordinates.
        /// </summary>
        /// <param name="detX/Y/W/H">Bounding box in model-canvas pixels (already with tile offset applied).</param>
        /// <param name="screenSize">Actual captured screen resolution.</param>
        /// <param name="modelSize">Model input size (square, e.g. 640x640).</param>
        /// <param name="imgType">How the screenshot was fitted into the canvas</param>
        public static Rectangle ToScreenRect(float detX, float detY, float detW, float detH, Size2Int screenSize, Size2Int modelSize, ImageType imgType){
            var (sx, sy) = GetScale(screenSize, modelSize, imgType);
            return new Rectangle((int)(detX * sx), (int)(detY * sy), (int)(detW * sx), (int)(detH * sy));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (float scaleX, float scaleY) GetScale(Size2Int screen, Size2Int model, ImageType imgType){
            switch (imgType) {
                case ImageType.SQUARE: {
                    float biggest = MathF.Max(screen.Width, screen.Height);
                    float k = biggest / model.Width;
                    return (k, k);
                }
                case ImageType.VERTICAL: {
                    float aspect = (float)screen.Height / screen.Width;
                    float xk = (float)screen.Width  / model.Width;
                    float yk = (float)screen.Height / (model.Height * 2);
                    // aspect > 2 => very tall phone: scale both axes by y-scale
                    return aspect > 2 ? (yk, yk) : (xk, xk);
                }
                case ImageType.HORIZONTAL: {
                    float aspect = (float)screen.Width / screen.Height;
                    float xk = (float)screen.Width  / (model.Width * 2);
                    float yk = (float)screen.Height / model.Height;
                    // aspect > 2 => very wide: scale both axes by x-scale
                    return aspect > 2 ? (xk, xk) : (yk, yk);
                }
                case ImageType.GRID_4x2: {
                    // Canvas is 4xmodelW x 2xmodelH; source resized keeping aspect (h = 2*modelH)
                    // Both axes share the same scale since aspect ratio is preserved.
                    float k = (float)screen.Height / (model.Height * 2);
                    return (k, k);
                }
                default: {
                    float n = (float)screen.Width / model.Width;
                    return (n, n);
                }
            }
        }
    }
}