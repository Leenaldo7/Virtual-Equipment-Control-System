# Virtual-Equipment-Control-System

# 🏭 Virtual Semiconductor Equipment Control System

> **TCP/IP 소켓 통신을 활용한 가상 반도체 장비 데이터 수집 및 제어 시스템** > (Virtual Equipment Data Collection & Control System using C# Socket)

![Generic badge](https://img.shields.io/badge/Language-C%23-blue.svg)
![Generic badge](https://img.shields.io/badge/Framework-.NET_6.0-purple.svg)
![Generic badge](https://img.shields.io/badge/Type-WinForms-green.svg)
![Generic badge](https://img.shields.io/badge/Status-In_Progress-yellow.svg)

## 1. 프로젝트 개요 (Overview)
제조업 현장(FAB)의 설비 제어 환경을 이해하기 위해 구축한 **C# 기반의 장비 시뮬레이션 및 제어 프로젝트**입니다. 
실제 하드웨어 없이 **가상의 장비(Server)**와 **제어 PC(Client)** 프로그램을 각각 구현하여, TCP/IP 네트워크를 통한 실시간 데이터 모니터링 및 원격 제어 기능을 구현하였습니다.

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


