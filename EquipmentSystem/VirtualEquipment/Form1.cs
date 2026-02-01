using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VirtualEquipment
{
    public partial class Form1 : Form
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private volatile bool _running;

        private const int Port = 5000;

        // ===== Day5에 이어질 장비 상태(지금은 간단 버전 유지) =====
        private enum EquipState { Stopped, Running }
        private readonly object _stateLock = new();
        private EquipState _state = EquipState.Stopped;
        private string? _mode;
        private int _value;

        public Form1()
        {
            InitializeComponent();
            UpdateUi();
        }

        // Start 버튼: async (UI 안 멈춤)
        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (_running)
            {
                Log("[SERVER] Already running.");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();

                _running = true;
                UpdateUi();

                Log($"[SERVER] Listening... Port={Port}");

                // Accept 루프를 Task로 분리 (통신 분리 핵심)
                _acceptTask = AcceptLoopAsync(_cts.Token);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log($"[SERVER] Start failed: {ex.Message}");
                await StopServerAsync("Start failed");
            }
        }

        // Stop 버튼: async
        private async void btnStop_Click(object sender, EventArgs e)
        {
            await StopServerAsync("User requested");
        }

        // Accept 루프 (여러 클라이언트 동시 처리)
        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _running && _listener != null)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    Log("[SERVER] Client connected!");

                    // 클라별 핸들러를 Task로 분리(동시 처리)
                    _ = HandleClientAsync(client, ct);
                }
            }
            catch (Exception ex)
            {
                Log($"[SERVER] AcceptLoop error: {ex.Message}");
            }
            finally
            {
                Log("[SERVER] AcceptLoop ended.");
            }
        }

        // 클라이언트 처리 루프
        private async Task HandleClientAsync(TcpClient client, CancellationToken serverCt)
        {
            var framer = new StxEtxFramer();
            var recvBuf = new byte[4096];

            try
            {
                using (client)
                using (NetworkStream ns = client.GetStream())
                {
                    while (!serverCt.IsCancellationRequested && _running)
                    {
                        int n;
                        try
                        {
                            n = await ns.ReadAsync(recvBuf, 0, recvBuf.Length, serverCt);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (IOException)
                        {
                            break;
                        }

                        if (n <= 0) break;

                        var frames = framer.Feed(recvBuf.AsSpan(0, n), out var warn);
                        if (warn != null) Log($"[SERVER] Framer warn: {warn}");

                        foreach (var bodyBytes in frames)
                        {
                            string body = Encoding.UTF8.GetString(bodyBytes);
                            Log($"[SERVER] Body: {body}");

                            if (!PacketParser.TryParse(body, out var pkt, out var err))
                            {
                                Log($"[SERVER] Packet FAIL: {err}");
                                await SendFrameAsync(ns, $"ERR|PARSE|{Sanitize(err)}", serverCt);
                                continue;
                            }

                            Log($"[SERVER] Packet OK: {pkt}");

                            // ===== Command 처리 =====
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
                                        await SendFrameAsync(ns, resp, serverCt);
                                        Log($"[SERVER] Sent: {resp}");
                                        break;
                                    }

                                case "START":
                                    {
                                        lock (_stateLock)
                                        {
                                            _state = EquipState.Running;
                                            _mode = pkt.Params[0];
                                            _value = int.Parse(pkt.Params[1]);
                                        }
                                        await SendFrameAsync(ns, "ACK|START|RUNNING", serverCt);
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
                                        await SendFrameAsync(ns, "ACK|STOP|STOPPED", serverCt);
                                        Log("[SERVER] Sent: ACK|STOP|STOPPED");
                                        break;
                                    }

                                default:
                                    {
                                        await SendFrameAsync(ns, $"ERR|{pkt.Command}|UNKNOWN_COMMAND", serverCt);
                                        Log($"[SERVER] Sent: ERR|{pkt.Command}|UNKNOWN_COMMAND");
                                        break;
                                    }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[SERVER] Client handler error: {ex.Message}");
            }
            finally
            {
                Log("[SERVER] Client disconnected.");
            }
        }

        // STX/ETX 프레임 송신 (WriteAsync)
        private static async Task SendFrameAsync(NetworkStream ns, string body, CancellationToken ct)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            byte[] packet = new byte[bodyBytes.Length + 2];
            packet[0] = StxEtxFramer.STX;
            Buffer.BlockCopy(bodyBytes, 0, packet, 1, bodyBytes.Length);
            packet[^1] = StxEtxFramer.ETX;

            await ns.WriteAsync(packet, 0, packet.Length, ct);
        }

        private static string Sanitize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unknown";
            // 프로토콜에서 구분자 | 와 줄바꿈 등은 최소한 제거(간단)
            return s.Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        // 안전 종료: Cancel → listener.Stop → Accept/Read 깨우기 → 정리
        private async Task StopServerAsync(string reason)
        {
            if (!_running && _listener == null)
            {
                Log("[SERVER] Not running.");
                return;
            }

            Log($"[SERVER] Stop requested... ({reason})");
            _running = false;
            UpdateUi();

            try { _cts?.Cancel(); } catch { }

            try { _listener?.Stop(); } catch { }
            _listener = null;

            try
            {
                if (_acceptTask != null)
                    await _acceptTask;
            }
            catch { /* 종료 중 예외 무시 */ }

            _acceptTask = null;

            _cts?.Dispose();
            _cts = null;

            Log("[SERVER] Stopped.");
            UpdateUi();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _ = StopServerAsync("Form closing");
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

        // 디자이너가 btnStop_Click_1 같은 핸들러를 쓰면 유지(있으면)
        private async void btnStop_Click_1(object sender, EventArgs e)
        {
            await StopServerAsync("User requested");
        }
    }
}
