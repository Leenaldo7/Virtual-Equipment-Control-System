# Virtual-Equipment-Control-System

# Virtual Semiconductor Equipment Control System

 **TCP/IP 소켓 통신을 활용한 가상 반도체 장비 데이터 수집 및 제어 시스템**  (Virtual Equipment Data Collection & Control System using C# Socket)

![Generic badge](https://img.shields.io/badge/Language-C%23-blue.svg)
![Generic badge](https://img.shields.io/badge/Framework-.NET_6.0-purple.svg)
![Generic badge](https://img.shields.io/badge/Type-WinForms-green.svg)
![Generic badge](https://img.shields.io/badge/Status-In_Progress-yellow.svg)

## 1. 프로젝트 개요 (Overview)
제조업 현장(FAB)의 설비 제어 환경을 이해하기 위해 구축한 C# 기반의 장비 시뮬레이션 및 제어 프로젝트입니다. 
실제 하드웨어 없이 가상의 장비(Server)와 제어 PC(Client) 프로그램을 각각 구현하여, TCP/IP 네트워크를 통한 실시간 데이터 모니터링 및 원격 제어 기능을 구현하였습니다.

### 개발 목표
* **소켓 통신 이해:** TCP/IP 3-Way Handshake 및 Socket 연결/해제 과정의 코드 레벨 구현.
* **프로토콜 설계:** STX/ETX 기반의 패킷 구조 설계 및 문자열 파싱(Parsing) 능력 배양.
* **스레드 활용:** UI 스레드와 통신 스레드 분리를 통한 비동기 처리(Non-blocking) 구현.
* **안정성 확보:** 네트워크 단절 시 자동 재접속(Auto-Reconnection) 로직 구현.

---

## 🛠 2. 기술 스택 (Tech Stack)

| 구분 | 상세 내용 | 선정 이유 |
| :--- | :--- | :--- |
| **Language** | C# (.NET 6.0) | MS 기반 제조 현장 표준 언어 및 강력한 라이브러리 지원 |
| **IDE** | Visual Studio 2022 | 생산성 및 디버깅 도구 최적화 |
| **UI Framework** | Windows Forms | 직관적인 GUI 구성 및 현장 HMI/PC 제어 프로그램의 표준 |
| **Database** | SQLite | 별도 서버 설치가 필요 없는 경량 로컬 DB |
| **Communication** | System.Net.Sockets | TCP/IP 소켓 통신 구현 |

---

## 3. TCP/IP Communication Definition (통신 방식 정의)

### 3.1 Communication Spec (고정 규격)
- **Protocol:** TCP/IP (Connection-oriented, Stream-based)
- **Topology:** 1 Client ↔ 1 Server (단일 클라이언트 기준)
- **Server 역할:** TCP Listen → Client Accept → Command 처리 → Status/Data Push
- **Client 역할:** TCP Connect → Command Send → Status/Data Receive & UI Display

#### Endpoint
- **Server IP:** `127.0.0.1` (로컬 테스트) / `192.168.x.x` (LAN 테스트)
- **Server Port:** `5000` (고정값)
- **Endpoint:** `IP:5000`

#### Data Format
- **Encoding:** UTF-8
- **Data Type:** String 기반 메시지
- **Note:** TCP는 메시지 경계가 없는 스트림이므로, DAY3에서 **STX/ETX 기반 프레이밍(Framing)** 으로 패킷 경계를 확정한다.

---

### 3.2 Connection Scenario (연결 시나리오 - Normal Case)
아래는 Client가 Connect를 수행했을 때의 정상 연결 흐름이다.

1. **Server Start**
   - `TcpListener` 실행, 포트 `5000` Listen 시작  
   - Log: `LISTENING :5000`

2. **Client Start**
   - WinForms(Client) 실행 후 사용자가 `Connect` 버튼 클릭  
   - Client 상태: `DISCONNECTED → CONNECTING`

3. **Client Connect**
   - `TcpClient.Connect(ServerIP, 5000)` 시도  
   - 성공 시 Client 상태: `CONNECTING → CONNECTED`

4. **Server Accept**
   - Server는 `AcceptTcpClient()`로 접속을 수락  
   - ClientHandler(세션) 생성 후 송수신 준비

5. **Socket Connected**
   - 양쪽에서 `NetworkStream` 확보  
   - 이후부터 송수신 가능

6. **(Optional but Recommended) Application-level Handshake**
   - Client → Server: `CONNECT` (또는 `HELLO`)
   - Server → Client: `ACK|CONNECTED`
   - Client UI에 “Connected” 표시

#### State Transition
- **Client:** `DISCONNECTED → CONNECTING → CONNECTED`
- **Server:** `LISTENING → CLIENT_CONNECTED`

---

### 3.3 Disconnect Scenario (해제 시나리오 - Normal Case)
Client가 정상적으로 Disconnect를 수행할 때의 흐름이다.

#### A) User Disconnect Button (정상 종료)
1. Client에서 `Disconnect` 버튼 클릭
2. (Optional) Client → Server: `DISCONNECT` 전송
3. Client가 `NetworkStream.Close()` / `TcpClient.Close()` 호출
4. Client 상태: `CONNECTED → DISCONNECTED`
5. Server는 Receive Loop에서 `0 byte` 또는 `SocketException`으로 연결 종료 감지
6. Server Cleanup
   - ClientHandler 종료
   - 리소스 정리 후 `LISTENING` 상태로 복귀

#### B) Client Forced Exit (강제 종료)
1. Client 프로세스 강제 종료(메시지 없이 끊김)
2. Server는 Receive에서 `0 byte/예외`로 끊김 감지
3. Server Cleanup 후 Listen 유지

---

### 3.4 Exception Cases (예외 케이스 정의)
#### 1) Server Down 상태에서 Client Connect
- Client에서 `Connect()` 실패(예외 발생)
- UI 상태: `CONNECTING → DISCONNECTED`
- 사용자에게 오류 메시지 표시 (예: connection refused / timeout)
- (DAY7) Auto-Reconnect 로직으로 주기적 재시도 가능

#### 2) Network Drop (통신 중 네트워크 단절)
- 수신 스레드에서 예외 발생 → 연결 종료 처리
- UI 상태: `CONNECTED → DISCONNECTED`
- (DAY7) Auto-Reconnect로 재연결 시도 → 성공 시 `CONNECTED` 복귀

#### 3) TCP Stream 특성 (메시지 경계 문제)
- 한 번에 여러 메시지가 붙어서 오거나,
- 한 메시지가 쪼개져 여러 번에 나눠서 도착할 수 있음
- 해결: DAY3에서 **STX/ETX 기반 패킷 프레이밍** 적용

---

### 3.5 Recommended Logging Format (권장 로그 포맷)
디버깅을 위해 Client/Server는 아래 형식으로 로그를 남긴다.

#### Client Log Example
- `[10:01:02.101] CONNECTING 127.0.0.1:5000`
- `[10:01:02.155] CONNECTED`
- `[10:01:02.200] TX CONNECT`
- `[10:01:02.230] RX ACK|CONNECTED`
- `[10:05:10.000] DISCONNECTED (user)`

#### Server Log Example
- `[10:01:00.000] LISTENING :5000`
- `[10:01:02.150] ACCEPT client=127.0.0.1:53122`
- `[10:01:02.230] RX CONNECT`
- `[10:01:02.231] TX ACK|CONNECTED`
- `[10:05:10.010] CLIENT_DISCONNECTED`



