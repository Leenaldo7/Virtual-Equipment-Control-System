using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VirtualEquipment
{
    public partial class Form1 : Form
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private Thread? _serverThread;
        private volatile bool _running;
        private enum EquipState { Stopped, Running }
        private readonly object _stateLock = new();
        private EquipState _state = EquipState.Stopped;
        private string? _mode;
        private int _value;


        private const int Port = 5000;

        public Form1()
        {
            InitializeComponent();
            UpdateUi();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_running)
            {
                Log("[SERVER] Already running.");
                return;
            }

            _running = true;
            _serverThread = new Thread(ServerLoop) { IsBackground = true };
            _serverThread.Start();

            Log($"[SERVER] Start requested. Port={Port}");
            UpdateUi();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            if (!_running)
            {
                Log("[SERVER] Not running.");
                return;
            }

            Log("[SERVER] Stop requested...");
            _running = false;

            // Accept/ReadLine 깨우기
            try { _client?.Close(); } catch { }
            try { _listener?.Stop(); } catch { }

            _client = null;
            _listener = null;

            UpdateUi();
        }

        private void ServerLoop()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                Log("[SERVER] Listening... (Accept blocks)");

                while (_running)
                {
                    TcpClient? accepted = null;

                    try
                    {
                        accepted = _listener.AcceptTcpClient(); // BLOCKING
                    }
                    catch (SocketException)
                    {
                        // Stop()로 인해 Accept가 깨질 수 있음
                        if (!_running) break;
                        throw;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Stop()로 listener가 dispose되면 발생 가능
                        if (!_running) break;
                        throw;
                    }

                    var client = accepted;
                    Log("[SERVER] Client connected!");

                    var t = new Thread(() =>
                    {
                        try
                        {
                            HandleClient(client);
                        }
                        catch (Exception ex)
                        {
                            Log($"[SERVER] Client handler error: {ex.Message}");
                        }
                        finally
                        {
                            try { client.Close(); } catch { }
                            Log("[SERVER] Client disconnected.");
                        }
                    })
                    { IsBackground = true };

                    t.Start();
                }
            }
            catch (Exception ex)
            {
                Log($"[SERVER] ServerLoop error: {ex.Message}");
            }
            finally
            {
                try { _client?.Close(); } catch { }
                try { _listener?.Stop(); } catch { }
                _client = null;
                _listener = null;
                _running = false;
                Log("[SERVER] Stopped.");
                UpdateUi();
            }
        }

        private void HandleClient(TcpClient client)
        {
            using NetworkStream ns = client.GetStream();

            var framer = new StxEtxFramer();
            var recvBuf = new byte[4096];

            while (_running)
            {
                int n;
                try
                {
                    n = ns.Read(recvBuf, 0, recvBuf.Length); // BLOCKING
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (n <= 0) break;

                var frames = framer.Feed(recvBuf.AsSpan(0, n), out var warn);
                if (warn != null) Log($"[SERVER] Framer warn: {warn}");

                foreach (var bodyBytes in frames)
                {
                    var body = Encoding.UTF8.GetString(bodyBytes);
                    Log($"[SERVER] Body: {body}");

                    if (!PacketParser.TryParse(body, out var pkt, out var err))
                    {
                        Log($"[SERVER] Packet FAIL: {err}");
                        SendFrame(ns, $"ERR|{err}");
                        continue;
                    }

                    Log($"[SERVER] Packet OK: {pkt}");

                    // Command 처리
                    switch (pkt!.Command)
                    {
                        case "STATUS":
                            {
                                string resp;
                                lock (_stateLock)
                                {
                                    if (_state == EquipState.Stopped)
                                        resp = "ACK|STATUS|STOPPED";
                                    else
                                        resp = $"ACK|STATUS|RUNNING|{_mode}|{_value}";
                                }
                                SendFrame(ns, resp);
                                Log($"[SERVER] Sent: {resp}");
                                break;
                            }

                        case "START":
                            {
                                // START|A|100 (parser에서 이미 검증됨)
                                lock (_stateLock)
                                {
                                    _state = EquipState.Running;
                                    _mode = pkt.Params[0];
                                    _value = int.Parse(pkt.Params[1]);
                                }
                                SendFrame(ns, "ACK|START|RUNNING");
                                Log("[SERVER] Sent: ACK|START|RUNNING");
                                break;
                            }

                        case "STOP":
                            {
                                lock (_stateLock)
                                {
                                    _state = EquipState.Stopped;
                                    _mode = null;
                                    _value = 0;
                                }
                                SendFrame(ns, "ACK|STOP|STOPPED");
                                Log("[SERVER] Sent: ACK|STOP|STOPPED");
                                break;
                            }

                        default:
                            SendFrame(ns, $"ERR|{pkt.Command}|UNKNOWN_COMMAND");
                            Log($"[SERVER] Sent: ERR|{pkt.Command}|UNKNOWN_COMMAND");
                            break;
                    }

                }
            }
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopServer();
        }

        private void UpdateUi()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateUi));
                return;
            }

            btnStart.Enabled = !_running;
            btnStop.Enabled = _running;
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

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnStop_Click_1(object sender, EventArgs e)
        {
            StopServer();
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {

        }

        private void panelTop_Paint(object sender, PaintEventArgs e)
        {

        }

        private void SendFrame(NetworkStream ns, string body)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var packet = new byte[bodyBytes.Length + 2];
            packet[0] = StxEtxFramer.STX;
            Buffer.BlockCopy(bodyBytes, 0, packet, 1, bodyBytes.Length);
            packet[^1] = StxEtxFramer.ETX;

            ns.Write(packet, 0, packet.Length);
        }

    }
}
