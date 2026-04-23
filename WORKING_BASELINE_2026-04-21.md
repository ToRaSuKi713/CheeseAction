# Working Baseline 2026-04-21

이 시점은 "VSeeFace 반동이 다시 원래 자세로 복귀되고 있음" 기준 복구 지점이다.

핵심 파일:
- `Assets/Scripts/VmcOscSender.cs`
- `Assets/Scripts/SimpleLauncher.cs`
- `Assets/Scripts/HeadArmPoseController.cs`
- `Assets/Scripts/ProjectileImpactReaction.cs`
- `Assets/1.unity`

포트 기준:
- `VSeeFace -> Unity`: `39541`
- `Unity -> VSeeFace`: `39540`

VSeeFace 쪽 현재 의도:
- 송신 활성화
- 수신 활성화
- Unity 테스트 아바타는 VSeeFace 움직임을 최대한 그대로 받음
- VSeeFace 최종 반동은 Unity가 충돌 시 계산해서 다시 보냄

현재 반동 기본값:
- `upperChestPositionWeight: 0.28`
- `upperChestRotationWeight: 1.35`
- `chestPositionWeight: 0.20`
- `chestRotationWeight: 1.00`
- `spinePositionWeight: 0.12`
- `spineRotationWeight: 0.52`
- `upperBodyStrength: 0.82`
- `spineWeight: 0.28`
- `chestWeight: 0.58`
- `upperChestWeight: 0.82`

나중에 치명적인 문제가 생기면 이 상태를 기준으로 되돌린다.
