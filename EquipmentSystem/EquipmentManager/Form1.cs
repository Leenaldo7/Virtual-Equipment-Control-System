using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EquipmentManager
{
    public partial class Form1 : Form
    {
        private TcpClient? _client;
        private NetworkStream? _ns;
        private Thread? _recvThread;
        private volatile bool _connected;

        private readonly StxEtxFramer _framer = new StxEtxFramer();
        private readonly byte[] _recvBuf = new byte[4096];

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

                _ns = _client.GetStream();
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

        // 기존 HELLO 버튼: 프로토콜 메시지로 보내기 (예: STATUS)
        private void btnHello_Click(object sender, EventArgs e)
        {
            TrySend("STATUS");
        }

        private void SendFrame(string body)
        {
            if (_ns == null) throw new InvalidOperationException("Not connected.");

            // STX + UTF8(body) + ETX
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var packet = new byte[bodyBytes.Length + 2];
            packet[0] = StxEtxFramer.STX;
            Buffer.BlockCopy(bodyBytes, 0, packet, 1, bodyBytes.Length);
            packet[^1] = StxEtxFramer.ETX;

            _ns.Write(packet, 0, packet.Length);
        }

        private void RecvLoop()
        {
            try
            {
                while (_connected && _ns != null)
                {
                    int n;
                    try
                    {
                        n = _ns.Read(_recvBuf, 0, _recvBuf.Length); // BLOCKING
                    }
                    catch
                    {
                        break;
                    }

                    if (n <= 0) break;

                    var frames = _framer.Feed(_recvBuf.AsSpan(0, n), out var warn);
                    if (warn != null) Log($"[CLIENT] Framer warn: {warn}");

                    foreach (var bodyBytes in frames)
                    {
                        var body = Encoding.UTF8.GetString(bodyBytes);
                        Log($"[CLIENT] Body: {body}");

                        if (PacketParser.TryParse(body, out var pkt, out var err))
                        {
                            Log($"[CLIENT] Packet OK: {pkt}");
                            // TODO: 여기서 switch(pkt.Command)로 UI/상태 처리
                        }
                        else
                        {
                            Log($"[CLIENT] Packet FAIL: {err}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Receive error: {ex.Message}");
            }
            finally
            {
                Disconnect("RecvLoop ended");
            }
        }

        private void Disconnect(string reason)
        {
            if (!_connected && _client == null && _ns == null)
                return;

            Log($"[CLIENT] Disconnecting... ({reason})");
            _connected = false;

            try { _client?.Close(); } catch { }

            Cleanup();
            UpdateUi();
            Log("[CLIENT] Disconnected.");
        }

        private void Cleanup()
        {
            _connected = false;
            _ns = null;
            _client = null;
            _framer.Reset();
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
            btnStart.Enabled = _connected;
            btnStop.Enabled = _connected;
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

        private void btnStatus_Click(object sender, EventArgs e)
        {
            TrySend("STATUS");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            // 일단 테스트는 하드코딩
            TrySend("START|A|100");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            TrySend("STOP");
        }
        private void TrySend(string body)
        {
            if (!_connected || _ns == null)
            {
                Log("[CLIENT] Not connected.");
                return;
            }

            try
            {
                SendFrame(body);
                Log($"[CLIENT] Sent frame: {body}");
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Send failed: {ex.Message}");
                Disconnect("Send failed");
            }
        }

    }
}
