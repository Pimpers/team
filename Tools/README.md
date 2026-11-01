# Tools

One-off asset scripts moved out of the project root (2026-10-31). Nothing in the game loads them.

- `sprite_scripts/*.ps1` - PowerShell generators / converters for enemy, effect and tile sprites. Paths inside
  them are relative to the project root (two folders up), so run them from anywhere:
  `powershell -File Tools/sprite_scripts/generate_enemy_sprites.ps1`
- `pxl/*.px.js` - pxl (Claude pixel-art DSL) sources: effects, the stage-exit log / leaves / fireflies, the spider
  web, the bear. Build with `node <pxl>/bin/pxl.js build Tools/pxl/<file> --out <dir>` and copy the PNGs into Assets.

## Who lays out which screen

The scene builders (`Assets/Editor/LobbySceneBuilder.cs`, `StageSceneBuilder.cs`, then `UISkinApplier`) create the
UI objects, but several panels are re-laid out at runtime by their own script. For these, **the runtime script is
the source of truth** - change sizes and positions there, not in the builder:

| Screen | Runtime owner | What it sets |
|---|---|---|
| Village settings | `CS/OptionsUI.cs` (rows from `GameSettings.Sliders` / `Toggles`) | panel size, rows, buttons |
| Title settings | `CS/TitleMenu.cs` (`TitleSettings`, same lists) | whole panel |
| Blacksmith | `CS/BlacksmithUI.cs` | the 4th row (행운의 방울), panel height |
| Result board | `CS/ResultUI.cs` | columns, icon strip, retry button |
| Card draft | `CS/CardDraftUI.cs` | card face layout, badges, tip |
| Pause | `CS/PauseMenu.cs` | everything (built at runtime) |
| World map labels | `CS/WorldMapLabels.cs` | Korean region plates |
| Scene texts | `CS/LocText.cs` + `Editor/SceneTextLinker.cs` | text from `Resources/Strings/ko.txt` |

`Cats Smash > Capture Screens` (`Editor/ScreenCaptureTool.cs`) saves every main screen to `Screenshots/` to check a
layout change by eye.
