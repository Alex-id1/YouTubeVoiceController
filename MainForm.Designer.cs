namespace YouTubeVoiceController
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Button       startStopBtn;
        private System.Windows.Forms.Button       helpBtn;
        private System.Windows.Forms.Label        statusLabel;
        private System.Windows.Forms.RichTextBox  logBox;
        private System.Windows.Forms.ProgressBar  loadingBar;
        private System.Windows.Forms.Label        apiKeyLabel;
        private System.Windows.Forms.TextBox      apiKeyInput;
        private System.Windows.Forms.Label        micGainLabel;
        private System.Windows.Forms.TrackBar     micGainSlider;
        private System.Windows.Forms.NotifyIcon       notifyIcon;
        private System.Windows.Forms.ContextMenuStrip trayMenu;
        private System.Windows.Forms.ToolStripMenuItem trayShowItem;
        private System.Windows.Forms.ToolStripMenuItem trayExitItem;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            startStopBtn = new Button();
            helpBtn = new Button();
            statusLabel = new Label();
            logBox = new RichTextBox();
            loadingBar = new ProgressBar();
            apiKeyLabel = new Label();
            apiKeyInput = new TextBox();
            micGainLabel = new Label();
            micGainSlider = new TrackBar();
            trayMenu = new ContextMenuStrip(components);
            trayShowItem = new ToolStripMenuItem();
            trayExitItem = new ToolStripMenuItem();
            notifyIcon = new NotifyIcon(components);
            pictureBox1 = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)micGainSlider).BeginInit();
            trayMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // startStopBtn
            // 
            startStopBtn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            startStopBtn.Location = new Point(69, 12);
            startStopBtn.Name = "startStopBtn";
            startStopBtn.Size = new Size(220, 34);
            startStopBtn.TabIndex = 3;
            startStopBtn.Text = "Start";
            startStopBtn.Click += StartStopBtn_Click;
            // 
            // helpBtn
            // 
            helpBtn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            helpBtn.Image = (Image)resources.GetObject("helpBtn.Image");
            helpBtn.Location = new Point(447, 12);
            helpBtn.Name = "helpBtn";
            helpBtn.Size = new Size(25, 25);
            helpBtn.TabIndex = 4;
            helpBtn.UseVisualStyleBackColor = true;
            helpBtn.Click += HelpBtn_Click;
            // 
            // statusLabel
            // 
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Location = new Point(13, 55);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(460, 18);
            statusLabel.TabIndex = 5;
            statusLabel.Text = "Stopped";
            // 
            // logBox
            // 
            logBox.BackColor = Color.Black;
            logBox.Font = new Font("Consolas", 9F);
            logBox.ForeColor = Color.LimeGreen;
            logBox.Location = new Point(12, 109);
            logBox.Name = "logBox";
            logBox.ReadOnly = true;
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            logBox.Size = new Size(460, 247);
            logBox.TabIndex = 16;
            logBox.Text = "";
            // 
            // loadingBar
            // 
            loadingBar.Location = new Point(13, 75);
            loadingBar.MarqueeAnimationSpeed = 25;
            loadingBar.Name = "loadingBar";
            loadingBar.Size = new Size(460, 6);
            loadingBar.Style = ProgressBarStyle.Marquee;
            loadingBar.TabIndex = 6;
            loadingBar.Visible = false;
            // 
            // apiKeyLabel
            // 
            apiKeyLabel.AutoSize = true;
            apiKeyLabel.Location = new Point(12, 120);
            apiKeyLabel.Name = "apiKeyLabel";
            apiKeyLabel.Size = new Size(66, 15);
            apiKeyLabel.TabIndex = 12;
            apiKeyLabel.Text = "YT API Key:";
            apiKeyLabel.Visible = false;
            // 
            // apiKeyInput
            // 
            apiKeyInput.Location = new Point(90, 117);
            apiKeyInput.Name = "apiKeyInput";
            apiKeyInput.PlaceholderText = "Paste YouTube Data API v3 key (optional)";
            apiKeyInput.Size = new Size(382, 23);
            apiKeyInput.TabIndex = 13;
            apiKeyInput.Visible = false;
            apiKeyInput.Leave += ApiKeyInput_Leave;
            // 
            // micGainLabel
            // 
            micGainLabel.AutoSize = true;
            micGainLabel.ForeColor = Color.DimGray;
            micGainLabel.Location = new Point(12, 88);
            micGainLabel.Name = "micGainLabel";
            micGainLabel.Size = new Size(71, 15);
            micGainLabel.TabIndex = 14;
            micGainLabel.Text = "Mic gain: 1x";
            // 
            // micGainSlider
            // 
            micGainSlider.AutoSize = false;
            micGainSlider.LargeChange = 1;
            micGainSlider.Location = new Point(91, 84);
            micGainSlider.Margin = new Padding(3, 0, 3, 0);
            micGainSlider.Maximum = 8;
            micGainSlider.Minimum = 1;
            micGainSlider.Name = "micGainSlider";
            micGainSlider.Size = new Size(300, 22);
            micGainSlider.TabIndex = 15;
            micGainSlider.TickStyle = TickStyle.None;
            micGainSlider.Value = 1;
            micGainSlider.Scroll += MicGainSlider_Scroll;
            // 
            // trayMenu
            // 
            trayMenu.Items.AddRange(new ToolStripItem[] { trayShowItem, trayExitItem });
            trayMenu.Name = "trayMenu";
            trayMenu.Size = new Size(104, 48);
            // 
            // trayShowItem
            // 
            trayShowItem.Name = "trayShowItem";
            trayShowItem.Size = new Size(103, 22);
            trayShowItem.Text = "Show";
            trayShowItem.Click += TrayShowItem_Click;
            // 
            // trayExitItem
            // 
            trayExitItem.Name = "trayExitItem";
            trayExitItem.Size = new Size(103, 22);
            trayExitItem.Text = "Exit";
            trayExitItem.Click += TrayExitItem_Click;
            // 
            // notifyIcon
            // 
            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.Icon = (Icon)resources.GetObject("notifyIcon.Icon");
            notifyIcon.Text = "YouTube Voice Controller";
            notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            // 
            // pictureBox1
            // 
            pictureBox1.BackgroundImage = (Image)resources.GetObject("pictureBox1.BackgroundImage");
            pictureBox1.BackgroundImageLayout = ImageLayout.Stretch;
            pictureBox1.InitialImage = null;
            pictureBox1.Location = new Point(13, 2);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(50, 50);
            pictureBox1.TabIndex = 17;
            pictureBox1.TabStop = false;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(484, 368);
            Controls.Add(pictureBox1);
            Controls.Add(startStopBtn);
            Controls.Add(helpBtn);
            Controls.Add(statusLabel);
            Controls.Add(loadingBar);
            Controls.Add(apiKeyLabel);
            Controls.Add(apiKeyInput);
            Controls.Add(micGainLabel);
            Controls.Add(micGainSlider);
            Controls.Add(logBox);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "MainForm";
            Text = "YouTube Voice Controller";
            ((System.ComponentModel.ISupportInitialize)micGainSlider).EndInit();
            trayMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        private PictureBox pictureBox1;
    }
}
