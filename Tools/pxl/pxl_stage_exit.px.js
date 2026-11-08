// Stage exit (cats-smash): the hollow log the cat crawls into at the end of a cleared stage (and pops out of at
// the start of the next), the leaves that sweep across the screen between stages, and the fireflies that lead
// the way. Colours from the stages' FreeCuteTileset decor (tree bark, moss) - no black outline, dark plum edges
// like the trees. 16 px per unit like the tiles.
pal({
  K: "#201932", P: "#3f1a41", b: "#79492f", B: "#5a3326", t: "#b6963a", T: "#d9bd68",
  g: "#69c71d", G: "#1d864f", D: "#154645",
  c: "#6e2440", C: "#a83a4c", o: "#e08a2e", O: "#a8561f", r: "#e0414a", R: "#8a2342", y: "#f6f7a0", Y: "#c8e86a",
  E: "#ebe3cf", e: "#c1b89a", X: "#150904", L: "#85d527", M: "#63a019", s: "#4e8415",
})

// The log, lying on the ground, its open end on the left (flipped in game for the way out on the right).
// Bark: mid brown, plum grooves along it, a light top edge; moss on the back; the cut end shows the rings
// round a dark hole the cat fits through.
function logBody(c, crimson) {
  const bark = crimson ? "c" : "b", shadeC = crimson ? "P" : "B", groove = "P", top = crimson ? "C" : "t"
  // body: a long box from the cut end to a rounded far end, outlined in plum-black
  c.ellipse(43, 12, 4, 10, "K"); c.rect(8, 3, 36, 20, "K")
  c.ellipse(43, 12, 3, 9, bark); c.rect(8, 4, 36, 18, bark)
  c.line(9, 4, 42, 4, top); c.line(10, 5, 26, 5, top); c.px(43, 5, top) // light along the top
  c.line(9, 21, 43, 21, shadeC); c.line(12, 20, 40, 20, shadeC)          // shade underneath
  // grooves: short broken runs that follow the trunk, a light lip above some
  for (const [x0, x1, y] of [[15, 22, 8], [25, 33, 9], [36, 42, 8], [13, 18, 13], [21, 31, 14], [34, 41, 15], [16, 25, 18], [29, 37, 18]]) {
    c.line(x0, y, x1, y, groove)
    if ((x0 + y) % 3 === 0) c.line(x0 + 1, y - 1, x1 - 1, y - 1, top)
  }
  // moss over the back (green in the forest, dead and dark on the crimson one), a strand hanging down
  const m1 = crimson ? "P" : "g", m2 = crimson ? "K" : "G"
  for (const [x, w] of [[17, 9], [33, 7]]) {
    c.line(x + 1, 1, x + w - 3, 1, m1); c.rect(x, 2, w, 2, m1); c.line(x, 4, x + w - 1, 4, m2)
    c.px(x + 2, 5, m2); c.px(x + 2, 6, m2); c.px(x + w - 3, 5, m2)
  }
  // the cut end: outline, light wood, a ring, the hole
  c.ellipse(8, 12, 7, 10, "K")
  c.ellipse(8, 12, 6, 9, crimson ? "r" : "T")
  c.ellipse(8, 12, 5, 8, top)
  c.ellipse(8, 12, 4, 7, crimson ? "R" : "B")
  c.ellipse(8, 13, 3, 6, "K")
  c.ellipse(9, 14, 2, 4, "P")                                    // a little depth at the bottom of the hole
}
sprite("exit_log", { w: 48, h: 24, pivot: "bottom" }, (c) => logBody(c, false))
sprite("exit_log_crimson", { w: 48, h: 24, pivot: "bottom" }, (c) => logBody(c, true))

