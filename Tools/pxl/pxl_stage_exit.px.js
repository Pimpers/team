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
