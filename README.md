# 🧪 Virtual Equipment Control System

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WinForms](https://img.shields.io/badge/WinForms-Windows-blue?style=for-the-badge)
![TCP/IP](https://img.shields.io/badge/Network-TCP%2FIP-orange?style=for-the-badge)

> **TCP/IP 기반 가상 장비(Server)와 제어 클라이언트(Client)를 구현한 프로젝트입니다.** > 현업 반도체/자동화 장비 소프트웨어의 통신 흐름을 가상 환경에서 재현하였으며, 특히 **네트워크 단절 상황에서의 안정적인 복구(Auto-Reconnect)** 및 **상태 동기화** 로직에 집중하여 설계했습니다.

---

## 📌 Project Overview

### 1. Server (VirtualEquipment)
- **가상 장비 역할 수행**: 실제 하드웨어 없이 장비의 동작을 소프트웨어로 시뮬레이션
- **상태 관리**: `IDLE` / `RUN` / `STOP` / `ERROR` 4가지 상태 머신(FSM) 운용
- **데이터 브로드캐스트**: RUN 상태 진입 시 연결된 클라이언트에게 실시간 센서 데이터 전송
- **장애 시뮬레이션**: `FORCEERR`(강제 에러), `RESET`(복구) 기능 제공

### 2. Client (EquipmentManager)
- **장비 제어 및 모니터링 UI**: 원격에서 장비에 명령(Start, Stop)을 내리고 응답 수신
- **네트워크 복원력(Resilience)**: 통신 장애 감지 시 `Auto-Reconnect` 로직 가동
- **비동기 통신**: `async/await` 패턴을 사용하여 UI Freezing 없는 통신 구현

---

## 🛠 Tech Stack

| Category | Technology |
| :--- | :--- |
| **Language** | C# (.NET) |
| **UI Framework** | Windows Forms (WinForms) |
| **Network** | TCP/IP Socket (System.Net.Sockets) |
| **Async** | async / await, Task, CancellationToken |
| **Protocol** | Custom Frame (STX/ETX), State-driven Pattern |

---

## 🔄 Sequence & Architecture

### Auto-Reconnect Logic (Simplified)
네트워크 단절 시 클라이언트가 서버로 재접속을 시도하는 로직입니다.

```mermaid
sequenceDiagram
    participant Client
    participant Server
    
    Client->>Server: Connect Request
    Server-->>Client: Accept (Connected)
    Note over Client, Server: Normal Communication (RUN/DATA...)
    
    Client->>Client: ⚠️ Network Disconnected!
    Client->>Client: Enter Auto-Reconnect Mode (UI Lock)
    
    loop Exponential Backoff + Jitter
        Client->>Server: Reconnect Attempt #1 (Immediate)
        Server--xClient: Fail
        Client->>Client: Wait 1s...
        Client->>Server: Reconnect Attempt #2
        Server--xClient: Fail
        Client->>Client: Wait 2s...
        Client->>Server: Reconnect Attempt #3
    end
    
    Server-->>Client: Accept (Reconnected!)
    Client->>Server: Request STATUS (Synchronization)
    Server-->>Client: Current State (RUN)
    Client->>Client: Update UI & Unlock
