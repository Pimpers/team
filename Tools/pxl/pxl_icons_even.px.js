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
  h: "#ff8aa8",
  t: "#c08a5a", T: "#7a4a2a", u: "#ecc89a",
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

// 질긴 털가죽: a stretched tabby hide - four legs and a tail out at the corners, brown stripes across.
sprite("guard", { w: 32, h: 32, outline: "k", mx: true },
  rect(9, 8, 8, 17, "t"), poly([9, 8, 3, 3, 6, 10], "t"), poly([9, 22, 3, 28, 7, 22], "t"),
  rect(15, 3, 2, 6, "t"), rect(15, 24, 2, 6, "t"),
  rect(10, 11, 7, 2, "T"), rect(11, 16, 6, 2, "T"), rect(10, 21, 7, 2, "T"),
  rect(10, 9, 3, 2, "u"), px(4, 4, "u"))

// 꾹꾹이 베기: a paw print pressed down - a big pad and four toes - with a blue cut sweeping under it.
sprite("k_knead", { w: 32, h: 32, outline: "k" },
  ellipse(16, 18, 7, 6, "h"), clip((x, y) => y > 20, ellipse(16, 18, 7, 6, "R")),
  circ(7, 10, 3, "h"), circ(13, 6, 3, "h"), circ(19, 6, 3, "h"), circ(25, 10, 3, "h"),
  px(6, 9, "y"), px(12, 5, "y"), px(18, 5, "y"), px(24, 9, "y"), px(14, 15, "y"),
  curve([3, 28, 16, 30, 29, 22], "C", { width: 2 }), curve([3, 28, 16, 30, 29, 22], "c"))

// 고양이 낮잠: a grey cat curled up asleep, tail round its nose, three sleep bubbles.
sprite("heal", { w: 32, h: 32, outline: "k" },
  ellipse(15, 21, 11, 7, "S"), clip((x, y) => y < 19, ellipse(15, 21, 11, 7, "s")),
  circ(9, 19, 5, "s"), poly([5, 16, 6, 11, 9, 15], "s"), poly([10, 14, 13, 10, 14, 15], "s"),
  line(6, 19, 8, 19, "k"), line(10, 19, 12, 19, "k"), px(9, 21, "h"),
  line(26, 22, 20, 26, "S", 2), line(20, 26, 12, 26, "S", 2),
  circ(22, 9, 2, "w"), circ(26, 5, 1, "w"), px(29, 2, "w"))

// 긴 발톱: one long hooked claw reaching up to the right, a glint at the point.
sprite("reach", { w: 32, h: 32, outline: "k" },
  poly([3, 29, 9, 29, 13, 21, 20, 12, 28, 3, 22, 6, 14, 12, 7, 20], "s"),     // wide at the root, a needle tip
  clip((x, y) => x + y > 30, poly([3, 29, 9, 29, 13, 21, 20, 12, 28, 3, 22, 6, 14, 12, 7, 20], "S")),
  line(8, 24, 13, 16, "w"), line(14, 15, 21, 8, "w"), px(27, 4, "w"),
  ellipse(6, 29, 5, 2, "h"))

// 황금 수염: a pink nose with three gold whiskers each side.
sprite("greed", { w: 32, h: 32, outline: "k", mx: true },
  curve([11, 13, 7, 10, 2, 9], "G", { width: 2 }), curve([11, 16, 6, 16, 1, 17], "G", { width: 2 }), curve([11, 19, 7, 22, 2, 25], "G", { width: 2 }),
  curve([11, 13, 7, 10, 2, 9], "g"), curve([11, 16, 6, 16, 1, 17], "g"), curve([11, 19, 7, 22, 2, 25], "g"),
  poly([12, 13, 16, 13, 16, 17, 14, 18], "h"), px(14, 14, "n"), line(16, 18, 16, 20, "R"), line(14, 21, 15, 20, "R"))

// 현상금: a wanted poster pinned up, a paw print on it and a fishbone below.
sprite("bounty", { w: 32, h: 32, outline: "k" },
  rect(6, 4, 20, 25, "n"), rect(6, 24, 20, 5, "g"), clip((x, y) => x > 22, rect(6, 4, 20, 25, "g")),
  circ(16, 3, 2, "r"), px(15, 2, "y"),
  ellipse(16, 15, 4, 3, "R"), circ(11, 10, 1, "R"), circ(14, 8, 1, "R"), circ(18, 8, 1, "R"), circ(21, 10, 1, "R"),
  line(10, 25, 21, 25, "S"), line(21, 23, 23, 25, "S"), line(21, 27, 23, 25, "S"), px(12, 24, "S"), px(15, 24, "S"), px(18, 24, "S"))

// 날렵한 몸놀림: a lightning-quick zigzag with a cat's tail tip.
sprite("haste", { w: 32, h: 32, outline: "k" },
  poly([18, 2, 8, 16, 15, 16, 10, 30, 25, 12, 17, 12, 22, 2], "g"),
  poly([18, 4, 11, 15, 16, 15], "n"), clip((x, y) => x > 17, poly([18, 2, 8, 16, 15, 16, 10, 30, 25, 12, 17, 12, 22, 2], "G")))


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
