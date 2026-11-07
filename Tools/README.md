# Tools

One-off asset scripts moved out of the project root (2026-10-31). Nothing in the game loads them.

- `sprite_scripts/*.ps1` - PowerShell generators / converters for enemy, effect and tile sprites. Paths inside
  them are relative to the project root (two folders up), so run them from anywhere:
  `powershell -File Tools/sprite_scripts/generate_enemy_sprites.ps1`
- `pxl/*.px.js` - pxl (Claude pixel-art DSL) sources: effects, the stage-exit log / leaves / fireflies, the spider
  web, the bear, the village menu icons (`pxl_village_icons.px.js`, 16 px, blown up x7 into `Assets/UI/*Icon.png`).
  Build with `node <pxl>/bin/pxl.js build Tools/pxl/<file> --out <dir>` and copy the PNGs into Assets.

### Art passes (each backs up to `_art_backup/<name>_<date>` first; `-Restore` where noted puts the originals back)

- `aether_import.ps1 -Src <dir>` - Aether card backs, lobby and world map into Assets (the map's English labels painted over).
- `aether_fx.ps1` - Aether ambient effects (volcano, campfire, chimney smoke, explosion) to sheets.
- `aether_skillfx.ps1 -Set 1..4` - Aether skill effects to sheets: background keyed out, cells cut and floored.
- `fx_to_32.ps1` / `fx_wind.ps1` - older effect sheets down to 32 px cells / the wind effect redrawn.
- `art_style_fix.ps1 -Csv <scan>` - the batch fixer for the art rules (alpha cut, 24 colours, outline).
- `cards_flatten.ps1`, `icons_place.ps1`, `bubble_from_icelivin.ps1` - card fronts flattened, skill icons centred, the smith's bubble.
- `monster_tones.ps1 -Names Slime,Bear [-Colours 12] [-NoOutline] [-Restore]` - one shared palette per monster, outline gaps closed.
- `worldmap_refill.ps1` - the rim round each Korean plate on the world map re-filled with the map round it.
- `icon_rescale.ps1 -Files <png,...> -Px 32 [-Restore]` - a too-fine picture shrunk to the game's pixel size and blown back up.
- `palette.ps1 -Build` / `-Apply <png,...> [-Restore]` - `palette.png` (UI, cats, effects + a few added) and the recolour to it (files that would shift too far are kept).
- `fx_half.ps1`, `fx_dechecker.ps1`, `fx_fadeshrink.ps1`, `fx_outline.ps1 -Cell <n>` - effect sheets halved, checker dither cleared, fade-out frames redrawn by shrinking, outlines on big pieces.
- `icon_shade.ps1 [-Restore]` - card skill icons: outline where missing, one shadow tone.

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

## Tests and the verification copy

`run_tests.ps1` runs every automated check (EditMode, PlayMode, the full-run autoplay, the screen captures) on
**this project folder**, so the Unity editor must be closed first:

    powershell -ExecutionPolicy Bypass -File Tools/run_tests.ps1

With the editor open, run the same checks on a copy instead. `sync_verify.ps1` refreshes the copy
(`%TEMP%\cs_verify` by default) and proves it is the same code:

    powershell -ExecutionPolicy Bypass -File Tools/sync_verify.ps1            # or -Dest D:\cs_verify, -Extra font,UI

- Mirrors `Assets/CS, Editor, Tests, Resources, Scenes, Enemies` (deletes what the project no longer has), copies
  every other changed / new `.meta` under `Assets` and `Packages/manifest.json`. Assets outside those folders
  (art in `Assets/Characters`, `Assets/font`...) are only copied when the copy is first created - pass `-Extra` to
  mirror more folders.
- Then compares SHA-256 of every mirrored file with the original. Last line `sync_verify: OK, N files identical`,
  exit code 0; anything else lists `MISSING / DIFFERS / EXTRA` and exits 1 - don't trust test results then.
- Paths come from the script's own location and go to robocopy as separate arguments: the Korean folder names in
  the project path broke an earlier copy step, and the tests passed on old code.

Then run Unity on the copy (same arguments as `run_tests.ps1`, other `-projectPath`):

    $U = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe"; $P = "$env:TEMP\cs_verify"
    function U { Start-Process $U -ArgumentList (@('-batchmode', '-projectPath', "`"$P`"") + $args) -Wait }
    U -logFile "$P\editmode.log" -runTests -testPlatform EditMode -testResults "$P\editmode.xml"
    U -logFile "$P\playmode.log" -runTests -testPlatform PlayMode -testResults "$P\playmode.xml"
    U -logFile "$P\fullrun.log" -executeMethod FullRunPlaytest.Run -fullRunPlaytest -playtestOut "$P\fullrun"
    U -logFile "$P\screens.log" -executeMethod ScreenCaptureTool.RunBatch      # PNGs land in $P\Screenshots

(Unity.exe is a windowed program: PowerShell's `&` doesn't wait for it, `Start-Process -Wait` does.)

## Dynamic font asset

`Assets/font/Galmuri/Galmuri11 Dynamic SDF.asset` is a dynamic TMP font: it collects glyphs as text is drawn.
`Editor/DynamicFontGuard.cs` keeps that out of git - saves of it are skipped while playing, and back in edit mode
(or when the editor quits) its dynamic data is cleared, which is the committed state. A build clears it anyway
(`ClearDynamicDataOnBuild`). Menu: `Cats Smash > Clear Dynamic Font Data`.

## Working with several Claude sessions

One folder, one writer. In this project the writer is the main Claude session; the second session (cats-smash-2) only reads here.

One folder, one writer. Two sessions editing this checkout at once mixed their changes into each other's commits
(a commit that said it was one session's work also carried the other's). So:

- A second session that writes code works in its own git worktree (`git worktree add ../team-<task> -b <task>`)
  and its work comes back by a merge; read-only sessions (reviews, questions) may share this folder.
- Before committing, `git status` should list only files you changed; stage those by name (`git add <files>`),
  never `git add -A` while another session may have work in the tree.
- The Unity editor holds the project open: batch runs go to the verification copy (`Tools/sync_verify.ps1`, above),
  or, with the editor connected through Unity-MCP, the tests run in the open editor.

## Art

Card icons (`Assets/Resources/SkillIcons/<id>.png`, 64 px, clear ground) are pixelfx stills: soft glow, several
shades, no black outline. Hand-drawn ones (pxl) keep that look: outlines in a darker shade of the colour they
touch (`selout`), not black, and at least three tones per colour.
