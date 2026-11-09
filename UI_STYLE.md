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

**Accent colours follow the ground.** On a dark plate or the game picture: `$gold` for keywords and
`UiTheme.TagOnDark` for set tags. On cream parchment: `$key` / `$ink` and `CardPool.TagColors`. A dark ink on a dark
plate (or a light one on cream) is a bug.

## Overlays
One rule for the fight HUD (hearts, dash, counters, minimap, arrows, set badges, skill bar), applied by
`RunHudOverlay`:
- a panel that covers the screen and stops the fight (card draft, pause, route choice, result) **hides** it;
- a quick look that leaves the fight running (the Tab sheet) **dims** it to 35 %;
- banners and tips sit on top and change nothing.

## World map: progressive reveal
One region is open; only the **next** one is shown, as a "?" plate (`WorldMapLabels.NextRegion`); every other region
is under pixel cloud. A list of "준비 중" plates reads as content the game lacks; one "?" reads as what comes next. A
boss win says "다음 지역 … 곧 공개!" on the result board. The map's own title carries the realm's name as its second
line; the CHAPTER tag sits over the region it opens.

## Type
One pixel font, three crisp steps at the 1920 x 1080 reference (match 0.5): small 24, body 36, title 48
(`UiTheme.TextSmall / TextLarge / TextTitle`). Nothing under 24 on screen; the 큰 글씨 option raises the HUD lines.
**Minimum size: 24 (TextSmall), auto-size included** - no `<size=7x%>` lines and no `fontSizeMin` under it. A box
that cannot hold its text at 24 gets fewer lines or shorter words (the card face: kind line + effect, tags as a badge).

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

World signs (the fork portals' signs) keep out of the screen-fixed UI: the HUD's corner top left (560 x 360 at 1080p)
and the minimap with its legend top right (480 x 260) are a safe area, and so is the top middle while a banner is
up (800 wide, from 40 to 340 px above the centre); the test is on the sign's whole width, icon included. A sign that would fall in one is hidden until the
way is clear (pushed down it landed on its own portal); one pushed off the top of the screen moves beside its portal instead of onto it. Sign arms fit their content: 4 px inside each end, the icon, 2 px, the name (never under the small text size), then
the arrow tip; an arm that would reach past its portal's arch (4 px short of it) keeps the icon alone. Draw order
round a sign: background, portal, sparkles, signpost, cat, hit effects. Sign words are light (cream,
light orange, light green) on the dark strip; the route's colour is in the icon only.

## Motion and sound
A press: the button's own pressed sprite and a click. A pick: the card deal and `card_pick`. Banners (starter card, set
bonus, stage name: StageBanner, StageTitle) share one rule: the dark band and the words come up together in 0.15 s
(`UiTheme.BannerFade`), are held fully shown for the banner's time, and go in 0.15 s, on unscaled time. Never a long
fade: half-faded words over the fight can't be read. The band is the shared `UiTheme.TextBand`. The HUD does not move while nothing happens.

## The signature
The cards: wooden-vine fronts by grade, the grade in the medallion, the window tinted by the card's set tags.

## Principles
The UI is made of the village's own wood and parchment, drawn at the same pixel size as the world, so a screenshot
of any screen reads as this harbour island. Distinctive in look, conventional in where things are.

## Design notes
- **The first reroll of every run is free** (`CardDraftUI.BaseFreeRerolls`), plus one per level of 행운의 방울. The
  first draft comes before any fish, so a paid-only reroll was a dead button on the screen that teaches it.
- A button the player cannot afford stays clickable but looks off (grey board, price in red) and answers a click
  (a shake, 생선뼈 부족) rather than ignoring it.
