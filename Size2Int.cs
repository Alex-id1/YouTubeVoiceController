namespace YouTubeVoiceController{
    public readonly struct Size2Int{
        public int Width { get; }
        public int Height { get; }
        public static readonly Size2Int Zero = new(0, 0);

        public Size2Int(int width, int height) {
            Width = width;
            Height = height;
        }

        public override string ToString() => $"{Width}x{Height}";
    }
}