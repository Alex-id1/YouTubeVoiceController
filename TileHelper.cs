using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Point = SixLabors.ImageSharp.Point;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace YouTubeVoiceController
{
    /// <summary>
    /// Splits an arbitrary-size screenshot into model-sized tiles, preserving aspect ratio and padding with black
    /// </summary>
    static class TileHelper{
        /// <summary>
        /// Splits source to 8 tiles arranged in a 4x2 grid (4 columns, 2 rows).
        /// Canvas size: modelSize x4 wide, modelSize x2 tall.
        /// Tile order in the returned list: row-major top-to-bottom, left-to-right
        /// (t0...t3 = top row, t4...t7 = bottom row)
        /// </summary>
        public static List<TileInfo> SplitTo8Tiles(Image<Rgb24> source, int modelSize){
            int canvasW = modelSize * 4; // e.g. 1024 for modelSize = 256
            int canvasH = modelSize * 2; // e.g. 512

            // Resize preserving aspect ratio so height == canvasH
            float scale = (float)canvasH / source.Height;
            int rw = (int)(source.Width * scale);
            int rh = canvasH;

            using var canvas  = new Image<Rgb24>(canvasW, canvasH); // black padding
            using var resized = source.Clone(ctx => ctx.Resize(rw, rh));
            canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(0, 0), 1f));

            var tiles = new List<TileInfo>(8);
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                    tiles.Add(Crop(canvas, col * modelSize, row * modelSize, modelSize, modelSize, ImageType.GRID_4x2));
            return tiles;
        }

        public static List<TileInfo> SplitTo2Tiles(Image<Rgb24> source, int modelSize){
            int w = source.Width;
            int h = source.Height;

            if (w >= h) {
                float aspect = (float)w / h;
                int rw, rh;
                if (aspect <= 2) { rh = modelSize; rw = (int)(rh * aspect); }
                else { rw = modelSize * 2; rh = (int)(rw / aspect); }

                using var resized = source.Clone(ctx => ctx.Resize(rw, rh));
                return Create2Tiles(resized, modelSize, ImageType.HORIZONTAL);
            } else {
                float aspect = (float)h / w;
                int rw, rh;
                if (aspect <= 2) { rw = modelSize; rh = (int)(rw * aspect); }
                else { rh = modelSize * 2; rw = (int)(rh / aspect); }

                using var resized = source.Clone(ctx => ctx.Resize(rw, rh));
                return Create2Tiles(resized, modelSize, ImageType.VERTICAL);
            }
        }

        private static List<TileInfo> Create2Tiles(Image<Rgb24> src, int tileSize, ImageType imgType){
            int canvasW = imgType is ImageType.VERTICAL ? tileSize : tileSize * 2;
            int canvasH = imgType is ImageType.VERTICAL ? tileSize * 2 : tileSize;

            using var canvas = new Image<Rgb24>(canvasW, canvasH);
            canvas.Mutate(ctx => ctx.DrawImage(src, new Point(0, 0), 1f));

            var tiles = new List<TileInfo>();
            if (imgType is ImageType.VERTICAL) {
                tiles.Add(Crop(canvas, 0, 0, tileSize, tileSize, imgType));
                tiles.Add(Crop(canvas, 0, tileSize, tileSize, tileSize, imgType));
            } else {
                tiles.Add(Crop(canvas, 0, 0, tileSize, tileSize, imgType));
                tiles.Add(Crop(canvas, tileSize, 0, tileSize, tileSize, imgType));
            }
            return tiles;
        }

        private static TileInfo Crop(Image<Rgb24> canvas, int ox, int oy, int w, int h, ImageType imgType) => new TileInfo {
                Image = canvas.Clone(ctx => ctx.Crop(new Rectangle(ox, oy, w, h))),
                Offset = new System.Drawing.Point(ox, oy),
                ImgType = imgType
            };
    }
}