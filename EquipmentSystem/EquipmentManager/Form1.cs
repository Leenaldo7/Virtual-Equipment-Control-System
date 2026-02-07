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

        private const int MaxRows = 50;


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
                SetConnUi(true);
                Log("[CLIENT] Connected!");

                // 백그라운드 수신 루프 시작 (통신 분리 핵심)
                _recvTask = RecvLoopAsync(_cts.Token);

                await TrySendAsync("STATUS");
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

            SetConnUi(false);
            SetStateUi("UNKNOWN");
            SetLastErrorUi("NONE");

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
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        // 디자이너가 이 핸들러를 쓰면 유지
        private async void bunDisconnect_Click(object sender, EventArgs e)
        {
            await DisconnectAsync("User requested");
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

                    SetTelemetryLabels(ts, temp, press, rpm, mode, setv);
                    AddSnapshot(ts, _equipState.ToString(), temp, press, rpm, mode, setv, isErrorRow: (_equipState == EquipState.ERROR));
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


    }
}
