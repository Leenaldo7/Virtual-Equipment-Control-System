using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;

namespace EquipmentManager
{
    public partial class Form1 : Form
    {
        // ===== Connection =====
        private TcpClient? _client;
        private NetworkStream? _ns;

        private CancellationTokenSource? _cts;
        private Task? _recvTask;

        private readonly StxEtxFramer _framer = new StxEtxFramer();
        private readonly byte[] _recvBuf = new byte[4096];
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        private volatile bool _connected;

        // ===== Auto-Reconnect (single source of truth) =====
        private volatile bool _autoReconnectEnabled = true; // 정책 플래그(Disconnect 누르면 false)
        private volatile bool _reconnecting = false;

        private int _reconnectAttempt = 0;
        private CancellationTokenSource? _reconnectCts;
        private Task? _reconnectTask;

        private const int MaxReconnectAttempts = 20;

        // Disconnect 중복 방지 + RecvLoop self-await 방지
        private int _disconnecting = 0;

        // ===== Config =====
        private const string Host = "127.0.0.1";
        private const int Port = 5000;

        // ===== Equipment State =====
        private enum EquipState { Unknown, IDLE, RUN, STOP, ERROR }
        private EquipState _equipState = EquipState.Unknown;

        private string? _lastErrorKeyLogged;
        private const int MaxRows = 50;

        private readonly Random _rnd = new Random();

        // 재연결 딜레이: 최소/최대 캡
        private const int ReconnectMinDelayMs = 800;
        private const int ReconnectMaxDelayMs = 8000;

        private const int FirstReconnectDelayMs = 5000; // 첫 재연결 시작까지 대기

        // ===== STATUS polling (optional but recommended) =====
        private CancellationTokenSource? _statusPollCts;
        private Task? _statusPollTask;

        private const int StatusPollIntervalMs = 2000; // 2초(원하면 1000~3000)

        public Form1()
        {
            InitializeComponent();

            // splitMain이 null이 아니고, InitializeComponent에서 생성된 상태여야 함
            Shown += (_, __) =>
            {
                try
                {
                    if (splitMain == null) return;

                    splitMain.Panel1MinSize = 300;
                    splitMain.Panel2MinSize = 260;

                    int min = splitMain.Panel1MinSize;
                    int max = splitMain.Width - splitMain.Panel2MinSize - splitMain.SplitterWidth;
                    if (max < min) return; // 폭이 너무 작으면 그냥 건너뜀

                    splitMain.SplitterDistance = Math.Max(min, Math.Min(520, max));
                }
                catch { /* 무시 */ }
            };

            EnsureGridColumns();
            UpdateUi();
        }

        // Connect 버튼: async로 변경 + 자동 재연결 허용
        private async void btnConnect_Click(object sender, EventArgs e)
        {
            if (_connected)
            {
                Log("[CLIENT] Already connected.");
                await TrySendAsync("STATUS");
                return;
            }

            // 수동 Connect는 자동재연결 ON (정책)
            _autoReconnectEnabled = true;

            // 혹시 재연결 루프가 돌고 있으면 중단(수동 connect가 우선)
            StopAutoReconnect();

            await ConnectAsync(isReconnect: false);
        }

