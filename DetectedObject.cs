namespace YouTubeVoiceController{
    public class DetectedObject{
        public int ClassId { get; }
        public string Name { get; }
        public float Score { get; }
        public Rectangle Bounds { get; }
        public int TileId { get; }   // -1 = not from tiled inference

        public DetectedObject(int classId, string name, float score, Rectangle bounds, int tileId = -1){
            ClassId = classId;
            Name = name;
            Score = score;
            Bounds = bounds;
            TileId = tileId;
        }

        // Centre point in screen coordinates
        public System.Drawing.Point Centre => new(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
    }
}