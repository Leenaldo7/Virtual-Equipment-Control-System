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
            lblConn = new Label();
            lblState = new Label();
            lblLastError = new Label();
            lblTime = new Label();
            lblTemp = new Label();
            lblPressure = new Label();
            lblRpm = new Label();
            lblMode = new Label();
            lblSetValue = new Label();
            dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn3 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn4 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn5 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn6 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn7 = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvData).BeginInit();
            panelBottom.SuspendLayout();
            SuspendLayout();
            // 
            // txtLog
            // 
            txtLog.Dock = DockStyle.Fill;
            txtLog.Location = new Point(0, 0);
            txtLog.Margin = new Padding(4, 5, 4, 5);
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtLog.Size = new Size(418, 556);
            txtLog.TabIndex = 0;
            txtLog.Text = "";
            // 
            // splitMain
            // 
            splitMain.Dock = DockStyle.Fill;
            splitMain.Location = new Point(0, 0);
            splitMain.Margin = new Padding(4, 5, 4, 5);
            splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            splitMain.Panel1.Controls.Add(txtLog);
            // 
            // splitMain.Panel2
            // 
            splitMain.Panel2.Controls.Add(dgvData);
            splitMain.Size = new Size(1254, 556);
            splitMain.SplitterDistance = 418;
            splitMain.SplitterWidth = 9;
            splitMain.TabIndex = 0;
            // 
            // dgvData
            // 
            dgvData.AllowUserToAddRows = false;
            dgvData.AllowUserToDeleteRows = false;
            dgvData.ColumnHeadersHeight = 34;
            dgvData.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn1, dataGridViewTextBoxColumn2, dataGridViewTextBoxColumn3, dataGridViewTextBoxColumn4, dataGridViewTextBoxColumn5, dataGridViewTextBoxColumn6, dataGridViewTextBoxColumn7 });
            dgvData.Dock = DockStyle.Fill;
            dgvData.Location = new Point(0, 0);
            dgvData.Margin = new Padding(4, 5, 4, 5);
            dgvData.MultiSelect = false;
            dgvData.Name = "dgvData";
            dgvData.ReadOnly = true;
            dgvData.RowHeadersVisible = false;
            dgvData.RowHeadersWidth = 62;
            dgvData.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvData.Size = new Size(827, 556);
            dgvData.TabIndex = 0;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(11, 17);
            btnConnect.Margin = new Padding(4, 5, 4, 5);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(107, 38);
            btnConnect.TabIndex = 1;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // btnDisconnect
            // 
            btnDisconnect.Location = new Point(11, 75);
            btnDisconnect.Margin = new Padding(4, 5, 4, 5);
            btnDisconnect.Name = "btnDisconnect";
            btnDisconnect.Size = new Size(107, 38);
            btnDisconnect.TabIndex = 2;
            btnDisconnect.Text = "Disconnect";
            btnDisconnect.UseVisualStyleBackColor = true;
            btnDisconnect.Click += btnDisconnect_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(154, 17);
            btnStart.Margin = new Padding(4, 5, 4, 5);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(107, 38);
            btnStart.TabIndex = 3;
            btnStart.Text = "START";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(154, 75);
            btnStop.Margin = new Padding(4, 5, 4, 5);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(107, 38);
            btnStop.TabIndex = 4;
            btnStop.Text = "STOP";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // btnHello
            // 
            btnHello.Location = new Point(297, 17);
            btnHello.Margin = new Padding(4, 5, 4, 5);
            btnHello.Name = "btnHello";
            btnHello.Size = new Size(107, 38);
            btnHello.TabIndex = 5;
            btnHello.Text = "STATUS";
            btnHello.UseVisualStyleBackColor = true;
            btnHello.Click += btnHello_Click_1;
            // 
            // btnForceErr
            // 
            btnForceErr.Location = new Point(440, 17);
            btnForceErr.Margin = new Padding(4, 5, 4, 5);
            btnForceErr.Name = "btnForceErr";
            btnForceErr.Size = new Size(136, 38);
            btnForceErr.TabIndex = 6;
            btnForceErr.Text = "FORCE ERR";
            btnForceErr.UseVisualStyleBackColor = true;
            btnForceErr.Click += btnForceErr_Click;
            // 
            // btnReset
            // 
            btnReset.Location = new Point(440, 75);
            btnReset.Margin = new Padding(4, 5, 4, 5);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(136, 38);
            btnReset.TabIndex = 7;
            btnReset.Text = "RESET";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;
            // 
            // btnSimDrop
            // 
            btnSimDrop.Location = new Point(600, 17);
            btnSimDrop.Margin = new Padding(4, 5, 4, 5);
            btnSimDrop.Name = "btnSimDrop";
            btnSimDrop.Size = new Size(136, 38);
            btnSimDrop.TabIndex = 8;
            btnSimDrop.Text = "SIM DROP";
            btnSimDrop.UseVisualStyleBackColor = true;
            btnSimDrop.Click += btnSimDrop_Click;
            // 
            // panelBottom
            // 
            panelBottom.Controls.Add(btnSimDrop);
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
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Location = new Point(0, 556);
            panelBottom.Margin = new Padding(4, 5, 4, 5);
            panelBottom.Name = "panelBottom";
            panelBottom.Size = new Size(1254, 217);
            panelBottom.TabIndex = 1;
            // 
            // lblConn
            // 
            lblConn.AutoSize = true;
            lblConn.Location = new Point(743, 20);
            lblConn.Margin = new Padding(4, 0, 4, 0);
            lblConn.Name = "lblConn";
            lblConn.Size = new Size(145, 25);
            lblConn.TabIndex = 9;
            lblConn.Text = "DISCONNECTED";
            // 
            // lblState
            // 
            lblState.AutoSize = true;
            lblState.Location = new Point(743, 53);
            lblState.Margin = new Padding(4, 0, 4, 0);
            lblState.Name = "lblState";
            lblState.Size = new Size(170, 25);
            lblState.TabIndex = 10;
            lblState.Text = "STATE: UNKNOWN";
            // 
            // lblLastError
            // 
            lblLastError.AutoSize = true;
            lblLastError.Location = new Point(743, 87);
            lblLastError.Margin = new Padding(4, 0, 4, 0);
            lblLastError.Name = "lblLastError";
            lblLastError.Size = new Size(104, 25);
            lblLastError.TabIndex = 11;
            lblLastError.Text = "ERR: NONE";
            // 
            // lblTime
            // 
            lblTime.AutoSize = true;
            lblTime.Location = new Point(998, 17);
            lblTime.Margin = new Padding(4, 0, 4, 0);
            lblTime.Name = "lblTime";
            lblTime.Size = new Size(70, 25);
            lblTime.TabIndex = 12;
            lblTime.Text = "TIME: -";
            // 
            // lblTemp
            // 
            lblTemp.AutoSize = true;
            lblTemp.Location = new Point(998, 53);
            lblTemp.Margin = new Padding(4, 0, 4, 0);
            lblTemp.Name = "lblTemp";
            lblTemp.Size = new Size(75, 25);
            lblTemp.TabIndex = 13;
            lblTemp.Text = "TEMP: -";
            // 
            // lblPressure
            // 
            lblPressure.AutoSize = true;
            lblPressure.Location = new Point(998, 87);
            lblPressure.Margin = new Padding(4, 0, 4, 0);
            lblPressure.Name = "lblPressure";
            lblPressure.Size = new Size(79, 25);
            lblPressure.TabIndex = 14;
            lblPressure.Text = "PRESS: -";
            // 
            // lblRpm
            // 
            lblRpm.AutoSize = true;
            lblRpm.Location = new Point(998, 120);
            lblRpm.Margin = new Padding(4, 0, 4, 0);
            lblRpm.Name = "lblRpm";
            lblRpm.Size = new Size(67, 25);
            lblRpm.TabIndex = 15;
            lblRpm.Text = "RPM: -";
            // 
            // lblMode
            // 
            lblMode.AutoSize = true;
            lblMode.Location = new Point(743, 120);
            lblMode.Margin = new Padding(4, 0, 4, 0);
            lblMode.Name = "lblMode";
            lblMode.Size = new Size(82, 25);
            lblMode.TabIndex = 16;
            lblMode.Text = "MODE: -";
            // 
            // lblSetValue
            // 
            lblSetValue.AutoSize = true;
            lblSetValue.Location = new Point(743, 120);
            lblSetValue.Margin = new Padding(4, 0, 4, 0);
            lblSetValue.Name = "lblSetValue";
            lblSetValue.Size = new Size(58, 25);
            lblSetValue.TabIndex = 17;
            lblSetValue.Text = "SET: -";
            // 
            // dataGridViewTextBoxColumn1
            // 
            dataGridViewTextBoxColumn1.HeaderText = "Time";
            dataGridViewTextBoxColumn1.MinimumWidth = 8;
            dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            dataGridViewTextBoxColumn1.ReadOnly = true;
            dataGridViewTextBoxColumn1.Width = 150;
            // 
            // dataGridViewTextBoxColumn2
            // 
            dataGridViewTextBoxColumn2.HeaderText = "State";
            dataGridViewTextBoxColumn2.MinimumWidth = 8;
            dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            dataGridViewTextBoxColumn2.ReadOnly = true;
            dataGridViewTextBoxColumn2.Width = 150;
            // 
            // dataGridViewTextBoxColumn3
            // 
            dataGridViewTextBoxColumn3.HeaderText = "Temp";
            dataGridViewTextBoxColumn3.MinimumWidth = 8;
            dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            dataGridViewTextBoxColumn3.ReadOnly = true;
            dataGridViewTextBoxColumn3.Width = 150;
            // 
            // dataGridViewTextBoxColumn4
            // 
            dataGridViewTextBoxColumn4.HeaderText = "Press";
            dataGridViewTextBoxColumn4.MinimumWidth = 8;
            dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
            dataGridViewTextBoxColumn4.ReadOnly = true;
            dataGridViewTextBoxColumn4.Width = 150;
            // 
            // dataGridViewTextBoxColumn5
            // 
            dataGridViewTextBoxColumn5.HeaderText = "RPM";
            dataGridViewTextBoxColumn5.MinimumWidth = 8;
            dataGridViewTextBoxColumn5.Name = "dataGridViewTextBoxColumn5";
            dataGridViewTextBoxColumn5.ReadOnly = true;
            dataGridViewTextBoxColumn5.Width = 150;
            // 
            // dataGridViewTextBoxColumn6
            // 
            dataGridViewTextBoxColumn6.HeaderText = "Mode";
            dataGridViewTextBoxColumn6.MinimumWidth = 8;
            dataGridViewTextBoxColumn6.Name = "dataGridViewTextBoxColumn6";
            dataGridViewTextBoxColumn6.ReadOnly = true;
            dataGridViewTextBoxColumn6.Width = 150;
            // 
            // dataGridViewTextBoxColumn7
            // 
            dataGridViewTextBoxColumn7.HeaderText = "Set";
            dataGridViewTextBoxColumn7.MinimumWidth = 8;
            dataGridViewTextBoxColumn7.Name = "dataGridViewTextBoxColumn7";
            dataGridViewTextBoxColumn7.ReadOnly = true;
            dataGridViewTextBoxColumn7.Width = 150;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1254, 773);
            Controls.Add(splitMain);
            Controls.Add(panelBottom);
            Margin = new Padding(4, 5, 4, 5);
            MinimumSize = new Size(1276, 829);
            Name = "Form1";
            Text = "EquipmentManager";
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvData).EndInit();
            panelBottom.ResumeLayout(false);
            panelBottom.PerformLayout();
            ResumeLayout(false);
        }


        #endregion



        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn6;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn7;
    }
}
