# Working Baseline 2026-04-23

현재 기준 복구 포인트.

## 핵심 상태
- 치지직 로그인/세션 복구 기반 유지
- `appSessionToken`만 로컬 저장
- `accessToken`은 메모리 전용
- 치지직 실시간 연결은 `SocketIOUnity` 기준
- 실시간 이벤트 경로:
  - `ChzzkLoginManager`
  - `ChzzkSessionClient`
  - `ChzzkRealtimeRouter`
  - `LauncherCommandBridge`
  - `ChatOrDonationRouter`
  - `SimpleLauncher`

## 현재 중요 파일
- `Assets/Scripts/ChzzkLoginManager.cs`
- `Assets/Scripts/ChzzkSessionApiClient.cs`
- `Assets/Scripts/ChzzkSessionClient.cs`
- `Assets/Scripts/ChzzkSessionModels.cs`
- `Assets/Scripts/ChzzkRealtimeRouter.cs`
- `Assets/Scripts/LauncherCommandBridge.cs`
- `Assets/Scripts/ChatOrDonationRouter.cs`
- `Assets/Scripts/LauncherDebugUI.cs`

## 현재 라우팅 기준
- 채팅: `content -> HandleChatEvent(...)`
- 후원: `payAmount/donationText -> HandleDonationEvent(...)`
- 명령어 커스텀 규칙은 `ChatOrDonationRouter.ReplaceCustomRules(...)`
- 같은 채팅/같은 도네이션 금액 규칙은 마지막 설정 우선

## 테스트 키
- `F6` 싱글샷
- `F7` 샷건
- `F8` 스티커 붙이기

## 메모
- 치지직 세션 URL은 응답 `url` 그대로 사용
- `SocketIOUnity` 옵션은 `WebSocket`, `EIO=V3`, query 전달 사용
- 원본 payload 로그 출력 있음:
  - `SYSTEM payload`
  - `CHAT payload`
  - `DONATION payload`
