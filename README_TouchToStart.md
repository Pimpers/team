# TitleScene - Touch to Start

## 변경 사항 (2026-09-28)
기존 PlayButton/OptionsButton을 없애고, 화면 아무 곳이나 클릭(터치)하면 바로 GameScene으로 넘어가는 방식으로 바꿨습니다.

## 구성
- `ClickCatcher` (Canvas 자식) — 화면 전체를 덮는 투명한 Image + `ClickAnywhereToStart` 컴포넌트. 클릭/터치를 감지해서 `GameScene`을 로드합니다.
- `TouchToStartText` (Canvas 자식) — "Touch to Start" TMP 텍스트. Raycast Target은 꺼져 있어서 클릭을 가로채지 않고 `ClickCatcher`로 그대로 전달됩니다.
- `Assets/CS/ClickAnywhereToStart.cs` — 클릭 감지 스크립트 (`IPointerClickHandler` 사용, 새 Input System 환경에서도 EventSystem을 통해 정상 동작).

## 참고
- 이 프로젝트는 `Active Input Handler`가 `Input System Package (New)`로 설정되어 있어서 (`ProjectSettings.asset`), 예전 방식인 `Input.GetMouseButtonDown()`은 런타임에 예외가 납니다. 그래서 UI EventSystem 기반(`IPointerClickHandler`)으로 구현했습니다 — 마우스든 터치든 EventSystem이 알아서 처리해줍니다.
- 기존 `MainMenuController`(Play/Options 버튼용)는 TitleScene에서 제거됐습니다. OptionsScene 진입 경로가 다시 필요해지면 별도로 알려주세요.
- Options로 가는 길이 없어졌으니, 나중에 설정 화면 진입이 필요하면 예: 화면 구석에 작은 톱니바퀴 아이콘 버튼을 다시 추가하는 식으로 처리하면 됩니다.
