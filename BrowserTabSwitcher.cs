using Interop.UIAutomationClient;

namespace YouTubeVoiceController{
    /// <summary>
    /// Switches the active tab inside a browser window via Windows UI Automation.
    /// WinAPI sees a browser as a single window, so it can't reach background
    /// tabs - but Chrome/Edge/Firefox expose their tab strip in the accessibility
    /// tree as TabItem elements named after the page title. We find the one containing "YouTube" and select it
    /// </summary>
    static class BrowserTabSwitcher{
        // A single shared automation object - creating it is relatively expensive
        private static readonly IUIAutomation _automation = new CUIAutomationClass();

        /// <summary>
        /// Looks for a tab whose title contains "YouTube" in the given browser window
        /// and selects it. Returns true if such a tab was found and activated.
        /// </summary>
        public static bool TrySelectYouTubeTab(IntPtr hWnd){
            try{
                IUIAutomationElement root = _automation.ElementFromHandle(hWnd);
                if (root == null) return false;

                IUIAutomationCondition tabCondition = _automation.CreatePropertyCondition(
                    UIA_PropertyIds.UIA_ControlTypePropertyId,
                    UIA_ControlTypeIds.UIA_TabItemControlTypeId);

                IUIAutomationElementArray tabs = root.FindAll(TreeScope.TreeScope_Descendants, tabCondition);

                for (int i = 0; i < tabs.Length; i++){
                    IUIAutomationElement tab = tabs.GetElement(i);
                    string name = tab.CurrentName ?? "";
                    if (name.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (SelectTab(tab)){
                        AppLogger.Info($"BrowserTabSwitcher: selected tab '{name}'");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex){
                AppLogger.Debug($"BrowserTabSwitcher failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Selects a tab via the SelectionItem pattern, falling back to Invoke</summary>
        private static bool SelectTab(IUIAutomationElement tab){
            if (tab.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) is IUIAutomationSelectionItemPattern sip){
                sip.Select();
                return true;
            }
            if (tab.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) is IUIAutomationInvokePattern inv){
                inv.Invoke();
                return true;
            }
            return false;
        }
    }
}