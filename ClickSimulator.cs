using System.Runtime.InteropServices;

namespace YouTubeVoiceController{
    /// <summary>
    /// Simulates a mouse click at absolute screen coordinates via SendInput (Win32).
    /// Does NOT move the visible cursor - uses MOUSEEVENTF_MOVE for positioning
    /// followed by left-down/up to keep it clean
    /// </summary>
    static class ClickSimulator{
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public static void Click(int screenX, int screenY){
            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            // SendInput absolute coords are in the range [0, 65535]
            int absX = (int)((screenX + 0.5) * 65535 / screenW);
            int absY = (int)((screenY + 0.5) * 65535 / screenH);

            var inputs = new INPUT[] {
                MoveInput(absX, absY),
                ButtonInput(MOUSEEVENTF_LEFTDOWN),
                ButtonInput(MOUSEEVENTF_LEFTUP)
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            AppLogger.Debug($"Click sent: ({screenX}, {screenY})");
        }

        public static void Click(System.Drawing.Point pt) => Click(pt.X, pt.Y);

        /// <summary>
        /// Drag-click: mousedown => tiny move => mouseup. he browser sees this as a drag, NOT a click, so click handlers
        /// (e.g. YouTube's play/pause toggle on the video) don't fire. But the mousedown still moves DOM focus to the clicked element -
        /// perfect for "focus the video player without changing playback state"
        /// </summary>
        public static void ClickToFocus(int screenX, int screenY, int dragPixels = 6){
            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);

            int absX1 = (int)((screenX + 0.5) * 65535 / screenW);
            int absY1 = (int)((screenY + 0.5) * 65535 / screenH);
            int absX2 = (int)((screenX + dragPixels + 0.5)* 65535 / screenW);

            var inputs = new INPUT[] {
                MoveInput(absX1, absY1),
                ButtonInput(MOUSEEVENTF_LEFTDOWN),
                MoveInput(absX2, absY1), // small horizontal drag - breaks click detection
                ButtonInput(MOUSEEVENTF_LEFTUP)
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            AppLogger.Debug($"ClickToFocus (drag): ({screenX}, {screenY}) => +{dragPixels}px");
        }

        public static void ClickToFocus(System.Drawing.Point pt) => ClickToFocus(pt.X, pt.Y);

        // --- Win32 structs ---

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private static INPUT MoveInput(int absX, int absY) => new INPUT {
            type = 0, // INPUT_MOUSE
            mi = new MOUSEINPUT {
                dx = absX,
                dy = absY,
                mouseData = 0,
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        private static INPUT ButtonInput(uint flags) => new INPUT {
            type = 0,
            mi = new MOUSEINPUT { dwFlags = flags, dwExtraInfo = IntPtr.Zero }
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public MOUSEINPUT mi; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT {
            public int dx, dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}