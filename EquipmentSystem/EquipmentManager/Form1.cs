using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace EquipmentManager
{
    public partial class Form1 : Form
    {
        private TcpClient? _client;
        private NetworkStream? _ns;

        private CancellationTokenSource? _cts;
        private Task? _recvTask;

        private readonly StxEtxFramer _framer = new StxEtxFramer();
        private readonly byte[] _recvBuf = new byte[4096];

        // 동시에 Send 버튼 연타해도 Write가 엉키지 않게(선택이지만 추천)
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        private volatile bool _connected;

        private const string Host = "127.0.0.1";
        private const int Port = 5000;
        
        // 장비 상태
        private enum EquipState { Unknown, IDLE, RUN, STOP, ERROR }
        private EquipState _equipState = EquipState.Unknown;

        private string? _lastErrorKeyLogged; // 중복 에러 로그 방지용


        public Form1()
        {
            InitializeComponent();
            UpdateUi();
        }

        // Connect 버튼: async로 변경
        private async void btnConnect_Click(object sender, EventArgs e)
        {
            if (_connected)
            {
                Log("[CLIENT] Already connected.");
                return;
            }

            try
            {
                _client = new TcpClient();
                Log("[CLIENT] Connecting...");

                // UI 안 멈춤
                await _client.ConnectAsync(Host, Port);

                _ns = _client.GetStream();
                _cts = new CancellationTokenSource();

                _connected = true;
                UpdateUi();

                Log("[CLIENT] Connected!");

                // 백그라운드 수신 루프 시작 (통신 분리 핵심)
                _recvTask = RecvLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Connect failed: {ex.Message}");
                await DisconnectAsync("Connect failed");
            }
        }

        // Disconnect 버튼
        private async void btnDisconnect_Click(object sender, EventArgs e)
        {
            await DisconnectAsync("User requested");
        }

        // STATUS 버튼(기존 btnHello 버튼을 STATUS로 쓰는 경우)
        private async void btnHello_Click(object sender, EventArgs e)
        {
            await TrySendAsync("STATUS");
        }

        // START/STOP 버튼을 이미 추가했다면(없으면 무시)
        private async void btnStart_Click(object sender, EventArgs e)
        {
            await TrySendAsync("START|A|100");
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            await TrySendAsync("STOP");
        }
        private async void btnForceErr_Click(object sender, EventArgs e)
        {
            await TrySendAsync("FORCEERR");
        }

        private async void btnReset_Click(object sender, EventArgs e)
        {
            await TrySendAsync("RESET");
        }


        private async Task TrySendAsync(string body)
        {
            if (!_connected || _ns == null)
            {
                Log("[CLIENT] Not connected.");
                return;
            }

            // ERROR면 RESET/STATUS만 허용 (Disconnect는 별도 버튼)
            if (_equipState == EquipState.ERROR)
            {
                bool allowed = body.StartsWith("STATUS", StringComparison.OrdinalIgnoreCase)
                            || body.StartsWith("RESET", StringComparison.OrdinalIgnoreCase);

                if (!allowed)
                {
                    LogError($"[CLIENT] BLOCKED in ERROR state: {body}");
                    return;
                }
            }

            try
            {
                await SendFrameAsync(body);
                Log($"[CLIENT] Sent frame: {body}");
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Send failed: {ex.Message}");
                await DisconnectAsync("Send failed");
            }
        }


        // STX/ETX 프레임 송신 (WriteAsync + lock)
        private async Task SendFrameAsync(string body)
        {
            if (_ns == null) throw new InvalidOperationException("Not connected.");

            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            byte[] packet = new byte[bodyBytes.Length + 2];
            packet[0] = StxEtxFramer.STX;
            Buffer.BlockCopy(bodyBytes, 0, packet, 1, bodyBytes.Length);
            packet[^1] = StxEtxFramer.ETX;

            await _sendLock.WaitAsync();
            try
            {
                await _ns.WriteAsync(packet, 0, packet.Length);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        // 핵심: 수신 루프 async 버전
        private async Task RecvLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _connected && _ns != null)
                {
                    int n;
                    try
                    {
                        n = await _ns.ReadAsync(_recvBuf, 0, _recvBuf.Length, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // 정상 종료
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

                    var frames = _framer.Feed(_recvBuf.AsSpan(0, n), out var warn);
                    if (warn != null) Log($"[CLIENT] Framer warn: {warn}");

                    foreach (var bodyBytes in frames)
                    {
                        string body = Encoding.UTF8.GetString(bodyBytes);
                        Log($"[CLIENT] Body: {body}");

                        // 파서 성공/실패 상관없이 먼저 상태 갱신 시도
                        HandleServerMessage(body);

                        if (PacketParser.TryParse(body, out var pkt, out var err))
                            Log($"[CLIENT] Packet OK: {pkt}");
                        else
                            Log($"[CLIENT] Packet FAIL: {err}");
                    }

                }
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Receive error: {ex.Message}");
            }
            finally
            {
                // 수신 루프가 끝났다는 건 연결이 끊겼거나 종료 요청
                // 이미 끊는 중이면 중복 로그 줄이기 위해 조건 처리
                if (_connected)
                    await DisconnectAsync("RecvLoop ended");
            }
        }

        // 안전 종료: Cancel → Close/Dispose로 ReadAsync 깨우기 → 정리
        private async Task DisconnectAsync(string reason)
        {
            if (!_connected && _client == null && _ns == null)
                return;

            Log($"[CLIENT] Disconnecting... ({reason})");

            _connected = false;
            UpdateUi();

            try { _cts?.Cancel(); } catch { }

            try { _ns?.Close(); } catch { }
            try { _client?.Close(); } catch { }

            // 수신 태스크가 있으면 잠깐 정리 (UI 블로킹 피하려면 await)
            try
            {
                if (_recvTask != null)
                    await _recvTask;
            }
            catch { /* 종료 중 예외는 무시 */ }

            Cleanup();

            _equipState = EquipState.Unknown;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    lblLastError.Text = "ERR: NONE";
                    lblLastError.ForeColor = Color.Black;
                }));
            }
            else
            {
                lblLastError.Text = "ERR: NONE";
                lblLastError.ForeColor = Color.Black;
            }

            Log("[CLIENT] Disconnected.");
            UpdateUi();
        }

        // 장비 상태에 따른 버튼 비활성화
        private void ApplyEquipState(EquipState st)
        {
            _equipState = st;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyEquipState(st)));
                return;
            }

            // 연결 상태가 아니면 기존 규칙 유지
            if (!_connected)
            {
                btnStart.Enabled = false;
                btnStop.Enabled = false;
                btnForceErr.Enabled = false;
                btnReset.Enabled = false;
                return;
            }

            // ERROR면 RESET/Disconnect만 허용(STATUS는 허용)
            bool isError = (st == EquipState.ERROR);

            btnReset.Enabled = true;                // 연결 중이면 항상 가능(서버가 NOT_IN_ERROR 줄 수도 있음)
            btnForceErr.Enabled = !isError;         // ERROR 상태면 더 못 누르게
            btnStart.Enabled = !isError;            // ERROR면 비활성
            btnStop.Enabled = !isError;             // ERROR면 비활성

            // STATUS 버튼
            btnHello.Enabled = true;
        }

        private void Cleanup()
        {
            _framer.Reset();

            _ns = null;
            _client = null;

            _cts?.Dispose();
            _cts = null;

            _recvTask = null;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 폼 닫힐 때는 fire-and-forget (async void로 대기하면 UI 종료가 지연될 수 있음)
            _ = DisconnectAsync("Form closing");
        }

        private void UpdateUi()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateUi));
                return;
            }

            bool isError = _equipState == EquipState.ERROR;

            btnConnect.Enabled = !_connected;
            btnDisconnect.Enabled = _connected;

            btnHello.Enabled = _connected;        // STATUS는 항상 OK
            btnReset.Enabled = _connected;        // 항상 OK
            btnForceErr.Enabled = _connected && !isError;
            btnStart.Enabled = _connected && !isError;
            btnStop.Enabled = _connected && !isError;

            lblConn.Text = _connected ? "CONNECTED" : "DISCONNECTED";
            lblConn.ForeColor = _connected ? System.Drawing.Color.Green : System.Drawing.Color.DarkGray;

            lblState.Text = $"STATE: {_equipState}";
            lblState.ForeColor = (_equipState == EquipState.ERROR) ? System.Drawing.Color.Red : System.Drawing.Color.Black;
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

        // 디자이너가 이 핸들러를 쓰면 유지
        private async void bunDisconnect_Click(object sender, EventArgs e)
        {
            await DisconnectAsync("User requested");
        }

        private void btnHello_Click_1(object sender, EventArgs e) => btnHello_Click(sender, e);


        private void HandleServerMessage(string body)
        {
            // FORCEERR 응답 즉시 ERROR 잠금
            if (body.StartsWith("ACK|FORCEERR|", StringComparison.OrdinalIgnoreCase))
            {
                _equipState = EquipState.ERROR;
                LogErrorOnce("FORCEERR", $"[CLIENT] {body}");
                UpdateUi();
                return;
            }

            // START 응답이면 RUN으로
            if (body.StartsWith("ACK|START|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = body.Split('|');

                // parts: 0 ACK, 1 STATUS, 2 STATE, 3 lastError, 4 mode, 5 setValue, 6 temp, 7 pressure, 8 rpm
                if (parts.Length >= 4)
                {
                    _equipState = ParseEquipState(parts[2]);

                    var lastErr = parts[3];
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            lblLastError.Text = $"ERR: {lastErr}";
                            lblLastError.ForeColor = (string.Equals(lastErr, "NONE", StringComparison.OrdinalIgnoreCase))
                                ? System.Drawing.Color.Black
                                : System.Drawing.Color.Red;
                        }));
                    }
                    else
                    {
                        lblLastError.Text = $"ERR: {lastErr}";
                        lblLastError.ForeColor = (string.Equals(lastErr, "NONE", StringComparison.OrdinalIgnoreCase))
                            ? System.Drawing.Color.Black
                            : System.Drawing.Color.Red;
                    }

                    if (_equipState == EquipState.ERROR)
                        LogErrorOnce("STATUS_ERROR", $"[CLIENT] {body}");

                    UpdateUi();
                }
                return;
            }

            // STOP 응답이면 STOP으로
            if (body.StartsWith("ACK|STOP|", StringComparison.OrdinalIgnoreCase))
            {
                _equipState = EquipState.STOP;
                UpdateUi();
                return;
            }

            // 서버가 에러를 주면 ERROR로 잠금
            if (body.StartsWith("ERR|", StringComparison.OrdinalIgnoreCase))
            {
                if (body.Contains("|IN_ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    _equipState = EquipState.ERROR;
                    LogErrorOnce(body, $"[CLIENT] {body}"); // 메시지 자체를 key로 써도 됨
                    UpdateUi();
                    return;
                }
            }


            if (body.StartsWith("ALARM|ERROR|", StringComparison.OrdinalIgnoreCase))
            {
                _equipState = EquipState.ERROR;
                LogErrorOnce("ALARM_ERROR", $"[CLIENT] {body}");
                UpdateUi();
                return;
            }


            if (body.StartsWith("ACK|STATUS|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = body.Split('|');
                if (parts.Length >= 3)
                {
                    _equipState = ParseEquipState(parts[2]);
                    if (_equipState == EquipState.ERROR)
                        LogErrorOnce("STATUS_ERROR", $"[CLIENT] {body}");
                    UpdateUi();
                }
                return;
            }

            if (body.StartsWith("ACK|RESET|", StringComparison.OrdinalIgnoreCase))
            {
                if (body.StartsWith("ACK|RESET|", StringComparison.OrdinalIgnoreCase))
                {
                    _equipState = EquipState.IDLE;
                    _lastErrorKeyLogged = null;

                    // UI 에러 라벨 초기화
                    lblLastError.Text = "ERR: NONE";
                    lblLastError.ForeColor = System.Drawing.Color.Black;

                    UpdateUi();
                    return;
                }
            }


            if (body.Contains("|IN_ERROR", StringComparison.OrdinalIgnoreCase))
            {
                _equipState = EquipState.ERROR;
                LogError($"[CLIENT] {body}");
                UpdateUi();
                return;
            }
        }


        private EquipState ParseEquipState(string s)
        {
            s = s.Trim().ToUpperInvariant();
            return s switch
            {
                "IDLE" => EquipState.IDLE,
                "RUN" => EquipState.RUN,
                "STOP" => EquipState.STOP,
                "STOPPED" => EquipState.STOP,
                "ERROR" => EquipState.ERROR,
                _ => EquipState.Unknown
            };
        }
        private void LogError(string msg)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke(new Action(() => LogError(msg)));
                return;
            }

            // txtLog가 RichTextBox일 때만 부분 색상 가능
            if (txtLog is RichTextBox rtb)
            {
                rtb.SelectionStart = rtb.TextLength;
                rtb.SelectionLength = 0;
                rtb.SelectionColor = System.Drawing.Color.Red;
                rtb.AppendText($"{DateTime.Now:HH:mm:ss} {msg}{Environment.NewLine}");
                rtb.SelectionColor = rtb.ForeColor;
            }
            else
            {
                // TextBox면 색상 불가 → 접두어로 강조
                txtLog.AppendText($"{DateTime.Now:HH:mm:ss} [ERROR] {msg}{Environment.NewLine}");
            }
        }

        private void LogErrorOnce(string key, string msg)
        {
            // 같은 key면 한 번만 찍음
            if (string.Equals(_lastErrorKeyLogged, key, StringComparison.OrdinalIgnoreCase))
                return;

            _lastErrorKeyLogged = key;
            LogError(msg);
        }

    }
}
