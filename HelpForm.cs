namespace YouTubeVoiceController
{
    /// <summary>
    /// Modal help dialog with two tabs:
    ///   1) Quick-start guide - how to use the app.
    ///   2) Full voice command reference, grouped by category
    /// </summary>
    sealed class HelpForm : Form{
        public HelpForm(){
            Text = "Help - YouTube Voice Controller";
            ClientSize = new Size(520, 480);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f);

            var tabs = new TabControl { Dock = DockStyle.Fill };

            tabs.TabPages.Add(BuildHowToTab());
            tabs.TabPages.Add(BuildCommandsTab());

            Controls.Add(tabs);
        }

        // --- Tab 1: How to use -----------------------------------------------------
        private static TabPage BuildHowToTab(){
            var page = new TabPage("How to use");
            var box = new RichTextBox{
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5f),
            };

            box.Rtf = BuildHowToRtf();
            page.Controls.Add(box);
            return page;
        }
        //\i Note: selecting results by number requires a YouTube Data API key (see README).\i0\line
        private static string BuildHowToRtf() {
            // RTF with bold headers and normal body text

            return @"{\rtf1\ansi\ansicpg1252\deff0
{\fonttbl{\f0 Segoe UI;}}
{\colortbl;\red0\green0\blue0;\red0\green100\blue0;\red180\green0\blue0;}
\f0\fs20
\b Wake word\b0\line
Say \b ""YouTube""\b0  to activate the 15-second command window.\line
The status indicator turns green while the command window is active.\line
Each accepted command automatically resets the 15-second timer,\line
so you can chain commands without repeating the wake word.\line
\line
\b Giving commands\b0\line
Speak clearly after the wake word. Examples:\line
\i ""YouTube > pause""\i0\line
\i ""YouTube > search""\i0  (then speak your search query)\line
Supported commands are listed in the \b Voice Commands\b0  tab.\line
\line
\b Voice search\b0\line
1. Say \b ""YouTube > search""\b0  (or \b ""find""\b0 ).\line
2. When you hear the confirmation beep, speak your query.\line
3. Results will appear in the log. Say \b ""first""\b0 , \b ""second""\b0 , etc. to open a video.\line
To cancel an active search, say \b ""stop""\b0  or \b ""cancel""\b0 .\line
\line
\b Mic gain slider\b0\line
If the app misses your commands, increase the mic gain slider.\line
Start at 1x (no boost) and raise gradually until recognition is reliable.\line
Too much gain may cause false triggers - find the sweet spot for your mic.\line
\line
\b Like / Dislike / Skip ad\b0\line
These commands use a custom offline AI (YOLO) model to locate the button on screen and click it.\line
Make sure the YouTube player is visible and not covered.\line
After the command, the app waits ~1.5 sec. for the player controls to appear\line
before taking a screenshot - this is normal.\line
\line
\b GPU / CPU inference\b0\line
The app uses your GPU by default for fast AI inference.\line
If your GPU is not supported, it will automatically fall back to CPU\line
and save this preference for future sessions.\line
\line
\b Tips\b0\line
\bullet  Speak at a natural pace - no need to shout or slow down.\line
\bullet  Reduce background noise for best recognition accuracy.\line
\bullet  The command window closes automatically after 15 seconds of silence.\line
\bullet  The app minimises to the system tray - double-click to restore.\line
}";
        }

        // --- Tab 2: Voice commands ------------------------------------------------
        private static TabPage BuildCommandsTab(){
            var page = new TabPage("Voice Commands");
            var box = new RichTextBox{
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5f),
            };

            box.Rtf = BuildCommandsRtf();
            page.Controls.Add(box);
            return page;
        }

        private static string BuildCommandsRtf(){
            // Section markers use plain ASCII ">" to avoid any encoding issues.
            // Separators between commands use \u183? (middle dot, U+00B7)
            return @"{\rtf1\ansi\ansicpg1252\deff0
{\fonttbl{\f0 Segoe UI;}}
{\colortbl;\red0\green0\blue0;\red40\green100\blue40;\red80\green80\blue80;}
\f0\fs20
\b\cf2 > Open YouTube\cf1\b0\line
open \u183? open youtube\line
\line
\b\cf2 > Playback\cf1\b0\line
play \u183? pause \u183? stop \u183? resume \u183? continue \u183? freeze \u183? hold\line
\line
\b\cf2 > Navigation\cf1\b0\line
next \u183? next video \u183? forward\line
back \u183? previous \u183? go back\line
rewind \u183? replay \u183? again \u183? repeat\line
\line
\b\cf2 > Volume\cf1\b0\line
louder \u183? volume up\line
quieter \u183? quiet \u183? volume down \u183? softer \u183? lower\line
\line
\b\cf2 > Mute\cf1\b0\line
mute \u183? unmute \u183? silence \u183? silent\line
sound off \u183? sound on \u183? volume off \u183? volume on\line
\line
\b\cf2 > Fullscreen\cf1\b0\line
fullscreen \u183? full screen \u183? screen\line
maximize \u183? expand \u183? minimize\line
\line
\b\cf2 > Subtitles\cf1\b0\line
subtitles \u183? captions\line
\line
\b\cf2 > Like / Dislike\cf1\b0\line
like \u183? thumbs up\line
dislike \u183? thumbs down\line
\line
\b\cf2 > Skip ad\cf1\b0\line
skip \u183? skip ad \u183? close ad \u183? skip this \u183? dismiss\line
\line
\b\cf2 > Search\cf1\b0\line
search \u183? find  \cf3\i (then speak your query)\cf1\i0\line
\line
\b\cf2 > Select search result\cf1\b0\line
first \u183? second \u183? third \u183? fourth \u183? fifth\line
sixth \u183? seventh \u183? eighth \u183? ninth \u183? tenth\line
\line
\b\cf2 > Cancel / Stop\cf1\b0\line
stop \u183? cancel  \cf3\i (cancels active search capture)\cf1\i0\line
}";
        }
    }
}