# Cats Smash UI style

The plan every screen is built to (unity-ui-design). The tokens live in code: `Assets/CS/UiTheme.cs` (colours, type
sizes, the text styles) and `Assets/CS/UISkin.cs` (panel sprites, spacing). Change a value there, not in a screen.

## The game
A side-view pixel-art action roguelite: a cat knight or archer fights through a forest island, picks a card after
each stage, comes back to a harbour village. PC, mouse and keyboard first, gamepad supported. The HUD is read
mid-fight; the draft, the blacksmith and the village are read at rest.

## Palette (roles)
- Wood `#734d2e` (UiTheme.Wood) - buttons, plates, the HUD frames: the village is built of it.
- Cream `#fff1d1` (UiTheme.Cream) - text on wood and on the picture.
- Ink `#1a0f0d` (UiTheme.OutlineInk) - text outlines, the 1 px outline every sprite has.
- Gold `#ffd140` (UiTheme.Gold) - rewards, ready dash bars, the player on the minimap.
- Alert `#ff594d` (UiTheme.Alert) - danger, enemies on the minimap.
- Reference palette for art: `Tools/palette.png`.

**Text over the game picture** (it moves): a translucent dark band (`UiTheme.Backdrop`, `TextBand` 70 %), or for a
short label a 1 px dark outline (`UiTheme.Outline`). The pixel font has no SDF, so TMP's own outline draws nothing.

## Type
One pixel font, three crisp steps at the 1920 x 1080 reference (match 0.5): small 24, body 36, title 48
(`UiTheme.TextSmall / TextLarge / TextTitle`). Nothing under 24 on screen; the 큰 글씨 option raises the HUD lines.

## Icons
Pixel art in the game's palette at the game's pixel size (one art pixel ~ 8-10 screen px), 1 px dark outline, 3-4
tones a material - drawn in pxl (`Tools/pxl/`), never a painted picture shrunk.

## Layout
```
+------------------------------------------------------------+
| [cat][hearts]                                    [minimap] |
| [dash][dash] 대시 · 우클릭  Tab 능력치                        |
| [stage / enemies / fish]                                   |
|                    (banners drop in here)                  |
|                                                            |
|                       [skill slots]                        |
| [set badges]                                               |
+------------------------------------------------------------+
```
Health, dash and counters top-left; the map top-right; skills bottom-centre - where the genre puts them. Every
screen is checked at 16:9, 16:10 and 4:3 (`Cats Smash > Capture Screens`).

## Motion and sound
A press: the button's own pressed sprite and a click. A pick: the card deal and `card_pick`. Banners fade in, hold,
fade out on unscaled time. The HUD does not move while nothing happens.

## The signature
The cards: wooden-vine fronts by grade, the grade in the medallion, the window tinted by the card's set tags.

## Principles
The UI is made of the village's own wood and parchment, drawn at the same pixel size as the world, so a screenshot
of any screen reads as this harbour island. Distinctive in look, conventional in where things are.
