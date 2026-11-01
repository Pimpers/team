# pixelfx renders (Cats Smash)

Scripts that render art from the pixelfx library (MIT, https://github.com/patch-gg/pixelfx). Copy them into a
pixelfx checkout's `tools/` folder and run with node:

- `card-icons.mjs <out>` - the 14 stat-card icons -> `Assets/Resources/SkillIcons/<card>.png`
- `role-icons.mjs <out>` - enemy role icons (drawn from pixel maps) -> `Assets/Resources/EnemyFx/role_*.png`
- `ambient.mjs <out>` - looping ambience frames -> `Assets/Resources/Ambient/<name>/` (played by `CS/AmbientFx.cs`)

The blacksmith's faces are `Assets/Resources/Village/smith_0..7.png` (cut from the supplied sheet).
