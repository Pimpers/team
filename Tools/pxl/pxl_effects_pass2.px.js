// Second pass of redrawn effects (cats-smash): a small dust puff, a small eye glint, a teleport poof and an electric
// hit spark. Hard-edged, no partial alpha, 32 px cells at the cats pixel size.
pal({
  k: "#241a2e", w: "#f6fbff", c: "#a8dcf2", B: "#4f86d0", b: "#2b4a8a",
  y: "#ffd65a", o: "#f08a3c", l: "#c9d2e4", m: "#8d98b6", n: "#5c6688",
})
const S = 32, R = Math.round, TAU = Math.PI * 2
const rnd01 = (a, b) => { const v = Math.sin(a * 127.1 + b * 311.7) * 43758.5453; return v - Math.floor(v) }

// k_leap_fx: three small flat dust puffs at the feet that swell, drift apart and thin out
anim("k_leap_fx", { w: S, h: S, frames: 6, fps: 16 }, (c) => {
  const f = c.frame
  const size = [1, 2, 3, 3, 2, 1][f]
  for (const [dx, delay] of [[-7, 0], [0, 1], [7, 0]]) {
    const t = Math.min(5, Math.max(0, f - delay)), r = [1, 2, 3, 3, 2, 1][t]
    const x = 16 + dx + R(Math.sign(dx) * t * 0.8), y = 25 - (t > 2 ? 1 : 0)
    if (f - delay < 0) continue
    // a lumpy cloud: a centre and two lower side lobes, shaded darker underneath (no perfect circle: that reads as a rock)
    const lobes = [[0, 0, r], [-r - (r > 1 ? 1 : 0), 1, Math.max(1, r - 1)], [r + (r > 1 ? 1 : 0), 1, Math.max(1, r - 1)]]
    for (const [ox, oy, rr] of lobes) c.ellipse(x + ox, y + oy + 1, rr + 1, rr, "k")
    for (const [ox, oy, rr] of lobes) c.ellipse(x + ox, y + oy, rr + 1, rr, "m")
    for (const [ox, oy, rr] of lobes) c.ellipse(x + ox, y + oy - 1, Math.max(1, rr), Math.max(1, rr - 1), "l")
    if (r >= 2) { c.px(x - 1, y - 2, "w"); c.px(x, y - 2, "w") }
  }
})

// k_night_fx: a tiny glint on the cats eye, three or four pixels across at most
anim("k_night_fx", { w: S, h: S, frames: 8, fps: 12 }, (c) => {
  const f = c.frame, r = [0, 1, 1, 2, 2, 1, 1, 0][f]
  if (r === 0) return
  for (let i = -r; i <= r; i++) { c.px(16 + i, 16, "y"); c.px(16, 16 + i, "y") }
  c.px(16, 16, "w")
  if (f === 3 || f === 4) { c.px(16 + 3, 16 - 3, "w"); c.px(16 - 3, 16 + 3, "y") }
})

// fx_blink: the whisker counter - a vertical flash, then a ring of sparks flying out
anim("fx_blink", { w: S, h: S, frames: 8, fps: 22 }, (c) => {
  const f = c.frame
  if (f < 3) {
    const h = [10, 14, 8][f], wd = [1, 2, 1][f]
    c.rect(16 - wd, 16 - h, wd * 2 + 1, h * 2 + 1, f === 1 ? "w" : "c")
    c.rect(16 - wd - 1, 16 - h + 2, 1, h * 2 - 3, "B"); c.rect(16 + wd + 1, 16 - h + 2, 1, h * 2 - 3, "B")
  }
  if (f >= 1) {
    const rad = 3 + (f - 1) * 2.1, n = 10
    for (let i = 0; i < n; i++) {
      const a = i * TAU / n + (f % 2) * 0.3, x = R(16 + Math.cos(a) * rad), y = R(16 + Math.sin(a) * rad * 0.8)
      const key = f < 4 ? "w" : f < 6 ? "c" : "B"
      c.px(x, y, key); if (f < 5 && i % 2 === 0) c.px(x + (x > 16 ? 1 : -1), y, "B")
    }
  }
  if (f >= 4 && f <= 6) for (const s of [-1, 1]) c.px(16 + s * (8 + f), 16 - 6 + f, "y")
})

// fx_zap: an electric hit - a white core with jagged bolts forking out, yellow and blue
anim("fx_zap", { w: S, h: S, frames: 6, fps: 22 }, (c) => {
  const f = c.frame, L = [6, 11, 13, 12, 9, 5][f]
  for (let i = 0; i < 7; i++) {
    let x = 16, y = 16, a = i * TAU / 7 + 0.4
    for (let s = 0; s < 4; s++) {
      const step = L / 4, a2 = a + (rnd01(i * 3 + s, f % 2) - 0.5) * 1.5
      const nx = x + Math.cos(a2) * step, ny = y + Math.sin(a2) * step
      c.line(R(x), R(y), R(nx), R(ny), s < 2 ? "y" : "B"); x = nx; y = ny; a = a2
    }
    c.px(R(x), R(y), "w")
  }
  c.ellipse(16, 16, f < 3 ? 2 : 1, f < 3 ? 2 : 1, "w"); if (f < 3) c.px(16, 16, "y")
})
