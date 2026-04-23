# Security Notes

- `client secret`는 이 저장소에 포함하지 않습니다.
- 치지직 로그인은 Cloudflare Workers 백엔드를 통해 처리합니다.
- `appSessionToken`은 Windows DPAPI로 보호된 상태로만 로컬 저장됩니다.
- `accessToken`은 메모리 전용이며 저장소나 PlayerPrefs에 평문 저장하지 않습니다.
- 공개 저장소에 개인 토큰, 세션값, 테스트용 인증값을 커밋하지 마세요.
- 배포 전에는 워커 백엔드에서 레이트리밋, CORS, 세션 검증을 유지하세요.
