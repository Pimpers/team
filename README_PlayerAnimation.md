# PLAYER(Meow Knight) Idle 애니메이션

## 확인된 것
`Meow-Knight_Idle.png`는 이미 6프레임으로 슬라이스되어 있어요 (`Meow-Knight_Idle_0` ~ `Meow-Knight_Idle_5`).
Project 창에서 그 PNG 파일 왼쪽의 화살표(▶)를 누르면 6개 서브 스프라이트가 펼쳐집니다.

## 추가된 것
- `Assets/CS/LoopingSpriteAnimator.cs` — SpriteRenderer에 붙여서 지정한 프레임들을 순서대로 반복 재생하는 스크립트

## TitleScene에서 할 일 (에디터에서 1회)
1. `TitleScene.unity`를 열고 Hierarchy에서 `PLAYER` 오브젝트를 선택합니다.
2. `Add Component` → `LoopingSpriteAnimator` 추가.
3. Project 창에서 `Meow-Knight_Idle.png`를 펼쳐서 `Meow-Knight_Idle_0`부터 `Meow-Knight_Idle_5`까지 순서대로 `LoopingSpriteAnimator`의 `Frames` 배열에 드래그해서 채웁니다 (Size를 6으로 먼저 맞추면 편함).
4. `Frames Per Second`는 8~10 정도로 (픽셀아트 아이들 모션엔 이 정도가 자연스러움).
5. Play 모드로 실행해서 제자리에서 숨쉬듯 반복되는 Idle 모션이 재생되는지 확인.

## 나중에 확장하려면
- `Meow-Knight_Run.png`, `Attack_1~4`, `Dodge`, `Jump`, `Take_Damage`, `Death`도 전부 같은 방식으로 이미 슬라이스되어 있어서, 실제 이동/전투 기능을 붙일 때는 `LoopingSpriteAnimator`를 상태별로 프레임 배열을 바꿔주는 형태로 확장하거나(간단), Unity의 정식 Animator Controller + Animation Clip으로 전환(상태 전이가 많아지면 이쪽이 더 정석)하면 됩니다. 지금은 Title 화면 장식용이라 가장 간단한 방식으로 처리했어요.
