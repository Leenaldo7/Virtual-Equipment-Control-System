using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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

            Log("[CLIENT] Disconnected.");
            UpdateUi();
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

            btnConnect.Enabled = !_connected;
            btnDisconnect.Enabled = _connected;

            // 기존 btnHello를 STATUS로 쓰는 경우
            btnHello.Enabled = _connected;

            // START/STOP 버튼이 있으면 활성화 (없으면 컴파일 에러나므로 주석처리)
            // btnStart.Enabled = _connected;
            // btnStop.Enabled = _connected;

            btnForceErr.Enabled = _connected;
            btnReset.Enabled = _connected;

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

    }
}
