# TitleScene 배경 - GIF를 움직이는 배경으로

## 왜 이렇게 하나요
Unity는 `.gif` 파일을 직접 재생하지 못합니다. 그래서 `background.gif`(285x131, 480프레임, 평균 ~10fps)를
프레임별 PNG로 미리 추출해서 `Assets/Resources/BackgroundGif/frame_0000.png ~ frame_0479.png`에 넣어뒀습니다.

두 가지 재생 스크립트가 있습니다. **씬에 Player처럼 월드 스페이스 스프라이트가 있다면 반드시 2번(SpriteGifBackground)을 쓰세요.**

1. `UIGifBackground.cs` — RawImage(UI Canvas)에 붙이는 버전. Screen Space - Overlay 캔버스는 **항상 화면의 모든 것 위에 그려지기 때문에**, 씬에 Player 같은 월드 스페이스 오브젝트가 있으면 Hierarchy 순서를 아무리 바꿔도 그 오브젝트를 가려버립니다. 정말 UI만 있는 화면(버튼뿐인 순수 메뉴)에서만 쓰세요.
2. `SpriteGifBackground.cs` — **SpriteRenderer(월드 스페이스)에 붙이는 버전 (추천)**. 일반 스프라이트처럼 Sorting Layer/Order로 순서를 정하기 때문에 Player보다 뒤에 확실히 깔립니다. TitleScene에 Player가 있다면 이걸 쓰세요.

## TitleScene에서 할 일 (SpriteGifBackground 기준, 에디터에서 1회)
1. `TitleScene.unity`를 엽니다.
2. **기존에 UI Canvas 밑에 `Background`(RawImage)를 만들었다면 삭제하세요** — Player를 가리는 원인입니다.
3. Hierarchy 빈 곳 우클릭 → `2D Object > Sprite` 로 새 오브젝트를 만들고 이름을 `Background`로 변경합니다 (Canvas 밖, Player와 같은 월드 스페이스 레벨에 둡니다).
4. `Background`의 `Transform`을 화면 전체를 덮도록 위치/스케일을 조정합니다 (카메라가 Orthographic이므로 카메라 크기에 맞춰 스케일 조절, 보통 Z=0보다 뒤쪽에 두지 않아도 Sorting Order로 처리되므로 Z는 0이어도 무방).
5. `Background`에 `SpriteGifBackground` 컴포넌트를 추가합니다. `Resource Folder`는 기본값 `BackgroundGif` 그대로.
6. `Background`의 `Sprite Renderer` 컴포넌트에서 `Sorting Layer`/`Order in Layer`를 **Player보다 훨씬 낮은 값**(예: -100)으로 설정합니다. Player의 현재 Order in Layer 값을 먼저 확인하고 그보다 작은 값을 주면 됩니다.
7. `Assets/Resources/BackgroundGif/` 안의 PNG들이 Sprite로 잘 임포트됐는지 확인: 폴더에서 아무 PNG나 선택 → Inspector의 `Texture Type`이 `Sprite (2D and UI)`인지 확인. 아니라면 전체 선택 후 `Sprite (2D and UI)`로 바꾸고 Apply.
8. Play 모드로 실행해서 배경이 움직이면서 Player가 그 위에 제대로 보이는지 확인합니다.

## 나중에 다른 GIF로 교체하려면
1. `Assets/Resources/BackgroundGif/` 안의 기존 PNG들을 지웁니다.
2. 새 GIF를 프레임별 PNG로 추출해서 `frame_0000.png`, `frame_0001.png`, ... 순서로 이름 붙여 같은 폴더에 넣습니다 (이름 순서대로 재생되므로 자릿수를 맞춰 0으로 채워야 합니다).
3. `SpriteGifBackground`(또는 `UIGifBackground`)의 `Frames Per Second` 값을 새 GIF의 재생 속도에 맞게 조정합니다.