// The big exit log (about twice the cat's height), lying on the ground, its mouth on the left. Colours from the
// reference palette (Tools/palette.png): bark in three browns, the cut face in rings of light wood, moss in three
// greens, a 1 px near-black outline.
//   closed - vines and a plank across the mouth: monsters are left
//   open 0-3 - a wind swirl turning inside the hole (4-frame loop; faster when it pulls the cat in)
const LOG = {
  forest: { out: "#150904", barkD: "#483430", bark: "#886644", barkL: "#a7845d", rim: "#c9b688",
            woodL: "#d3bf8f", woodM: "#a7845d", woodD: "#886644", hole: "#150904", depth: "#3e1f27",
            mossD: "#4e8415", moss: "#63a019", mossL: "#85d527" },
  // the hard fork: scorched red-brown bark, raw orange wood (not the boss's plum crimson)
  hunt: { out: "#150904", barkD: "#3e1f27", bark: "#5e2c19", barkL: "#742b21", rim: "#b4564f",
          woodL: "#f3a15e", woodM: "#e6934f", woodD: "#ba6834", hole: "#150904", depth: "#3e1f27",
          mossD: "#3e1f27", moss: "#742b21", mossL: "#b4564f" },
  crimson: { out: "#150904", barkD: "#3e1f27", bark: "#7b364d", barkL: "#825665", rim: "#aa7980",
             woodL: "#e9b7bb", woodM: "#aa7980", woodD: "#825665", hole: "#150904", depth: "#3e1f27",
             mossD: "#3e1f27", moss: "#7b364d", mossL: "#b4564f" },
}
// kind: false / "forest" (the plain way), true / "crimson" (the boss way), "hunt" (the hard fork: thorns, cracked
// bark, a skull mark, a red swirl), "safe" (the easy fork: flowers and round leaves on the moss, a green swirl).
function bigLog(c, kind, state, frame) {
  const v = kind === true ? "crimson" : kind === false ? "forest" : kind
  const crimson = v === "crimson"
  const k = crimson ? LOG.crimson : v === "hunt" ? LOG.hunt : LOG.forest
  // body: from the mouth (x 12) to a rounded back end (x 55, radius 6), 30 px tall (y 6-35)
  c.ellipse(55, 21, 7, 16, k.out); c.rect(12, 5, 44, 32, k.out)
  c.ellipse(55, 21, 6, 15, k.bark); c.rect(12, 6, 44, 30, k.bark)
  c.line(13, 6, 54, 6, k.barkL); c.line(14, 7, 46, 7, k.barkL)                  // the lit top of the round trunk
  c.line(13, 35, 55, 35, k.barkD); c.line(16, 34, 52, 34, k.barkD)              // the shaded underside
  // the back end: the round of the trunk, a thin pale rim where its old cut shows
  c.ellipse(56, 21, 4, 13, k.barkD); c.ellipse(56, 21, 3, 12, k.bark)
  for (let y = 10; y <= 32; y++) c.px(59 + (Math.abs(y - 21) > 8 ? -1 : 0), y, k.rim)
  // grain: short broken runs, each offset from the last, in the dark bark tone
  for (const [x0, x1, y] of [[24, 29, 11], [33, 41, 12], [45, 50, 11], [22, 27, 17], [31, 34, 18], [40, 47, 17],
                             [26, 33, 23], [38, 41, 24], [46, 51, 23], [23, 28, 29], [33, 39, 30], [44, 49, 29]]) {
    c.line(x0, y, x1, y, k.barkD)
    c.px(x1 + 1, y + 1, k.barkD)                                                // the run kinks down where it ends
  }
  // two knots: long dark ovals along the grain with a lighter heart
  for (const [x, y] of [[37, 15], [30, 26]]) {
    c.line(x - 3, y, x + 3, y, k.barkD); c.line(x - 2, y - 1, x + 2, y - 1, k.barkD); c.line(x - 1, y, x + 1, y, k.barkL) // long, along the grain
  }
  // moss lying over the top, irregular, a few drips down the side (1 px outline, three greens)
  const mossTop = [[24, 3], [25, 2], [26, 2], [27, 1], [28, 1], [29, 2], [30, 2], [31, 3], [32, 3], [33, 4],
                   [43, 4], [44, 3], [45, 2], [46, 2], [47, 3], [48, 4]]
  const blob = (x0, x1, y0, drips) => {
    for (let x = x0; x <= x1; x++) {
      const top = (mossTop.find(([mx]) => mx === x) || [x, 4])[1]
      c.px(x, top - 1, k.out)                                                   // outline above
      for (let y = top; y <= 7; y++) c.px(x, y, y === top ? k.mossL : y < 6 ? k.moss : k.mossD)
      c.px(x, 8, k.out)
    }
    c.line(x0 - 1, 4, x0 - 1, 7, k.out); c.line(x1 + 1, 5, x1 + 1, 7, k.out)
    for (const [x, len] of drips) {                                             // drips hanging down the bark
      for (let y = 8; y < 8 + len; y++) c.px(x, y, k.mossD)
      c.px(x, 8 + len, k.out); c.px(x - 1, 8, k.mossD)
    }
  }
  if (v === "hunt") {
    // thorns along the top instead of moss, a split in the bark, a skull mark burnt into it
    for (const x of [18, 23, 28, 33, 38, 43, 48]) {
      const h = x % 10 === 3 ? 4 : 3
      // a thorn two pixels wide at the base, one at the tip, a pale point
      for (let i = 0; i < h; i++) {
        const w = i < 2 ? 1 : 0
        for (let dx = -w; dx <= 0; dx++) c.px(x + dx, 5 - i, i === h - 1 ? "#d3bf8f" : "#483430")
        c.px(x - w - 1, 5 - i, k.out); c.px(x + 1, 5 - i, k.out)
      }
      c.px(x, 5 - h, k.out)
    }
    for (const [x, y] of [[20, 9], [21, 10], [21, 11], [22, 12], [22, 13], [23, 14], [50, 26], [49, 27], [49, 28], [48, 29], [48, 30]]) c.px(x, y, k.out)
    for (const [x, y] of [[22, 10], [23, 13], [50, 27]]) c.px(x, y, "#b4564f")          // raw red wood in the cracks
    // the skull: bone, two dark eyes, a nose, teeth
    c.rect(38, 17, 7, 5, k.out); c.rect(39, 18, 5, 4, "#ebe3cf"); c.rect(39, 21, 5, 2, "#c1b89a"); c.line(38, 22, 44, 22, k.out)
    c.px(40, 19, k.out); c.px(42, 19, k.out); c.px(41, 20, "#c1b89a"); c.px(40, 22, "#ebe3cf"); c.px(42, 22, "#ebe3cf")
    c.line(39, 24, 43, 24, k.out); c.line(40, 23, 42, 23, "#c1b89a")
  } else {
    blob(24, 33, 2, [[26, 2], [31, 1]])
    blob(43, 48, 3, [[45, 3]])
  }
  if (v === "safe") {
    // flowers on the moss and round leaves sprouting from it
    const flower = (x, y, petal) => { c.px(x, y - 1, petal); c.px(x - 1, y, petal); c.px(x + 1, y, petal); c.px(x, y + 1, petal); c.px(x, y, "#fbcd0d") }
    flower(26, 1, "#f29cb0"); flower(31, 2, "#ebe3cf"); flower(46, 2, "#f29cb0")
    for (const [x, y] of [[36, 3], [52, 7]]) { c.px(x, y, "#4e8415"); c.rect(x - 2, y - 2, 2, 2, "#85d527"); c.rect(x + 1, y - 2, 2, 2, "#63a019"); c.px(x - 2, y - 3, k.out); c.px(x + 2, y - 3, k.out) }
  }
  // the cut end: bark rim, then rings of wood round the hole (light, mid, dark), the hole itself
  c.ellipse(12, 21, 11, 18, k.out)
  c.ellipse(12, 21, 10, 17, k.barkD)
  c.ellipse(12, 21, 9, 16, k.woodL)
  c.ellipse(12, 21, 8, 15, k.woodM)
  c.ellipse(12, 21, 7, 14, k.woodL)
  c.ellipse(12, 21, 6, 13, k.woodD)
  c.ellipse(12, 22, 5, 11, k.hole)
  if (state === "closed") {
    // vines across the hole and a plank nailed over it: shut
    c.line(6, 13, 18, 31, k.mossD); c.line(7, 13, 19, 31, k.moss); c.line(18, 12, 7, 30, k.mossD); c.line(17, 12, 6, 30, k.moss)
    for (const [x, y] of [[9, 17], [15, 18], [10, 26], [14, 27], [12, 21]]) { c.px(x, y, k.mossL); c.px(x + 1, y, k.mossD); c.px(x, y + 1, k.mossD) }
    c.rect(3, 19, 19, 4, k.out); c.rect(4, 20, 17, 2, k.barkL); c.line(4, 20, 20, 20, k.rim); c.px(6, 21, k.out); c.px(18, 21, k.out)
  } else {
    // the wind: streaks turning on two oval rings (outer three, inner two, the inner one the other way)
    c.ellipse(12, 22, 4, 10, k.depth)
    const ring = (rx, ry, n, dir, len, col, head) => {
      for (let s = 0; s < n; s++)
        for (let i = 0; i < len; i++) {
          const a = dir * frame * Math.PI / 6 + s * 2 * Math.PI / n + i * 0.16
          c.px(Math.round(12 + Math.cos(a) * rx), Math.round(22 + Math.sin(a) * ry), i < 2 ? head : col)
        }
    }
    const sw = { forest: ["#85d527", "#c3d971", "#4e8415", "#85d527"], crimson: ["#b4564f", "#e9b7bb", "#7b364d", "#aa7980"],
                 hunt: ["#e5354e", "#fbcd0d", "#ec6309", "#f3a15e"], safe: ["#63a019", "#c3d971", "#85d527", "#ebe3cf"] }[v]
    ring(3, 8, 3, 1, 6, sw[0], sw[1])
    ring(1, 4, 2, -1, 5, sw[2], sw[3])
  }
}
sprite("exit_logbig_closed", { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, false, "closed", 0))
sprite("exit_logbig_crimson_closed", { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, true, "closed", 0))
// the fork: the hard way (사냥 길) and the easy way (안전 길)
for (const v of ["hunt", "safe"]) {
  sprite(`exit_logbig_${v}_closed`, { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, v, "closed", 0))
  for (let f = 0; f < 4; f++) sprite(`exit_logbig_${v}_open_${f}`, { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, v, "open", f))
}
// The fork's head icons (shape as well as colour, for 색약): a skull for the hard way, a sprout for the easy way.
sprite("exit_icon_hunt", { outline: "#150904" }, `
  .EEEEE.
  EEEEEEE
  EXXEXXE
  EXXEXXE
  EEEXEEE
  .eEEEe.
  .E.E.E.`)
