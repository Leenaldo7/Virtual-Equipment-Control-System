using System;
using System.Collections.Generic;
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
        private Task? _telemetryTask;
        private volatile bool _running;

        private const int Port = 5000;

        // ====== 장비 상태 머신 ======
        private enum EquipState { IDLE, RUN, STOP, ERROR }

        private readonly object _stateLock = new();
        private EquipState _state = EquipState.IDLE;

        // 마지막 설정/센서 값(STATUS에서 보고)
        private string _mode = "A";
        private int _setValue = 0;

        private double _temp = 25.0;
        private double _pressure = 1.00;
        private int _rpm = 0;

        // ====== 연결된 클라이언트 목록(브로드캐스트용) ======
        private sealed class ClientConn
        {
            public TcpClient Client { get; }
            public NetworkStream Stream { get; }
            public SemaphoreSlim SendLock { get; } = new(1, 1);
            public ClientConn(TcpClient c)
            {
                Client = c;
                Stream = c.GetStream();
            }
        }

        private readonly object _clientsLock = new();
        private readonly Dictionary<Guid, ClientConn> _clients = new();

        public Form1()
        {
            InitializeComponent();
            UpdateUi();
        }

        // ===================== UI =====================
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

                _acceptTask = AcceptLoopAsync(_cts.Token);

                // 텔레메트리 루프 시작 (RUN일 때만 DATA 브로드캐스트)
                _telemetryTask = TelemetryLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Log($"[SERVER] Start failed: {ex.Message}");
                await StopServerAsync("Start failed");
            }
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            await StopServerAsync("User requested");
        }

        private async void btnStop_Click_1(object sender, EventArgs e)
        {
            await StopServerAsync("User requested");
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

        // ===================== Server Core =====================

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
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }

                    Log("[SERVER] Client connected!");

                    // 연결 목록에 등록
                    var id = Guid.NewGuid();
                    var conn = new ClientConn(client);
                    lock (_clientsLock) _clients[id] = conn;

                    _ = HandleClientAsync(id, conn, ct);
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

        private async Task HandleClientAsync(Guid id, ClientConn conn, CancellationToken serverCt)
        {
            var framer = new StxEtxFramer();
            var recvBuf = new byte[4096];

            try
            {
                using (conn.Client)
                {
                    while (!serverCt.IsCancellationRequested && _running)
                    {
                        int n;
                        try
                        {
                            n = await conn.Stream.ReadAsync(recvBuf, 0, recvBuf.Length, serverCt);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (ObjectDisposedException) { break; }
                        catch (IOException) { break; }

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
                                await SendFrameAsync(conn, $"ERR|PARSE|{Sanitize(err)}", serverCt);
                                continue;
                            }

                            Log($"[SERVER] Packet OK: {pkt}");

                            // ===== 상태 머신 기반 명령 처리 =====
                            var cmd = pkt!.Command.ToUpperInvariant();

                            switch (cmd)
                            {
                                case "STATUS":
                                    {
                                        string resp;
                                        lock (_stateLock)
                                        {
                                            resp = $"ACK|STATUS|{_state}|{_mode}|{_setValue}|{_temp:F1}|{_pressure:F2}|{_rpm}";
                                        }
                                        await SendFrameAsync(conn, resp, serverCt);
                                        Log($"[SERVER] Sent: {resp}");
                                        break;
                                    }

                                case "START":
                                    {
                                        // START|MODE|VALUE (VALUE는 int)
                                        // PacketParser에서 검증해도 되고 여기서 검증해도 됨.
                                        var mode = (pkt.Params.Count >= 1) ? pkt.Params[0] : "A";
                                        var value = (pkt.Params.Count >= 2 && int.TryParse(pkt.Params[1], out var v)) ? v : 0;

                                        string resp;
                                        lock (_stateLock)
                                        {
                                            if (_state == EquipState.ERROR)
                                            {
                                                resp = "ERR|START|IN_ERROR";
                                            }
                                            else if (_state == EquipState.RUN)
                                            {
                                                resp = "ERR|START|ALREADY_RUNNING";
                                            }
                                            else
                                            {
                                                _mode = mode;
                                                _setValue = value;

                                                _state = EquipState.RUN;

                                                // RUN 시작값 세팅
                                                _rpm = Math.Clamp(value * 10, 0, 6000);
                                                resp = "ACK|START|RUN";
                                            }
                                        }

                                        await SendFrameAsync(conn, resp, serverCt);
                                        Log($"[SERVER] Sent: {resp}");
                                        break;
                                    }

                                case "STOP":
                                    {
                                        string resp;
                                        lock (_stateLock)
                                        {
                                            if (_state == EquipState.RUN)
                                            {
                                                _state = EquipState.STOP;
                                                _rpm = 0;
                                                resp = "ACK|STOP|STOP";
                                            }
                                            else
                                            {
                                                // 장비 정책 선택: 이미 멈췄어도 ACK로 친절하게
                                                resp = $"ACK|STOP|{_state}";
                                            }
                                        }

                                        await SendFrameAsync(conn, resp, serverCt);
                                        Log($"[SERVER] Sent: {resp}");
                                        break;
                                    }

                                // ERROR 상태에서만 복구
                                case "RESET":
                                    {
                                        string resp;
                                        lock (_stateLock)
                                        {
                                            if (_state == EquipState.ERROR)
                                            {
                                                _state = EquipState.IDLE;
                                                _rpm = 0;
                                                resp = "ACK|RESET|IDLE";
                                            }
                                            else
                                            {
                                                resp = $"ERR|RESET|NOT_IN_ERROR";
                                            }
                                        }

                                        await SendFrameAsync(conn, resp, serverCt);
                                        Log($"[SERVER] Sent: {resp}");
                                        break;
                                    }

                                default:
                                    {
                                        var resp = $"ERR|{cmd}|UNKNOWN_COMMAND";
                                        await SendFrameAsync(conn, resp, serverCt);
                                        Log($"[SERVER] Sent: {resp}");
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
                RemoveClient(id);
                Log("[SERVER] Client disconnected.");
            }
        }

        // ===== RUN 상태면 주기적으로 센서 데이터 생성 & 브로드캐스트 =====
        private async Task TelemetryLoopAsync(CancellationToken ct)
        {
            try
            {
                var rnd = new Random();

                while (!ct.IsCancellationRequested && _running)
                {
                    EquipState st;
                    string mode;
                    int setValue;
                    double temp, pressure;
                    int rpm;

                    lock (_stateLock)
                    {
                        st = _state;
                        mode = _mode;
                        setValue = _setValue;

                        if (st == EquipState.RUN)
                        {
                            // 간단한 “진짜 장비 느낌” 생성 로직
                            // setValue에 따라 rpm/pressure/temp가 살짝 변동
                            _rpm = Math.Clamp(_rpm + rnd.Next(-50, 51), 0, 6500);
                            _pressure = Math.Clamp(_pressure + (rnd.NextDouble() - 0.5) * 0.05, 0.80, 1.50);
                            _temp = Math.Clamp(_temp + (rnd.NextDouble() - 0.5) * 0.20 + (_rpm / 6500.0) * 0.05, 20.0, 90.0);

                            // 예시: 과속이면 ERROR
                            if (_rpm > 6200)
                            {
                                _state = EquipState.ERROR;
                            }
                        }
                        else if (st == EquipState.STOP || st == EquipState.IDLE)
                        {
                            // 멈춘 상태는 서서히 안정화
                            _rpm = 0;
                            _pressure = Math.Max(1.00, _pressure - 0.01);
                            _temp = Math.Max(25.0, _temp - 0.05);
                        }

                        // 복사(STATUS/DATA용)
                        temp = _temp;
                        pressure = _pressure;
                        rpm = _rpm;
                        st = _state;
                    }

                    // RUN이면 DATA 브로드캐스트 (그리고 ERROR로 바뀌면 ALARM도 한번 쏴줌)
                    if (st == EquipState.RUN)
                    {
                        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
                        var data = $"DATA|{ts}|{mode}|{setValue}|{temp:F1}|{pressure:F2}|{rpm}";
                        await BroadcastAsync(data, ct);
                        Log($"[SERVER] Broadcast: {data}");
                        await Task.Delay(500, ct); // 0.5초 주기
                    }
                    else if (st == EquipState.ERROR)
                    {
                        await BroadcastAsync("ALARM|ERROR|OVERSPEED", ct);
                        Log("[SERVER] Broadcast: ALARM|ERROR|OVERSPEED");
                        await Task.Delay(500, ct);
                    }
                    else
                    {
                        // IDLE/STOP일 땐 가볍게 쉼
                        await Task.Delay(300, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            catch (Exception ex)
            {
                Log($"[SERVER] TelemetryLoop error: {ex.Message}");
            }
            finally
            {
                Log("[SERVER] TelemetryLoop ended.");
            }
        }

        // ===================== Send helpers =====================
        private async Task BroadcastAsync(string body, CancellationToken ct)
        {
            List<(Guid id, ClientConn conn)> snapshot;
            lock (_clientsLock)
            {
                snapshot = new List<(Guid, ClientConn)>(_clients.Count);
                foreach (var kv in _clients)
                    snapshot.Add((kv.Key, kv.Value));
            }

            foreach (var (id, conn) in snapshot)
            {
                try
                {
                    await SendFrameAsync(conn, body, ct);
                }
                catch
                {
                    // 보내다 실패한 클라는 제거
                    RemoveClient(id);
                }
            }
        }

        private static async Task SendFrameAsync(ClientConn conn, string body, CancellationToken ct)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var packet = new byte[bodyBytes.Length + 2];
            packet[0] = StxEtxFramer.STX;
            Buffer.BlockCopy(bodyBytes, 0, packet, 1, bodyBytes.Length);
            packet[^1] = StxEtxFramer.ETX;

            await conn.SendLock.WaitAsync(ct);
            try
            {
                await conn.Stream.WriteAsync(packet, 0, packet.Length, ct);
            }
            finally
            {
                conn.SendLock.Release();
            }
        }

        private void RemoveClient(Guid id)
        {
            ClientConn? conn = null;
            lock (_clientsLock)
            {
                if (_clients.TryGetValue(id, out conn))
                    _clients.Remove(id);
            }

            if (conn != null)
            {
                try { conn.Stream.Close(); } catch { }
                try { conn.Client.Close(); } catch { }
            }
        }

        private static string Sanitize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unknown";
            return s.Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // 디자이너 이벤트 연결용(필요 시 초기화 코드 작성)
        }

        private void panelTop_Paint(object sender, PaintEventArgs e)
        {
            // 디자이너 이벤트 연결용(커스텀 페인트 안 쓰면 비워둬도 됨)
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {
                
        }


        // ===================== Stop (safe) =====================
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

            // 모든 클라 끊기
            List<Guid> ids;
            lock (_clientsLock) ids = new List<Guid>(_clients.Keys);
            foreach (var id in ids) RemoveClient(id);

            try { if (_acceptTask != null) await _acceptTask; } catch { }
            try { if (_telemetryTask != null) await _telemetryTask; } catch { }

            _acceptTask = null;
            _telemetryTask = null;

            _cts?.Dispose();
            _cts = null;

            lock (_stateLock)
            {
                _state = EquipState.IDLE;
                _rpm = 0;
            }

            Log("[SERVER] Stopped.");
            UpdateUi();
        }
    }

}
