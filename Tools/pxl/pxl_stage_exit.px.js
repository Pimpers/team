// Stage exit (cats-smash): the hollow log the cat crawls into at the end of a cleared stage (and pops out of at
// the start of the next), the leaves that sweep across the screen between stages, and the fireflies that lead
// the way. Colours from the stages' FreeCuteTileset decor (tree bark, moss) - no black outline, dark plum edges
// like the trees. 16 px per unit like the tiles.
pal({
  K: "#201932", P: "#3f1a41", b: "#79492f", B: "#5a3326", t: "#b6963a", T: "#d9bd68",
  g: "#69c71d", G: "#1d864f", D: "#154645",
  c: "#6e2440", C: "#a83a4c", o: "#e08a2e", O: "#a8561f", r: "#e0414a", R: "#8a2342", y: "#f6f7a0", Y: "#c8e86a",
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

// The big exit log (about twice the cat's height): its mouth on the left, big enough to swallow the cat.
//   closed - vines and a plank across the mouth, the hole dark: monsters are left
//   open 0-3 - the vines gone and a wind swirl turning inside (4-frame loop; faster when it pulls the cat in)
function bigLog(c, crimson, state, frame) {
  const bark = crimson ? "c" : "b", shadeC = crimson ? "P" : "B", top = crimson ? "C" : "t"
  c.ellipse(57, 21, 6, 17, "K"); c.rect(12, 4, 46, 34, "K")
  c.ellipse(57, 21, 5, 16, bark); c.rect(12, 5, 46, 32, bark)
  c.line(13, 5, 56, 5, top); c.line(14, 6, 38, 6, top); c.line(14, 7, 24, 7, top)        // light along the top
  c.line(13, 36, 57, 36, shadeC); c.line(16, 35, 54, 35, shadeC); c.line(20, 34, 50, 34, shadeC) // shade under
  for (const [x0, x1, y] of [[24, 34, 10], [38, 50, 12], [22, 30, 16], [34, 46, 18], [26, 38, 24], [42, 54, 26], [24, 32, 30], [38, 48, 31]]) {
    c.line(x0, y, x1, y, "P")
    if ((x0 + y) % 3 === 0) c.line(x0 + 1, y - 1, x1 - 1, y - 1, top)
  }
  const m1 = crimson ? "P" : "g", m2 = crimson ? "K" : "G"
  for (const [x, w] of [[26, 12], [44, 9]]) {
    c.line(x + 1, 1, x + w - 3, 1, m1); c.rect(x, 2, w, 3, m1); c.line(x, 5, x + w - 1, 5, m2)
    c.px(x + 2, 6, m2); c.px(x + 2, 7, m2); c.px(x + 2, 8, m2); c.px(x + w - 3, 6, m2)
  }
  // the cut end: rings round a big dark hole
  c.ellipse(12, 21, 11, 18, "K")
  c.ellipse(12, 21, 10, 17, crimson ? "r" : "T")
  c.ellipse(12, 21, 9, 16, top)
  c.ellipse(12, 21, 7, 14, crimson ? "R" : "B")
  c.ellipse(12, 22, 6, 12, "K")
  if (state === "closed") {
    // vines across the hole and a plank nailed over it: shut
    c.line(5, 13, 19, 31, m2); c.line(6, 13, 20, 31, m1); c.line(19, 12, 6, 30, m2); c.line(18, 12, 5, 30, m1)
    for (const [x, y] of [[8, 17], [16, 18], [10, 26], [15, 27], [12, 21]]) { c.px(x, y, "g"); c.px(x + 1, y, "G"); c.px(x, y + 1, "G") }
    c.rect(3, 19, 19, 4, "K"); c.rect(4, 20, 17, 2, bark); c.line(4, 20, 20, 20, top); c.px(6, 21, "K"); c.px(18, 21, "K")
  } else {
    // the wind: streaks turning on two oval rings (outer three, inner two, the inner one the other way), nothing
    // from the middle out - arms from the centre made a symbol-like mark
    c.ellipse(12, 22, 5, 11, "D")
    const ring = (rx, ry, n, dir, len, col) => {
      for (let s = 0; s < n; s++) {
        for (let i = 0; i < len; i++) {
          const a = dir * frame * Math.PI / 6 + s * 2 * Math.PI / n + i * 0.16
          c.px(Math.round(12 + Math.cos(a) * rx), Math.round(22 + Math.sin(a) * ry), i < 2 ? "y" : col)
        }
      }
    }
    ring(4, 9, 3, 1, 6, "Y")
    ring(2, 4, 2, -1, 5, "G")
  }
}
sprite("exit_logbig_closed", { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, false, "closed", 0))
sprite("exit_logbig_crimson_closed", { w: 64, h: 40, pivot: "bottom" }, (c) => bigLog(c, true, "closed", 0))
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
