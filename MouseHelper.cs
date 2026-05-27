using System.Runtime.InteropServices;

namespace YouTubeVoiceController{
    /// <summary>
    /// Mouse movement helpers (no click) + foreground-window geometry.
    /// Used to reveal YouTube player controls before taking a screenshot and to crop the capture to the active browser window
    /// </summary>
    static class MouseHelper{
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        /// <summary>
        /// Moves the cursor to the YouTube controls zone (bottom-centre of screen) and waits for the UI to appear.
        /// Call this before ScreenCapture.Capture()
        /// </summary>
        public static async Task RevealYouTubeControlsAsync(int delayMs = 350){
            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            // Bottom-centre -where YouTube progress bar and controls live
            int targetX = screenW / 2;
            int targetY = (int)(screenH * 0.88);

            // Short mouse movement(1px) to wake up the YouTube UI - guarantees a mousemove event
            var d = delayMs / 2;
            SetCursorPos(targetX, targetY);
            await Task.Delay(d);
            SetCursorPos(targetX + 1, targetY);
            await Task.Delay(d);
        }

        /// <summary>
        /// Shifts the cursor by <paramref name="pixels"/> in X from its current position.
        /// This is enough to fire a mousemove event and reveal hidden player UI in fullscreen
        /// </summary>
        public static void NudgeCursor(int pixels = 1){
            GetCursorPos(out var pt);
            SetCursorPos(pt.X + pixels, pt.Y);
        }

        /// <summary>
        /// Returns the current cursor position
        /// </summary>
        public static Point GetCursorPosition(){
            GetCursorPos(out var pt);
            return new Point(pt.X, pt.Y);
        }

        /// <summary>Sets cursor position back to a saved point (used after click-to-focus)</summary>
        public static void SetCursorPosition(Point pt) => SetCursorPos(pt.X, pt.Y);

        /// <summary>
        /// Returns the bounding rectangle of the current foreground window, clamped to the primary screen bounds.
        /// Falls back to the full screen if the window handle is invalid
        /// </summary>
        public static Rectangle GetForegroundWindowBounds(){
            var screen = Screen.PrimaryScreen!.Bounds;

            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return screen;

            if (!GetWindowRect(hwnd, out var r))
                return screen;

            // GetWindowRect includes window chrome/shadow on Win10+. Use as-is
            int x = Math.Max(r.Left, screen.Left);
            int y = Math.Max(r.Top, screen.Top);
            int x2 = Math.Min(r.Right, screen.Right);
            int y2 = Math.Min(r.Bottom, screen.Bottom);

            int w = x2 - x;
            int h = y2 - y;

            // Sanity check: if the result is too small => fall back to full screen
            if (w < 200 || h < 150)
                return screen;

            return new Rectangle(x, y, w, h);
        }
    }
}