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
        private Button btnSimDrop;

        private Label lblConn;
        private Label lblState;
        private Label lblLastError;


        private RichTextBox txtLog;
        private Panel panelBottom;

        private Label lblTime;
        private Label lblTemp;
        private Label lblPressure;
        private Label lblRpm;
        private Label lblMode;
        private Label lblSetValue;

        private SplitContainer splitMain;
        private DataGridView dgvData;

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

            splitMain = new SplitContainer();
            dgvData = new DataGridView();

            btnConnect = new Button();
            btnDisconnect = new Button();
            btnStart = new Button();
            btnStop = new Button();
            btnHello = new Button();
            btnForceErr = new Button();
            btnReset = new Button();
            btnSimDrop = new Button();

            panelBottom = new Panel();

            lblMode = new Label();
            lblSetValue = new Label();

            lblConn = new Label();
            lblState = new Label();
            lblLastError = new Label();
            lblTime = new Label();
            lblTemp = new Label();
            lblPressure = new Label();
            lblRpm = new Label();

            SuspendLayout();

            // ===================== panelBottom =====================
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Height = 90;
            panelBottom.Name = "panelBottom";

            // btnConnect
            btnConnect.Location = new Point(8, 10);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(75, 23);
            btnConnect.TabIndex = 1;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;

            // btnDisconnect
            btnDisconnect.Location = new Point(8, 45);
            btnDisconnect.Name = "btnDisconnect";
            btnDisconnect.Size = new Size(75, 23);
            btnDisconnect.TabIndex = 2;
            btnDisconnect.Text = "Disconnect";
            btnDisconnect.UseVisualStyleBackColor = true;
            btnDisconnect.Click += bunDisconnect_Click;

            // btnStart
            btnStart.Location = new Point(108, 10);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(75, 23);
            btnStart.TabIndex = 3;
            btnStart.Text = "START";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;

            // btnStop
            btnStop.Location = new Point(108, 45);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(75, 23);
            btnStop.TabIndex = 4;
            btnStop.Text = "STOP";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;

            // btnHello (STATUS)
            btnHello.Location = new Point(208, 10);
            btnHello.Name = "btnHello";
            btnHello.Size = new Size(75, 23);
            btnHello.TabIndex = 5;
            btnHello.Text = "STATUS";
            btnHello.UseVisualStyleBackColor = true;
            btnHello.Click += btnHello_Click_1;

            // btnForceErr
            btnForceErr.Location = new Point(308, 10);
            btnForceErr.Name = "btnForceErr";
            btnForceErr.Size = new Size(95, 23);
            btnForceErr.TabIndex = 6;
            btnForceErr.Text = "FORCE ERR";
            btnForceErr.UseVisualStyleBackColor = true;
            btnForceErr.Click += btnForceErr_Click;

            // btnReset
            btnReset.Location = new Point(308, 45);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(95, 23);
            btnReset.TabIndex = 7;
            btnReset.Text = "RESET";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;

            // btnSimDrop (강제 끊김 시뮬)
            btnSimDrop.Location = new Point(420, 10);
            btnSimDrop.Name = "btnSimDrop";
            btnSimDrop.Size = new Size(95, 23);
            btnSimDrop.TabIndex = 8;
            btnSimDrop.Text = "SIM DROP";
            btnSimDrop.UseVisualStyleBackColor = true;
            btnSimDrop.Click += btnSimDrop_Click;

            // panelBottom에 추가 (다른 버튼들 추가하는 곳에 같이)
            panelBottom.Controls.Add(btnSimDrop);


            // 라벨들 (panelBottom 안에 배치)
            lblConn.AutoSize = true;
            lblConn.Location = new Point(440, 12);
            lblConn.Name = "lblConn";
            lblConn.Text = "DISCONNECTED";

            lblState.AutoSize = true;
            lblState.Location = new Point(440, 32);
            lblState.Name = "lblState";
            lblState.Text = "STATE: UNKNOWN";

            lblLastError.AutoSize = true;
            lblLastError.Location = new Point(440, 52);
            lblLastError.Name = "lblLastError";
            lblLastError.Text = "ERR: NONE";

            lblTime.AutoSize = true;
            lblTime.Location = new Point(620, 12);
            lblTime.Name = "lblTime";
            lblTime.Text = "TIME: -";

            lblTemp.AutoSize = true;
            lblTemp.Location = new Point(620, 32);
            lblTemp.Name = "lblTemp";
            lblTemp.Text = "TEMP: -";

            lblPressure.AutoSize = true;
            lblPressure.Location = new Point(620, 52);
            lblPressure.Name = "lblPressure";
            lblPressure.Text = "PRESS: -";

            lblRpm.AutoSize = true;
            lblRpm.Location = new Point(620, 72);
            lblRpm.Name = "lblRpm";
            lblRpm.Text = "RPM: -";

            lblMode.AutoSize = true;
            lblMode.Location = new Point(440, 72);
            lblMode.Name = "lblMode";
            lblMode.Text = "MODE: -";

            lblSetValue.AutoSize = true;
            lblSetValue.Location = new Point(520, 72);
            lblSetValue.Name = "lblSetValue";
            lblSetValue.Text = "SET: -";

            // ===================== txtLog =====================
            txtLog.Dock = DockStyle.Fill;
            txtLog.Location = new Point(0, 0);
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtLog.TabIndex = 0;

            // ===================== splitMain =====================
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;     // 좌/우 분할
            splitMain.Name = "splitMain";
            splitMain.SplitterWidth = 6;
            splitMain.IsSplitterFixed = false;

            // 왼쪽: txtLog
            txtLog.Dock = DockStyle.Fill;
            splitMain.Panel1.Controls.Add(txtLog);

            // 오른쪽: dgvData
            dgvData.Dock = DockStyle.Fill;
            dgvData.Name = "dgvData";
            dgvData.ReadOnly = true;
            dgvData.AllowUserToAddRows = false;
            dgvData.AllowUserToDeleteRows = false;
            dgvData.RowHeadersVisible = false;
            dgvData.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvData.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvData.ScrollBars = ScrollBars.Both;
            dgvData.MultiSelect = false;

            // 컬럼 추가
            dgvData.Columns.Add("colTime", "Time");
            dgvData.Columns.Add("colState", "State");
            dgvData.Columns.Add("colTemp", "Temp");
            dgvData.Columns.Add("colPress", "Press");
            dgvData.Columns.Add("colRpm", "RPM");
            dgvData.Columns.Add("colMode", "Mode");
            dgvData.Columns.Add("colSet", "Set");

            dgvData.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvData.Columns["colTime"].Width = 110;
            dgvData.Columns["colState"].Width = 70;
            dgvData.Columns["colTemp"].Width = 70;
            dgvData.Columns["colPress"].Width = 70;
            dgvData.Columns["colRpm"].Width = 80;
            dgvData.Columns["colMode"].Width = 60;
            dgvData.Columns["colSet"].Width = 60;

            splitMain.Panel2.Controls.Add(dgvData);

            // ===================== panelBottom에 컨트롤 추가 =====================
            panelBottom.Controls.Add(btnConnect);
            panelBottom.Controls.Add(btnDisconnect);
            panelBottom.Controls.Add(btnStart);
            panelBottom.Controls.Add(btnStop);
            panelBottom.Controls.Add(btnHello);
            panelBottom.Controls.Add(btnForceErr);
            panelBottom.Controls.Add(btnReset);

            panelBottom.Controls.Add(lblConn);
            panelBottom.Controls.Add(lblState);
            panelBottom.Controls.Add(lblLastError);
            panelBottom.Controls.Add(lblTime);
            panelBottom.Controls.Add(lblTemp);
            panelBottom.Controls.Add(lblPressure);
            panelBottom.Controls.Add(lblRpm);
            panelBottom.Controls.Add(lblMode);
            panelBottom.Controls.Add(lblSetValue);


            // ===================== Form =====================
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Name = "Form1";
            Text = "EquipmentManager";

            Controls.Add(splitMain);
            Controls.Add(panelBottom);

            ResumeLayout(false);
            PerformLayout();
        }


        #endregion



    }
}
