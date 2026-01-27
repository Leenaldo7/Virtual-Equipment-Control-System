using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EquipmentManager
{
    public partial class Form1 : Form
    {
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private Thread? _recvThread;
        private volatile bool _connected;

        private const string Host = "127.0.0.1";
        private const int Port = 5000;

        public Form1()
        {
            InitializeComponent();
            UpdateUi();

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (_connected)
            {
                Log("[CLIENT] Already connected.");
                return;
            }

            try
            {
                _client = new TcpClient();
                Log("[CLIENT] Connecting... (Connect blocks)");
                _client.Connect(Host, Port); // BLOCKING
                Log("[CLIENT] Connected!");

                var ns = _client.GetStream();
                _reader = new StreamReader(ns, Encoding.UTF8);
                _writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

                _connected = true;

                _recvThread = new Thread(RecvLoop) { IsBackground = true };
                _recvThread.Start();

                UpdateUi();
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Connect failed: {ex.Message}");
                Cleanup();
                UpdateUi();
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            Disconnect("User requested");
        }

        private void btnHello_Click(object sender, EventArgs e)
        {
            if (!_connected || _writer == null)
            {
                Log("[CLIENT] Not connected.");
                return;
            }

            try
            {
                _writer.WriteLine("HELLO");
                Log("[CLIENT] Sent: HELLO");
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Send failed: {ex.Message}");
                Disconnect("Send failed");
            }
        }

        private void RecvLoop()
        {
            try
            {
                while (_connected && _reader != null)
                {
                    string? line;
                    try
                    {
                        line = _reader.ReadLine(); // BLOCKING
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (line == null) break;
                    Log($"[CLIENT] Received: {line}");
                }
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Receive error: {ex.Message}");
            }
            finally
            {
                // 여기로 왔다는 건 연결이 끊겼다는 의미
                Disconnect("RecvLoop ended");
            }
        }

        private void Disconnect(string reason)
        {
            if (!_connected && _client == null && _reader == null && _writer == null)
                return;

            Log($"[CLIENT] Disconnecting... ({reason})");
            _connected = false;

            // ReadLine 깨우기
            try { _client?.Close(); } catch { }

            Cleanup();
            UpdateUi();
            Log("[CLIENT] Disconnected.");
        }

        private void Cleanup()
        {
            _connected = false;
            _reader = null;
            _writer = null;
            _client = null;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect("Form closing");
        }

        private void UpdateUi()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateUi));
                return;
            }

            btnConnect.Enabled = !_connected;
            btnDisconnect.Enabled = _connected;
            btnHello.Enabled = _connected;
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke(new Action(() => Log(msg)));
                return;
            }
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} {msg}{Environment.NewLine}");
        }

        private void bunDisconnect_Click(object sender, EventArgs e)
        {
            Disconnect("User requested");
        }

        private void btnHello_Click_1(object sender, EventArgs e) => btnHello_Click(sender, e);
    }
}
