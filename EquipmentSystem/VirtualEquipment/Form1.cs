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

                    _client = accepted;
                    Log("[SERVER] Client connected!");

                    try
                    {
                        HandleClient(_client);
                    }
                    catch (Exception ex)
                    {
                        Log($"[SERVER] Client handler error: {ex.Message}");
                    }
                    finally
                    {
                        try { _client?.Close(); } catch { }
                        _client = null;
                        Log("[SERVER] Client disconnected. Back to Accept...");
                    }
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
            using var reader = new StreamReader(ns, Encoding.UTF8);
            using var writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

            while (_running)
            {
                string? line;
                try
                {
                    line = reader.ReadLine(); // BLOCKING
                }
                catch (IOException)
                {
                    // 상대가 끊으면 종종 여기로 옴
                    break;
                }

                if (line == null) break;

                Log($"[SERVER] Received: {line}");

                if (line == "HELLO")
                {
                    writer.WriteLine("ACK");
                    Log("[SERVER] Sent: ACK");
                }
                else if (line == "BYE")
                {
                    writer.WriteLine("BYE");
                    Log("[SERVER] Sent: BYE (closing)");
                    break; // 정상 종료
                }
                else
                {
                    writer.WriteLine("UNKNOWN");
                    Log("[SERVER] Sent: UNKNOWN");
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
    }
}
