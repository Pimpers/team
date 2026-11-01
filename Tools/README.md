# Tools

One-off asset scripts moved out of the project root (2026-10-31). Nothing in the game loads them.

- `sprite_scripts/*.ps1` - PowerShell generators / converters for enemy, effect and tile sprites. Paths inside
  them are relative to the project root (two folders up), so run them from anywhere:
  `powershell -File Tools/sprite_scripts/generate_enemy_sprites.ps1`
- `pxl/*.px.js` - pxl (Claude pixel-art DSL) sources: effects, the stage-exit log / leaves / fireflies, the spider
  web, the bear. Build with `node <pxl>/bin/pxl.js build Tools/pxl/<file> --out <dir>` and copy the PNGs into Assets.
