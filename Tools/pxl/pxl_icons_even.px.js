// Six card icons redrawn to the house size (Resources/SkillIcons, exported x2 to 64 px): every drawing about 26 px
// inside its 32 px canvas with the outline, so the cards read as one set (these were 19 - 31 px). House rules:
// 3-4 tones a material, a 1 px dark outline, side on, no glow, no text. Same palette as pxl_card_icons.px.js.
pal({
  k: "#1a1020",
  r: "#e84a3a", R: "#a52a2e", o: "#ff9a4a", y: "#ffe08a",
  c: "#6ad8ff", C: "#2a7ac8", B: "#1e4a8a", w: "#e8fbff",
  g: "#ffd75e", G: "#c08a20", n: "#fff3c0",
  s: "#c8c8d8", S: "#7a7a90", d: "#4a4a60",
  e: "#8ccf63", E: "#4c8a44",
})
ramps("kRroy", "kBCcw", "kGgn", "kdSs", "kEe")

// 공격력: three claw marks raked down to the right, hot at the tip.
sprite("attack", { w: 32, h: 32, outline: "k" },
  line(7, 5, 15, 26, "R", 3), line(14, 4, 21, 25, "R", 3), line(21, 6, 26, 24, "R", 3),
  line(7, 5, 14, 23, "r", 2), line(14, 4, 20, 22, "r", 2), line(21, 6, 25, 21, "r", 2),
  line(7, 5, 11, 15, "o"), line(14, 4, 17, 14, "o"), line(21, 6, 23, 14, "o"),
  px(7, 5, "y"), px(14, 4, "y"), px(21, 6, "y"))

// 연사: three arrows flying right, one over the other, the middle one ahead.
const arrow = (x, y) => [line(x, y, x + 14, y, "C", 2), line(x, y, x + 14, y, "c"),
  poly([x + 14, y - 3, x + 19, y + 1, x + 14, y + 4], "w"), px(x + 15, y, "c"),
  line(x - 1, y - 2, x + 2, y, "S"), line(x - 1, y + 2, x + 2, y, "S")]
sprite("a_rapid", { w: 32, h: 32, outline: "k" }, ...arrow(5, 8), ...arrow(9, 16), ...arrow(4, 24))

// 방어: a round shield, steel rim, blue face, a gold boss; lit from the top left.
sprite("guard", { w: 32, h: 32, outline: "k" },
  circ(16, 16, 12, "S"), circ(16, 16, 10, "C"),
  clip((x, y) => x + y > 34, circ(16, 16, 10, "B")),     // the lower right of the face in shadow
  clip((x, y) => x + y < 24, circ(16, 16, 9, "c")),      // the upper left catching the light
  line(16, 7, 16, 25, "B"), line(7, 16, 25, 16, "B"),
  circ(16, 16, 3, "G"), circ(16, 16, 2, "g"), px(15, 15, "n"),
  clip((x, y) => x + y < 30, ring(16, 16, 12, "s")))     // the rim's lit half

// 이동 속도: a running paw with three speed lines behind it.
sprite("speed", { w: 32, h: 32, outline: "k" },
  line(6, 11, 11, 11, "S", 2), line(4, 17, 11, 17, "S", 2), line(6, 23, 11, 23, "S", 2),
  ellipse(20, 20, 7, 6, "g"), shade([20, 24, 6], 1),
  circ(14, 11, 3, "g"), circ(20, 7, 3, "g"), circ(26, 10, 3, "g"),
  px(19, 18, "n"), px(13, 10, "n"), px(19, 6, "n"), px(25, 9, "n"))

// 대시 횟수: two chevrons pointing right, the front one brighter.
const chevron = (x, c, d) => [poly([x, 4, x + 8, 4, x + 17, 16, x + 8, 28, x, 28, x + 9, 16], d),
  poly([x + 2, 6, x + 7, 6, x + 15, 16, x + 7, 26, x + 2, 26, x + 10, 16], c)]
sprite("dash_more", { w: 32, h: 32, outline: "k" }, ...chevron(3, "C", "B"), ...chevron(12, "c", "C"), line(14, 8, 20, 15, "w"))

// 대시 베기: speed lines behind three slashes thrown up to the right.
sprite("dash_cut", { w: 32, h: 32, outline: "k" },
  line(3, 12, 10, 12, "R"), line(3, 16, 11, 16, "R"), line(4, 20, 10, 20, "R"),
  line(10, 22, 20, 5, "R", 2), line(13, 25, 24, 6, "R", 2), line(17, 27, 27, 9, "R", 2),
  line(10, 22, 20, 5, "r"), line(13, 25, 24, 6, "o"), line(17, 27, 27, 9, "r"),
  line(14, 15, 20, 5, "y"), line(17, 17, 24, 6, "y"))