sprite("exit_icon_safe", { outline: "#150904" }, `
  .LL.MM.
  LLLMMMM
  LLLMMMM
  .LLsMM.
  ...s...
  ...s...
  ..sss..`)
for (let f = 0; f < 4; f++) {
  sprite(`exit_logbig_open_${f}`, { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, false, "open", f))
  sprite(`exit_logbig_crimson_open_${f}`, { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, true, "open", f))
}

// Leaves for the screen sweep: a few shapes, green / autumn / red. Drawn pointing right with a stem.
sprite("_leaf_a", `
  ...GGG...
  .GGgggGG.
  GgDDDDggG
  .GGgggGG.
  ...GGG...`)
sprite("_leaf_b", `
  ....GGG..
  ..GGgggG.
  GDDDDggG.
  ..GggggGG
  ...GGGG..`)
sprite("_leaf_c", `
  .GG......
  GggGG....
  GgDggGG..
  .GggDggG.
  ..GGgggG.
  ....GGG..`)
// The leaf grids are green (G edge, g fill, D vein): the other tones swap them.
const LEAF = { green: {}, gold: { G: "O", g: "o", D: "O" }, red: { G: "R", g: "r", D: "R" } }
for (const [tone, swap] of Object.entries(LEAF))
  for (const s of ["a", "b", "c"])
    sprite(`leaf_${tone}_${s}`, { base: `_leaf_${s}`, swap })

// A firefly: a bright core in a soft green glow, two frames (bright, dim) - the game pulses between them.
sprite("firefly", { w: 10, h: 5 }, (c) => {
  c.px(1, 1, "Y"); c.px(0, 2, "Y"); c.px(2, 2, "Y"); c.px(1, 3, "Y"); c.px(1, 2, "y")
  c.px(6, 2, "Y"); c.px(7, 2, "y"); c.px(8, 2, "Y"); c.px(7, 1, "Y"); c.px(7, 3, "Y")
  c.px(7, 0, "Y"); c.px(7, 4, "Y"); c.px(5, 2, "Y"); c.px(9, 2, "Y")
})

sheet("exit_preview", ["exit_log", "exit_log_crimson", "leaf_*", "firefly"], { pad: 2 })
