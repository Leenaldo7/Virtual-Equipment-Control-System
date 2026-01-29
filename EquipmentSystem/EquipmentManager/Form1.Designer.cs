namespace EquipmentManager
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private Button btnConnect;
        private Button btnHello;   // 이건 STATUS로 재활용
        private Button btnStart;
        private Button btnStop;



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
            btnConnect = new Button();
            btnHello = new Button();
            btnDisconnect = new Button();
            btnStart = new Button();
            btnStop = new Button();

            SuspendLayout();
            // 
            // txtLog
            // 
            txtLog.Dock = DockStyle.Fill;
            txtLog.Location = new Point(0, 0);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(800, 450);
            txtLog.TabIndex = 0;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(8, 369);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(75, 23);
            btnConnect.TabIndex = 1;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // btnHello
            // 
            btnHello.Location = new Point(8, 398);
            btnHello.Name = "btnHello";
            btnHello.Size = new Size(75, 23);
            btnHello.TabIndex = 2;
            btnHello.Text = "STATUS";
            btnHello.UseVisualStyleBackColor = true;
            btnHello.Click += btnHello_Click_1;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(89, 398);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(75, 23);
            btnStart.TabIndex = 4;
            btnStart.Text = "START";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(170, 398);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(75, 23);
            btnStop.TabIndex = 5;
            btnStop.Text = "STOP";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // btnDisconnect
            // 
            btnDisconnect.Location = new Point(8, 427);
            btnDisconnect.Name = "btnDisconnect";
            btnDisconnect.Size = new Size(75, 23);
            btnDisconnect.TabIndex = 3;
            btnDisconnect.Text = "Disconnect";
            btnDisconnect.UseVisualStyleBackColor = true;
            btnDisconnect.Click += bunDisconnect_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(btnDisconnect);
            Controls.Add(btnHello);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Controls.Add(btnConnect);
            Controls.Add(txtLog);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtLog;
        private Button btnDisconnect;
    }
}
