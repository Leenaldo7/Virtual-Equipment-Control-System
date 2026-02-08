🧪 Virtual Equipment Control System

TCP/IP 기반 가상 장비(Server) 와 장비 제어·모니터링 클라이언트(Client) 를 구현한 개인 프로젝트입니다.
네트워크 단절 상황에서도 장비 상태를 안정적으로 복구하기 위해 Auto-Reconnect 및 상태 동기화 로직을 중심으로 설계했습니다.

💡 현업 반도체/자동화 장비 소프트웨어의 동작 흐름을 가상 환경에서 재현하는 것을 목표로 했습니다.

📌 프로젝트 개요

Server (VirtualEquipment)
가상 장비 역할

장비 상태(IDLE / RUN / STOP / ERROR) 관리

START 시 실시간 DATA 브로드캐스트

FORCEERR, RESET 등 장애 시나리오 제공

Client (EquipmentManager)
장비 제어 및 모니터링 UI

TCP/IP 통신 기반 장비 제어

실시간 데이터 수신 및 UI 표시

네트워크 장애 감지 및 자동 재연결

🛠 기술 스택

Language: C# (.NET)

UI: WinForms

Network: TCP/IP Socket

Async: async / await, Task, CancellationToken

Protocol: STX / ETX Framing

Pattern: 상태 기반(State-driven) UI 제어

📡 통신 프로토콜
프레임 구조
STX (0x02) + BODY (UTF-8) + ETX (0x03)

주요 메시지 예시
STATUS
START|A|100
STOP
FORCEERR
RESET

ACK|STATUS|RUN|NONE|A|100|25.0|1.02|1035
DATA|17:50:01.637|A|100|25.0|0.98|1025
ALARM|ERROR|FORCED|A|100|25.1|1.01|0

🧠 핵심 기능
1️⃣ 장비 상태 모델링

IDLE / RUN / STOP / ERROR 상태 정의

ERROR 상태에서는 명령 제한

RESET 시 정상 상태 복구

2️⃣ 실시간 데이터 수신

RUN 상태에서 주기적 DATA 브로드캐스트

DataGridView 기반 로그 테이블

ERROR 발생 시 시각적 강조 표시

⭐ 3️⃣ Auto-Reconnect (DAY 7 핵심)

네트워크가 끊겨도 클라이언트가 자동으로 복구되도록 구현

사용자 Disconnect vs 네트워크 장애 구분

지수 백오프(Exponential Backoff) + 지터(Jitter) 적용

재연결 중 UI 잠금 및 상태 표시

재접속 후 STATUS 요청으로 장비 상태 동기화

[CLIENT] Disconnect detected
[CLIENT] Auto-Reconnect started
[CLIENT] Reconnect attempt #1
[CLIENT] Reconnected!


💬 현업 장비 소프트웨어에서 가장 중요한 “네트워크 복원력”을 직접 구현

4️⃣ 네트워크 장애 시나리오

서버 강제 종료

소켓 강제 Drop (SIM DROP)

RecvLoop 종료 감지 후 안전한 Disconnect 처리

🖥 실행 방법

Server 실행

VirtualEquipment 프로젝트 실행
→ Start Server 클릭


Client 실행

EquipmentManager 프로젝트 실행
→ Connect → STATUS / START


장애 테스트

SIM DROP 버튼으로 네트워크 장애 시뮬레이션

서버 종료 후 자동 재연결 확인

📚 프로젝트를 통해 배운 점

TCP 통신에서 연결 상태와 장비 상태는 완전히 분리된 개념임을 이해

클라이언트가 끊겨도 서버 장비는 RUN 상태를 유지할 수 있음

Auto-Reconnect 로직의 중복 호출, 레이스 컨디션 문제를 리팩토링하며
→ 단일 책임 구조(Single Source of Truth)의 중요성을 체감

UI Thread와 네트워크 Thread 분리의 필요성

🚀 향후 개선 계획

데이터 로그 DB 저장 (SQLite / MySQL)

다중 클라이언트 접속 지원

장애 로그 기반 재현 시나리오 자동화

👤 개발자

GitHub: https://github.com/Leenaldo7
