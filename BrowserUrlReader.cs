using Interop.UIAutomationClient;

namespace YouTubeVoiceController{
    /// <summary>
    /// Reads the URL of the active tab from a Chromium-based browser (Chrome, Edge...) using Windows UI Automation.
    /// Chromium exposes the address bar as an Edit element with AutomationId "omnibox"
    /// somewhere in the toolbar subtree. Firefox uses a different structure but has
    /// an edit field whose name starts with "Search" or contains the URL - we fall
    /// back to searching all edit controls in the toolbar for something that looks like a URL
    /// </summary>
    static class BrowserUrlReader{
        // Shared with BrowserTabSwitcher - creating CUIAutomationClass is expensive
        private static readonly IUIAutomation _automation = new CUIAutomationClass();

        /// <summary>
        /// Returns the URL currently shown in the address bar of <paramref name="hWnd"/>, or null if it could not be read
        /// </summary>
        public static string? ReadUrl(IntPtr hWnd){
            try{
                IUIAutomationElement root = _automation.ElementFromHandle(hWnd);
                if (root == null) return null;

                // ---- Strategy 1: Chromium "omnibox" (AutomationId) -----------------
                IUIAutomationCondition omniboxCond = _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AutomationIdPropertyId, "omnibox");

                IUIAutomationElement? omnibox = root.FindFirst(TreeScope.TreeScope_Descendants, omniboxCond);

                if (omnibox != null){
                    string? val = GetValue(omnibox);
                    if (!string.IsNullOrWhiteSpace(val)){
                        AppLogger.Debug($"BrowserUrlReader: omnibox => \"{val}\"");
                        return NormalizeUrl(val);
                    }
                }

                // ---- Strategy 2: any Edit in the toolbar that looks like a URL ------
                IUIAutomationCondition editCond = _automation.CreatePropertyCondition(
                    UIA_PropertyIds.UIA_ControlTypePropertyId,
                    UIA_ControlTypeIds.UIA_EditControlTypeId);

                IUIAutomationElementArray edits = root.FindAll(TreeScope.TreeScope_Descendants, editCond);

                for (int i = 0; i < edits.Length; i++){
                    string? val = GetValue(edits.GetElement(i));
                    if (LooksLikeUrl(val)){
                        AppLogger.Debug($"BrowserUrlReader: edit fallback => \"{val}\"");
                        return NormalizeUrl(val!);
                    }
                }

                AppLogger.Debug("BrowserUrlReader: could not locate address bar");
                return null;
            }catch (Exception ex){
                AppLogger.Debug($"BrowserUrlReader failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the URL of the first browser window that has an active YouTube tab, or null if none found
        /// </summary>
        public static string? ReadYouTubeUrl(){
            var hWnd = BrowserWindowFinder.FindYouTubeWindow();
            if (hWnd == null) return null;
            return ReadUrl(hWnd.Value);
        }

        // ---- Helpers --------------------------------------------------------------------

        private static string? GetValue(IUIAutomationElement elmnt){
            try{
                if (elmnt.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId) is IUIAutomationValuePattern vp)
                    return vp.CurrentValue;
            }catch { /* pattern not supported */ }
            return null;
        }

        private static bool LooksLikeUrl(string? s) => !string.IsNullOrWhiteSpace(s) &&
            (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             s.Contains("youtube.com", StringComparison.OrdinalIgnoreCase));

        /// <summary>Adds https:// if the browser omitted the scheme (Chrome does this for display)</summary>
        private static string NormalizeUrl(string url){
            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;
            return url;
        }
    }
}