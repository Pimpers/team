# Bear Enemy Sprite — Unity 설정

## 파일
- `bear_enemy_sheet_4x1.png`: Idle, Walk A, Walk B, Attack 순서의 4프레임 시트
- `bear_walk_sheet_2x1.png`: Walk A, Walk B만 포함한 반복 걷기 시트
- `bear_idle_256.png`, `bear_walk_a_256.png`, `bear_walk_b_256.png`, `bear_attack_256.png`: 개별 프레임

## Import Settings
1. PNG를 Unity `Assets` 폴더에 넣습니다.
2. Inspector에서 다음처럼 설정합니다.
   - Texture Type: **Sprite (2D and UI)**
   - Sprite Mode: 시트는 **Multiple**, 개별 파일은 **Single**
   - Pixels Per Unit: **256** (또는 프로젝트 기준에 맞게 동일하게 유지)
   - Filter Mode: **Point (no filter)**
   - Compression: **None**
   - Generate Mip Maps: **Off**
3. `Sprite Editor > Slice > Grid by Cell Size`에서 `256 x 256`으로 자릅니다.
4. 프레임 순서: `0 Idle`, `1 Walk A`, `2 Walk B`, `3 Attack`

## 권장 애니메이션
- `Bear_Idle`: 0번 프레임을 6~8 FPS로 반복
- `Bear_Walk`: 1→2를 8~10 FPS로 반복
- `Bear_Attack`: 0→3→0을 10~12 FPS로 재생 후 공격 판정 이벤트를 3번 프레임 근처에 배치

투명 배경이며, 도트 느낌을 유지하려면 카메라/Canvas의 필터도 Point 계열로 맞추는 것이 좋습니다.
