using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YouTubeVoiceController{
    /// <summary>
    /// Enumerates top-level windows looking for a known browser process
    /// whose title contains "YouTube". Lets us focus that window before dispatching a keyboard shortcut
    /// </summary>
    static class BrowserWindowFinder
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        private const int SW_RESTORE = 9;

        // Process names (without .exe) of supported browsers.
        private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase){
                "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "browser"
            };

        /// <summary>Returns every visible top-level window that belongs to a known browser process</summary>
        public static List<IntPtr> FindAllBrowserWindows(){
            var result = new List<IntPtr>();
            EnumWindows((hWnd, _) => {
                if (!IsWindowVisible(hWnd)) return true;
                if (GetWindowTextLength(hWnd) == 0) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                try{
                    using var proc = Process.GetProcessById((int)pid);
                    if (BrowserProcesses.Contains(proc.ProcessName))
                        result.Add(hWnd);
                }
                catch { /* process exited */ }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        /// <summary>Finds the first visible browser window whose title contains "YouTube". Null if none</summary>
        public static IntPtr? FindYouTubeWindow(){
            IntPtr found = IntPtr.Zero;

            EnumWindows((hWnd, _) => {
                if (!IsWindowVisible(hWnd)) return true;

                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;

                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (title.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) < 0) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                try{
                    using var proc = Process.GetProcessById((int)pid);
                    if (BrowserProcesses.Contains(proc.ProcessName)){
                        found = hWnd;
                        AppLogger.Debug($"BrowserWindowFinder: matched '{title}' ({proc.ProcessName})");
                        return false; // stop enumeration
                    }
                }catch { /* process exited */ }

                return true;
            }, IntPtr.Zero);

            return found == IntPtr.Zero ? null : found;
        }

        /// <summary>
        /// Reliably brings a window to the foreground, bypassing Windows foreground lock.
        /// Uses the AttachThreadInput trick: temporarily attach our thread's input to the currently-foreground thread,
        /// which lifts the restriction on SetForegroundWindow
        /// </summary>
        public static void Activate(IntPtr hWnd){
            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);

            IntPtr currentForeground = GetForegroundWindow();
            if (currentForeground == hWnd) return; // already foreground

            uint foregroundThreadId = GetWindowThreadProcessId(currentForeground, out _);
            uint currentThreadId = GetCurrentThreadId();

            bool attached = false;
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                attached = AttachThreadInput(foregroundThreadId, currentThreadId, true);

            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);

            if (attached)
                AttachThreadInput(foregroundThreadId, currentThreadId, false);
        }
    }
}