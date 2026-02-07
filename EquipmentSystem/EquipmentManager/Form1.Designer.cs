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
        private Button btnDisconnect;
        private Button btnForceErr;
        private Button btnReset;

        private Label lblConn;
        private Label lblState;
        private Label lblLastError;


        private RichTextBox txtLog;
        private Panel panelBottom;




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
            txtLog = new RichTextBox();
            btnConnect = new Button();
            btnHello = new Button();
            btnDisconnect = new Button();
            btnStart = new Button();
            btnStop = new Button();
            btnForceErr = new Button();
            btnReset = new Button();
            panelBottom = new Panel();

            SuspendLayout();
            // 
            // txtLog
            // 
            txtLog.Dock = DockStyle.Fill;
            txtLog.Location = new Point(0, 0);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtLog.Size = new Size(800, 450);
            txtLog.TabIndex = 0;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(8, 10);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(75, 23);
            btnConnect.TabIndex = 1;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // btnHello
            // 
            btnHello.Location = new Point(208, 10);
            btnHello.Name = "btnHello";
            btnHello.Size = new Size(75, 23);
            btnHello.TabIndex = 2;
            btnHello.Text = "STATUS";
            btnHello.UseVisualStyleBackColor = true;
            btnHello.Click += btnHello_Click_1;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(108, 10);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(75, 23);
            btnStart.TabIndex = 4;
            btnStart.Text = "START";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(108, 45);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(75, 23);
            btnStop.TabIndex = 5;
            btnStop.Text = "STOP";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // btnDisconnect
            // 
            btnDisconnect.Location = new Point(8, 45);
            btnDisconnect.Name = "btnDisconnect";
            btnDisconnect.Size = new Size(75, 23);
            btnDisconnect.TabIndex = 3;
            btnDisconnect.Text = "Disconnect";
            btnDisconnect.UseVisualStyleBackColor = true;
            btnDisconnect.Click += bunDisconnect_Click;

            // 
            // btnForceErr
            // 
            btnForceErr.Location = new Point(308, 10);
            btnForceErr.Name = "btnForceErr";
            btnForceErr.Size = new Size(95, 23);
            btnForceErr.TabIndex = 4;
            btnForceErr.Text = "FORCE ERR";
            btnForceErr.UseVisualStyleBackColor = true;
            btnForceErr.Click += btnForceErr_Click;

            // 
            // btnReset
            // 
            btnReset.Location = new Point(308, 45);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(95, 23);
            btnReset.TabIndex = 5;
            btnReset.Text = "RESET";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;

            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(txtLog);
            Controls.Add(btnConnect);
            Controls.Add(btnDisconnect);
            Controls.Add(btnHello);   
            Controls.Add(btnStart);
            Controls.Add(btnStop);
            Controls.Add(btnForceErr);
            Controls.Add(btnReset);
            Controls.Add(lblConn);
            Controls.Add(lblState);
            Controls.Add(lblLastError);

            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
            //
            // panelBottom
            //
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Height = 90;
            panelBottom.Name = "panelBottom";

            // 버튼은 panelBottom에
            panelBottom.Controls.Add(btnConnect);
            panelBottom.Controls.Add(btnHello);
            panelBottom.Controls.Add(btnDisconnect);
            panelBottom.Controls.Add(btnStart);
            panelBottom.Controls.Add(btnStop);
            panelBottom.Controls.Add(btnForceErr);
            panelBottom.Controls.Add(btnReset);

            // 폼에는 panelBottom 먼저, txtLog는 나중에(Fill이 남은 영역 먹게)
            Controls.Add(panelBottom);
            Controls.Add(txtLog);

            lblConn = new Label();
            lblState = new Label();
            lblLastError = new Label();

            // lblConn
            lblConn.AutoSize = true;
            lblConn.Location = new Point(120, 372);
            lblConn.Name = "lblConn";
            lblConn.Size = new Size(120, 15);
            lblConn.Text = "DISCONNECTED";

            // lblState
            lblState.AutoSize = true;
            lblState.Location = new Point(120, 398);
            lblState.Name = "lblState";
            lblState.Size = new Size(120, 15);
            lblState.Text = "STATE: UNKNOWN";

            // lblLastError
            lblLastError.AutoSize = true;
            lblLastError.Location = new Point(120, 424);
            lblLastError.Name = "lblLastError";
            lblLastError.Size = new Size(120, 15);
            lblLastError.Text = "ERR: NONE";


        }

        #endregion



    }
}
