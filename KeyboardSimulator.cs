using System.Runtime.InteropServices;

namespace YouTubeVoiceController{
    /// <summary>
    /// Simulates keyboard input via SendInput (Win32)
    /// </summary>
    static class KeyboardSimulator{
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public static void PressKey(VirtualKey key){
            var inputs = new[] { KeyDown(key), KeyUp(key) };
            DispatchInput(inputs);
        }

        /// <summary>Press a key combination (Ctrl/Shift/Alt + key)</summary>
        public static void PressKey(KeyCombo combo){
            var inputs = new List<INPUT>(8);
            if (combo.Ctrl) inputs.Add(KeyDown(VirtualKey.Control));
            if (combo.Shift) inputs.Add(KeyDown(VirtualKey.Shift));
            if (combo.Alt) inputs.Add(KeyDown(VirtualKey.Alt));
            inputs.Add(KeyDown(combo.Key));
            inputs.Add(KeyUp(combo.Key));
            if (combo.Alt) inputs.Add(KeyUp(VirtualKey.Alt));
            if (combo.Shift) inputs.Add(KeyUp(VirtualKey.Shift));
            if (combo.Ctrl) inputs.Add(KeyUp(VirtualKey.Control));
            DispatchInput(inputs.ToArray());
        }

        /// <summary>
        /// Wraps SendInput with diagnostic logging. SendInput returns the number of events successfully inserted into the input stream.
        /// If less than expected, GetLastError typically returns ERROR_ACCESS_DENIED (5) - meaning UIPI
        /// blocked us (target app is running at a higher integrity level than us)
        /// </summary>
        private static void DispatchInput(INPUT[] inputs){
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != inputs.Length){
                int err = Marshal.GetLastWin32Error();
                AppLogger.Warning(
                    $"SendInput injected {sent}/{inputs.Length} events. " +
                    $"Win32Error={err} ({(err == 5 ? "ACCESS_DENIED - UIPI blocked, target app likely elevated" : "see Win32 docs")})");
            }
        }

        public static void TypeText(string text){
            if (string.IsNullOrEmpty(text)) return;

            var inputs = new List<INPUT>();
            foreach (char c in text) {
                inputs.Add(CharDown(c));
                inputs.Add(CharUp(c));
            }
            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        }

        public static void SelectAll() => PressKey(new KeyCombo(VirtualKey.A, ctrl: true));

        // --- helpers ---
        private static INPUT KeyDown(VirtualKey key) => new INPUT {
            type = 1, // INPUT_KEYBOARD
            U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key } }
        };

        private static INPUT KeyUp(VirtualKey key) => new INPUT {
            type = 1,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = 0x0002 } } // KEYEVENTF_KEYUP
        };

        private static INPUT CharDown(char c) => new INPUT {
            type = 1,
            U = new InputUnion { ki = new KEYBDINPUT { wScan = c, dwFlags = 0x0004 } } // KEYEVENTF_UNICODE
        };

        private static INPUT CharUp(char c) => new INPUT {
            type = 1,
            U = new InputUnion { ki = new KEYBDINPUT { wScan = c, dwFlags = 0x0004 | 0x0002 } }
        };

        // --- Win32 structs ---
        // INPUT is a union in Win32: { type; union { MOUSEINPUT, KEYBDINPUT, HARDWAREINPUT } }.
        // We must declare it as Explicit layout with FieldOffset so its size matches what SendInput expects (40 bytes on x64),
        // otherwise SendInput fails with ERROR_INVALID_PARAMETER (87) and silently drops every event

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT{
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT{
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
    }

    /// <summary>Key + modifiers, used for keyboard shortcuts</summary>
    public readonly struct KeyCombo{
        public VirtualKey Key { get; }
        public bool Ctrl{ get; }
        public bool Shift{ get; }
        public bool Alt{ get; }

        public KeyCombo(VirtualKey key, bool ctrl = false, bool shift = false, bool alt = false)
        { Key = key; Ctrl = ctrl; Shift = shift; Alt = alt; }

        public override string ToString(){
            var parts = new List<string>();
            if (Ctrl)parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt)parts.Add("Alt");
            parts.Add(Key.ToString());
            return string.Join("+", parts);
        }
    }

    public enum VirtualKey : ushort{
        Enter = 0x0D,
        Zero = 0x30,
        Escape = 0x1B,
        Shift = 0x10,
        Control = 0x11,
        Alt = 0x12, // VK_MENU
        Space = 0x20,
        ArrowLeft = 0x25,
        ArrowUp = 0x26,
        ArrowRight= 0x27,
        ArrowDown = 0x28,
        A = 0x41,
        C = 0x43,
        F = 0x46,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        Slash = 0xBF, // '/' - focus YouTube search
    }
}