        // Disconnect 버튼 : 사용자가 누르면, 자동재연결 X
        private async void btnDisconnect_Click(object sender, EventArgs e)
        {
            _autoReconnectEnabled = false;              // 사용자 Disconnect면 자동재연결 OFF
            StopAutoReconnect();                        // 혹시 돌고 있으면 중단
            await DisconnectAsync("User requested", suppressAutoReconnect: true);
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

        // Send
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
                    catch (SocketException) 
                    { 
                        break; 
                    }

                    if (n <= 0) break;

                    var frames = _framer.Feed(_recvBuf.AsSpan(0, n), out var warn);
                    if (warn != null) Log($"[CLIENT] Framer warn: {warn}");

                    foreach (var bodyBytes in frames)
                    {
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        // DATA면 로그 최소화 + UI만 갱신
                        if (body.StartsWith("DATA|", StringComparison.OrdinalIgnoreCase))
                        {
                            HandleServerMessage(body);   // 여기서 라벨만 갱신
                            continue;
                        }

                        // 나머지는 로그 찍기
                        Log($"[CLIENT] Body: {body}");

                        // 파서 성공/실패 상관없이 먼저 상태 갱신 시도
                        HandleServerMessage(body);
                    }

                }
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Receive error: {ex.Message}");
            }
            finally
            {
                if (_connected)
                    await DisconnectAsync("RecvLoop ended", suppressAutoReconnect: false, fromRecvLoop: true);
            }
        }

        // Connect Core
        private async Task ConnectAsync(bool isReconnect)
        {
            try
            {
                SetConnUiReconnecting(isReconnect ? _reconnectAttempt : (int?)null);
                Log(isReconnect ? $"[CLIENT] Reconnecting... (attempt {_reconnectAttempt})"
                                : "[CLIENT] Connecting...");

                _client = new TcpClient();
                await _client.ConnectAsync(Host, Port);

                _ns = _client.GetStream();
                _cts = new CancellationTokenSource();

                _connected = true;
                _reconnecting = false;

                SetConnUi(true);
                Log("[CLIENT] Connected!");

                _reconnectAttempt = 0;
                _reconnecting = false;
                SetConnUiReconnecting(null);

                // 수신 루프 시작
                _recvTask = RecvLoopAsync(_cts.Token);

                // 연결 직후 상태 한번 당겨오기(현장감 + UI 동기화)
                await TrySendAsync("STATUS");
                // 연결 유지 중 STATUS polling 시작
                StartStatusPolling();

                SetConnUiReconnecting(null);
                UpdateUi();
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Connect failed: {ex.Message}");

                // 실패 시에는 "가벼운 정리"만
                try { _cts?.Cancel(); } catch { }
                try { _ns?.Close(); } catch { }
                try { _client?.Close(); } catch { }
                Cleanup();

                _connected = false;
                // 재연결 중이면 라벨은 루프가 계속 갱신하므로 여기서 건드리지 않아도 됨
                if (!isReconnect)
                {
                    SetConnUi(false);
                    SetConnUiReconnecting(null);
                    UpdateUi();
                }
            }
        }

        // 안전 종료: Cancel → Close/Dispose로 ReadAsync 깨우기 → 정리
        // suppressAutoReconnect = true 로 호출하면 Disconnect 이후 재연결 루프를 시작하지 않음
        private async Task DisconnectAsync(string reason, bool suppressAutoReconnect = false, bool fromRecvLoop = false)
        {
            if (Interlocked.Exchange(ref _disconnecting, 1) == 1)
                return;

            try
            {
                if (!_connected && _client == null && _ns == null)
                    return;

                Log($"[CLIENT] Disconnecting... ({reason})");

                _connected = false;
                // polling 중지
                StopStatusPolling();
                UpdateUi();
                SetConnUi(false);
                // UI를 즉시 끊김 상태로 확정 (스샷에서 “옆 상태 안바뀜” 방지)
                _equipState = EquipState.Unknown;
                SetStateUi("UNKNOWN");
                SetLastErrorUi("NONE");
                SetTelemetryLabels("-", "-", "-", "-", "-", "-");
                UpdateUi();

                try { _cts?.Cancel(); } catch { }
                try { _ns?.Close(); } catch { }
                try { _client?.Close(); } catch { }

                // RecvLoop에서 호출된 Disconnect는 자기 자신 await 금지
                if (!fromRecvLoop)
                {
                    try { if (_recvTask != null) await _recvTask; } catch { }
                }

                Cleanup();

                Log("[CLIENT] Disconnected.");
                UpdateUi();

                // 자동재연결: 비정상 끊김 + 정책 ON + suppressAutoReconnect=false 일 때만
                if (!suppressAutoReconnect && _autoReconnectEnabled)
                    StartAutoReconnect();
            }
            finally
            {
                Interlocked.Exchange(ref _disconnecting, 0);
            }
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

        // Auto Reconnect 
        private void StopAutoReconnect()
        {
            _reconnecting = false;

            try { _reconnectCts?.Cancel(); } catch { }
            try { _reconnectCts?.Dispose(); } catch { }

            _reconnectCts = null;
            _reconnectTask = null;
            _reconnectAttempt = 0;


            SetConnUiReconnecting(null); // 라벨 원복
            UpdateUi();
        }

        private void StartAutoReconnect()
        {
            // 이미 돌고 있으면 중복 시작 방지
            if (_reconnectTask != null && !_reconnectTask.IsCompleted) return;

            _reconnecting = true;
            _reconnectAttempt = 0;

            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = new CancellationTokenSource();

            SetConnUiReconnecting(0);  // "RECONNECTING...” 즉시 표시
            UpdateUi();                // 버튼 잠금 반영

            _reconnectTask = Task.Run(() => AutoReconnectLoopAsync(_reconnectCts.Token));
        }

        private int GetReconnectDelayMs(int attempt)
        {
            // attempt: 1,2,3...
            // base = 800ms * 2^(attempt-1), 최대 8000ms 캡
            double baseDelay = ReconnectMinDelayMs * Math.Pow(2, Math.Min(attempt - 1, 4)); // 800,1600,3200,6400,8000...
            int capped = (int)Math.Min(baseDelay, ReconnectMaxDelayMs);

            // 지터(랜덤) 0~250ms 추가: 여러 클라가 동시에 붙을 때 “동시 폭주” 방지
            int jitter = _rnd.Next(0, 251);
            return capped + jitter;
        }


        private void UpdateUi()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateUi));
                return;
            }

            bool isError = _equipState == EquipState.ERROR;
            bool busyReconnecting = _reconnecting && !_connected;

            btnConnect.Enabled = !_connected && !busyReconnecting; // 재연결 중엔 수동 Connect 막기(꼬임 방지)
            btnDisconnect.Enabled = _connected || busyReconnecting; // 재연결 중에도 “중단” 버튼으로 쓰고 싶으면 true

            // 재연결 중엔 명령 버튼들 잠금 (현업 UX)
            btnHello.Enabled = _connected && !busyReconnecting;
            btnReset.Enabled = _connected && !busyReconnecting;
            btnForceErr.Enabled = _connected && !busyReconnecting && !isError;
            btnStart.Enabled = _connected && !busyReconnecting && !isError;
            btnStop.Enabled = _connected && !busyReconnecting && !isError;

            btnSimDrop.Enabled = _connected;  // 연결 중일 때만 눌러서 강제끊김 시뮬

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
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void btnHello_Click_1(object sender, EventArgs e) => btnHello_Click(sender, e);


        private void HandleServerMessage(string body)
        {
            // DATA|ts|mode|setValue|temp|pressure|rpm
            if (body.StartsWith("DATA|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = body.Split('|');
                if (parts.Length >= 7)
                {
                    var ts = parts[1];
                    var mode = parts[2];
                    var setv = parts[3];
                    var temp = parts[4];
                    var press = parts[5];
                    var rpm = parts[6];

                    // DATA가 온다는 건 서버가 RUN에서 브로드캐스트 중이라는 뜻
                    // 재연결 직후 STATUS를 아직 못 받았어도 RUN으로 UI 보정
                    if (_equipState == EquipState.Unknown || _equipState == EquipState.IDLE || _equipState == EquipState.STOP)
                    {
                        _equipState = EquipState.RUN;
                        SetStateUi("RUN");
                        UpdateUi();
                    }


                    SetTelemetryLabels(ts, temp, press, rpm, mode, setv);
                    // 여기서 _equipState.ToString()이 Unknown이면 표에도 Unknown 뜨니까, 위에서 보정 후 넣기
                    AddSnapshot(ts, _equipState.ToString(), temp, press, rpm, mode, setv,
                        isErrorRow: (_equipState == EquipState.ERROR));
                }
                return; // DATA는 파서 OK/FAIL과 무관하게 UI만 갱신
            }

            // FORCEERR 응답 즉시 ERROR 잠금
            if (body.StartsWith("ACK|FORCEERR|", StringComparison.OrdinalIgnoreCase))
            {
                _equipState = EquipState.ERROR;
                SetStateUi("ERROR");
                SetLastErrorUi("FORCED");
                LogErrorOnce("FORCEERR", $"[CLIENT] {body}");
                UpdateUi();
                return;
            }

            // START 응답이면 RUN으로
            if (body.StartsWith("ACK|START|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = body.Split('|');

                // parts: 0 ACK, 1 STATUS, 2 STATE, 3 lastError, 4 mode, 5 setValue, 6 temp, 7 pressure, 8 rpm
                if (parts.Length >= 3)
                {
                    _equipState = ParseEquipState(parts[2]); // RUN / RUNNING 둘 다 처리
                    SetStateUi(parts[2]);

                    // START 성공 시 에러는 유지하지 않는게 일반적(한다면, NONE으로)
                    SetLastErrorUi("NONE");

                    UpdateUi();
                }
                return;
            }

            // STOP 응답이면 STOP으로
            if (body.StartsWith("ACK|STOP|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = body.Split('|'); // 0 ACK, 1 STOP, 2 STOP(or IDLE/STATE)

                if (parts.Length >= 3)
                {
                    _equipState = ParseEquipState(parts[2]);
                    SetStateUi(parts[2]);
                    UpdateUi();
                }
            }

            // 서버가 에러를 주면 ERROR로 잠금
            if (body.StartsWith("ERR|", StringComparison.OrdinalIgnoreCase))
            {
                if (body.Contains("|IN_ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    _equipState = EquipState.ERROR;
                    SetStateUi("ERROR");
                    AddEventRowNow("ERROR");
                    LogErrorOnce(body, $"[CLIENT] {body}"); // 메시지 자체를 key로 써도 됨
                    UpdateUi();
                    return;
                }
            }


            if (body.StartsWith("ALARM|ERROR|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = body.Split('|');
                // ALARM|ERROR|err|mode|set|temp|press|rpm
                if (parts.Length >= 8)
                {
                    var err = parts[2];
                    var mode = parts[3];
                    var setv = parts[4];
                    var temp = parts[5];
                    var press = parts[6];
                    var rpm = parts[7];

                    _equipState = EquipState.ERROR;
                    SetStateUi("ERROR");
                    SetLastErrorUi(err);

                    var ts = NowTs();
                    SetTelemetryLabels(ts, temp, press, rpm, mode, setv);

                    AddSnapshot(ts, "ERROR", temp, press, rpm, mode, setv, isErrorRow: true);

                    LogErrorOnce("ALARM_ERROR", $"[CLIENT] {body}");
                    UpdateUi();
                }
                return;
            }


            if (body.StartsWith("ACK|STATUS|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = body.Split('|');
                // 0 ACK, 1 STATUS, 2 STATE, 3 lastError, 4 mode, 5 set, 6 temp, 7 pressure, 8 rpm
                if (parts.Length >= 9)
                {
                    var st = parts[2];
                    var err = parts[3];
                    var mode = parts[4];
                    var setv = parts[5];
                    var temp = parts[6];
                    var press = parts[7];
                    var rpm = parts[8];
                    var ts = NowTs();

                    _equipState = ParseEquipState(st);

                    // 하단 라벨 갱신
                    SetStateUi(st);
                    SetLastErrorUi(err);
                    SetTelemetryLabels(ts, temp, press, rpm, mode, setv);

                    AddSnapshot(ts, st.ToUpperInvariant(), temp, press, rpm, mode, setv, isErrorRow: (_equipState == EquipState.ERROR));

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
                    var parts = body.Split('|'); // 0 ACK, 1 RESET, 2 IDLE

                    _lastErrorKeyLogged = null;

                    _equipState = (parts.Length >= 3) ? ParseEquipState(parts[2]) : EquipState.IDLE;
                    SetStateUi((parts.Length >= 3) ? parts[2] : "IDLE");
                    SetLastErrorUi("NONE");

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
                rtb.SelectionStart = rtb.TextLength;
                rtb.ScrollToCaret();
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

        private void SetLastErrorUi(string err)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetLastErrorUi(err)));
                return;
            }

            lblLastError.Text = $"ERR: {err}";
            lblLastError.ForeColor =
                string.Equals(err, "NONE", StringComparison.OrdinalIgnoreCase) ? Color.Black : Color.Red;
        }

        private void SetTelemetryUi(string time, string temp, string pressure, string rpm)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetTelemetryUi(time, temp, pressure, rpm)));
                return;
            }

            lblTime.Text = $"TIME: {time}";
            lblTemp.Text = $"TEMP: {temp}";
            lblPressure.Text = $"PRESS: {pressure}";
            lblRpm.Text = $"RPM: {rpm}";
        }

        // ========== UI Setter Helpers ==========

        private void SetConnUi(bool connected)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetConnUi(connected))); return; }
            lblConn.Text = connected ? "CONNECTED" : "DISCONNECTED";
            lblConn.ForeColor = connected ? System.Drawing.Color.Green : System.Drawing.Color.Gray;
        }

        private void SetConnUiReconnecting(int? attempt)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetConnUiReconnecting(attempt)));
                return;
            }

            if (attempt.HasValue)
            {
                lblConn.Text = $"RECONNECTING... ({attempt.Value})";
                lblConn.ForeColor = Color.OrangeRed;
            }
            else
            {
                // 재연결 모드 종료 → 현재 _connected 기준으로 표시
                lblConn.Text = _connected ? "CONNECTED" : "DISCONNECTED";
                lblConn.ForeColor = _connected ? Color.Green : Color.DarkGray;
            }
        }


        private void SetStateUi(string state)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetStateUi(state))); return; }
            lblState.Text = $"STATE: {state}";
            lblState.ForeColor = state.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                ? System.Drawing.Color.Red
                : System.Drawing.Color.Black;
        }

        private void SetTimeUi(string t)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetTimeUi(t))); return; }
            lblTime.Text = $"TIME: {t}";
        }

        private void SetTempUi(string temp)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetTempUi(temp))); return; }
            lblTemp.Text = $"TEMP: {temp}";
        }

        private void SetPressureUi(string press)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetPressureUi(press))); return; }
            lblPressure.Text = $"PRESS: {press}";
        }

        private void SetRpmUi(string rpm)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetRpmUi(rpm))); return; }
            lblRpm.Text = $"RPM: {rpm}";
        }

        private void SetModeUi(string mode)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetModeUi(mode))); return; }
            if (lblMode != null) lblMode.Text = $"MODE: {mode}";
        }

        private void SetSetValueUi(string setValue)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => SetSetValueUi(setValue))); return; }
            if (lblSetValue != null) lblSetValue.Text = $"SET: {setValue}";
        }

        private void AddRowToGrid(string time, string state, string temp, string press, string rpm, string mode, string setValue)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddRowToGrid(time, state, temp, press, rpm, mode, setValue)));
                return;
            }

            // 맨 위에 최신값이 올라오게 0번에 Insert
            dgvData.Rows.Insert(0, time, state, temp, press, rpm, mode, setValue);

            var row = dgvData.Rows[0];
            if (string.Equals(state, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                row.DefaultCellStyle.ForeColor = System.Drawing.Color.Red;
                row.DefaultCellStyle.Font = new System.Drawing.Font(dgvData.Font, System.Drawing.FontStyle.Bold);
            }

            // 최대 개수 유지
            while (dgvData.Rows.Count > MaxRows)
                dgvData.Rows.RemoveAt(dgvData.Rows.Count - 1);
        }

        private void EnsureGridColumns()
        {
            dgvData.AutoGenerateColumns = false;
            dgvData.Columns.Clear();

            dgvData.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTime", HeaderText = "Time", Width = 110 });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn { Name = "colState", HeaderText = "State", Width = 70 });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTemp", HeaderText = "Temp", Width = 70 });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPress", HeaderText = "Press", Width = 70 });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRpm", HeaderText = "RPM", Width = 80 });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMode", HeaderText = "Mode", Width = 60 });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSet", HeaderText = "Set", Width = 60 });

            dgvData.AllowUserToAddRows = false;
            dgvData.RowHeadersVisible = false;
            dgvData.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvData.MultiSelect = false;
        }

        private void AddTelemetryRow(string ts, string state, string temp, string press, string rpm, string mode, string setv)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddTelemetryRow(ts, state, temp, press, rpm, mode, setv)));
                return;
            }

            int idx = dgvData.Rows.Add(ts, state, temp, press, rpm, mode, setv);
            var row = dgvData.Rows[idx];

            // ERROR면 빨간 배경 + 흰 글자 + Bold
            if (string.Equals(state, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                row.DefaultCellStyle.BackColor = Color.IndianRed;   // 진한 빨강
                row.DefaultCellStyle.ForeColor = Color.White;
                row.DefaultCellStyle.SelectionBackColor = Color.DarkRed;
                row.DefaultCellStyle.SelectionForeColor = Color.White;
                row.DefaultCellStyle.Font = new Font(dgvData.Font, FontStyle.Bold);
            }
            else
            {
                // 다른 상태는 기본으로 되돌리기(중요)
                row.DefaultCellStyle.BackColor = dgvData.DefaultCellStyle.BackColor;
                row.DefaultCellStyle.ForeColor = dgvData.DefaultCellStyle.ForeColor;
                row.DefaultCellStyle.SelectionBackColor = dgvData.DefaultCellStyle.SelectionBackColor;
                row.DefaultCellStyle.SelectionForeColor = dgvData.DefaultCellStyle.SelectionForeColor;
                row.DefaultCellStyle.Font = dgvData.Font;
            }


            while (dgvData.Rows.Count > MaxRows)
                dgvData.Rows.RemoveAt(0);   // 오래된(위쪽) 제거

            // 자동 스크롤
            if (dgvData.Rows.Count > 0)
                dgvData.FirstDisplayedScrollingRowIndex = dgvData.Rows.Count - 1;
        }

        private void SetTelemetryLabels(string ts, string temp, string press, string rpm, string mode, string setv)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetTelemetryLabels(ts, temp, press, rpm, mode, setv)));
                return;
            }

            lblTime.Text = $"TIME: {ts}";
            lblTemp.Text = $"TEMP: {temp}";
            lblPressure.Text = $"PRESS: {press}";
            lblRpm.Text = $"RPM: {rpm}";
            lblMode.Text = $"MODE: {mode}";
            lblSetValue.Text = $"SET: {setv}";
        }

        private void AddEventRowNow(string state, string temp = "-", string press = "-", string rpm = "-", string mode = "-", string setv = "-")
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            AddTelemetryRow(ts, state, temp, press, rpm, mode, setv);
        }

        private void AddSnapshot(string ts, string state, string temp, string press, string rpm, string mode, string setv, bool isErrorRow)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => AddSnapshot(ts, state, temp, press, rpm, mode, setv, isErrorRow))); return; }

            dgvData.Rows.Add(ts, state, temp, press, rpm, mode, setv);

            // 최신 50개 유지
            while (dgvData.Rows.Count > MaxRows)
                dgvData.Rows.RemoveAt(0);

            // 방금 추가한 행 스타일
            var row = dgvData.Rows[dgvData.Rows.Count - 1];
            if (isErrorRow)
            {
                row.DefaultCellStyle.BackColor = Color.IndianRed; // 빨간 배경
                row.DefaultCellStyle.ForeColor = Color.White;
                row.DefaultCellStyle.Font = new Font(dgvData.Font, FontStyle.Bold);
            }

            // 자동 스크롤
            dgvData.FirstDisplayedScrollingRowIndex = dgvData.Rows.Count - 1;
        }

        private static string NowTs() => DateTime.Now.ToString("HH:mm:ss.fff");

        // 네트워크 장애/서버 다운 시뮬: "사용자 Disconnect"가 아니라 "갑작스런 끊김"
        // -> RecvLoopAsync가 예외/0바이트로 끊김 감지 -> DisconnectAsync("RecvLoop ended") -> Auto-Reconnect 동작
        private void btnSimDrop_Click(object sender, EventArgs e)
        {
            if (!_connected || _client == null || _ns == null)
            {
                Log("[CLIENT] Not connected. (SIM DROP ignored)");
                return;
            }

            // 장애 상황 => 자동재연결 유지
            _autoReconnectEnabled = true;

            Log("[CLIENT] *** SIMULATE NETWORK DROP *** (force close socket)");

            try { _client.Client?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _ns.Close(); } catch { }
            try { _client.Close(); } catch { }
        }

        private async Task AutoReconnectLoopAsync(CancellationToken ct)
        {
            Log("[CLIENT] Auto-Reconnect started.");

            _reconnecting = true;
            SetConnUiReconnecting(0);
            UpdateUi();

            try
            {
                while (!ct.IsCancellationRequested && !_connected && _autoReconnectEnabled)
                {
                    _reconnectAttempt++;

                    if (_reconnectAttempt > MaxReconnectAttempts)
                    {
                        Log("[CLIENT] Auto-Reconnect gave up (max attempts).");
                        break;
                    }

                    SetConnUiReconnecting(_reconnectAttempt);
                    UpdateUi();

                    int delayMs = (_reconnectAttempt == 1)
                        ? FirstReconnectDelayMs
                        : GetReconnectDelayMs(_reconnectAttempt);

                    Log($"[CLIENT] Waiting {delayMs}ms before reconnect attempt #{_reconnectAttempt}...");
                    await Task.Delay(delayMs, ct);

                    Log($"[CLIENT] Reconnect attempt #{_reconnectAttempt}...");

                    await ConnectAsync(isReconnect: true);

                    if (_connected)
                    {
                        Log("[CLIENT] Auto-Reconnect success.");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 중단
            }
            catch (Exception ex)
            {
                Log($"[CLIENT] Auto-Reconnect loop error: {ex.Message}");
            }
            finally
            {
                _reconnecting = false;
                SetConnUiReconnecting(null);
                UpdateUi();
                Log("[CLIENT] Auto-Reconnect ended.");
            }
        }


        private void StartStatusPolling()
        {
            StopStatusPolling();

            if (!_connected) return;

            _statusPollCts = new CancellationTokenSource();
            var ct = _statusPollCts.Token;

            _statusPollTask = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        if (_connected) await TrySendAsync("STATUS");
                        await Task.Delay(StatusPollIntervalMs, ct);
                    }
                    catch (OperationCanceledException) { break; }
                    catch { /* 끊기면 RecvLoop가 처리 */ }
                }
            }, ct);
        }

        private void StopStatusPolling()
        {
            try { _statusPollCts?.Cancel(); } catch { }
            try { _statusPollCts?.Dispose(); } catch { }
            _statusPollCts = null;
            _statusPollTask = null;
        }


    }
}
