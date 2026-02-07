namespace VirtualEquipment
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtLog;


        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            txtLog = new TextBox();
            btnStart = new Button();
            btnStop = new Button();
            panelBottom = new Panel();

            SuspendLayout();

            // txtLog
            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Name = "txtLog";
            txtLog.TabIndex = 0;

            // panelBottom
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Height = 90;
            panelBottom.Name = "panelBottom";

            // btnStart
            btnStart.Location = new Point(12, 12);
            btnStart.Size = new Size(110, 28);
            btnStart.Name = "btnStart";
            btnStart.Text = "Start Server";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;

            // btnStop
            btnStop.Location = new Point(12, 46);
            btnStop.Size = new Size(110, 28);
            btnStop.Name = "btnStop";
            btnStop.Text = "Stop Server";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click_1;

            panelBottom.Controls.Add(btnStart);
            panelBottom.Controls.Add(btnStop);

            // Form1
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            MinimumSize = new Size(800, 450);
            Name = "Form1";
            Text = "VirtualEquipment";
            Load += Form1_Load;

            Controls.Clear();
            Controls.Add(panelBottom); // Bottom 먼저
            Controls.Add(txtLog);      // Fill 나중에

            ResumeLayout(false);
        }

        #endregion

        private Button btnStart;
        private Button btnStop;
        private Panel panelBottom;
    }
}
