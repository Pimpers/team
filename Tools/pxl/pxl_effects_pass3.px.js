// Third pass of redrawn effects (cats-smash): the hit flashes (they replace the soft DryRain pulse_* glows), the player
// hurt flash and two expanding rings. Hard-edged, no partial alpha, near-white so a tint can colour them.
pal({
  k: "#241a2e", w: "#f6fbff", y: "#ffd65a", o: "#f08a3c", d: "#c4562e",
  r: "#e0414a", p: "#ffb0a8", l: "#c9d2e4", m: "#8d98b6",
})
const S = 32, B = 64, R = Math.round, TAU = Math.PI * 2

// One burst: rays out of the centre (len grows, then the rays break into loose dots).
function burst(c, cx, cy, rays, inner, outer, main, edge, core) {
  for (let i = 0; i < rays; i++) {
    const a = i * TAU / rays + 0.2
    const dx = Math.cos(a), dy = Math.sin(a)
    const long = i % 2 === 0
    const len = long ? outer : Math.max(inner + 1, R(outer * 0.6))
    c.line(R(cx + dx * inner), R(cy + dy * inner), R(cx + dx * len), R(cy + dy * len), main)
    c.px(R(cx + dx * len), R(cy + dy * len), edge)
  }
  if (core > 0) { c.ellipse(cx, cy, core, core, "w") }
}
function dots(c, cx, cy, n, rad, col, phase) {
  for (let i = 0; i < n; i++) {
    const a = i * TAU / n + phase
    c.px(R(cx + Math.cos(a) * rad), R(cy + Math.sin(a) * rad), col)
  }
}

// hit_small / hit_mid / hit_big: white flash -> spiked star -> loose sparks. mid and big add a dark outline halo.
const SIZES = { hit_small: [3, 5, 7], hit_mid: [4, 7, 10], hit_big: [5, 10, 14] }
for (const id of Object.keys(SIZES)) {
  const [r0, r1, r2] = SIZES[id], big = id === "hit_big", rays = big ? 12 : 8
  const main = big ? "o" : "y", edge = big ? "d" : "o"
  anim(id, { w: S, h: S, frames: 5, fps: 24 }, (c) => {
    const f = c.frame
    if (f === 0) { c.ellipse(16, 16, r0 - 1, r0 - 1, "w"); c.px(16, 16 - r0, "w"); c.px(16, 16 + r0, "w"); c.px(16 - r0, 16, "w"); c.px(16 + r0, 16, "w") }
    else if (f === 1) { burst(c, 16, 16, rays, 1, r1, "w", main, big ? 2 : 1) }
    else if (f === 2) { burst(c, 16, 16, rays, 2, r2, main, edge, 0); if (big) c.ellipse(16, 16, 2, 2, "w") }
    else if (f === 3) { dots(c, 16, 16, rays, r2 + 1, main, 0.2); dots(c, 16, 16, rays / 2, r1, edge, 0.5) }
    else { dots(c, 16, 16, rays / 2, r2 + 3, edge, 0.2) }
  })
}

// hurt: the cat is hit - a red burst with a white heart-of-it, rays kicked outward.
anim("hurt", { w: S, h: S, frames: 5, fps: 22 }, (c) => {
  const f = c.frame
  if (f === 0) { c.ellipse(16, 16, 5, 5, "r"); c.ellipse(16, 16, 3, 3, "w") }
  else if (f === 1) { burst(c, 16, 16, 8, 3, 9, "r", "k", 0); c.ellipse(16, 16, 4, 4, "p"); c.ellipse(16, 16, 2, 2, "w") }
  else if (f === 2) { burst(c, 16, 16, 8, 6, 12, "r", "d", 0); c.ellipse(16, 16, 2, 2, "p") }
  else if (f === 3) { dots(c, 16, 16, 8, 12, "r", 0.2); dots(c, 16, 16, 4, 8, "p", 0.7) }
  else { dots(c, 16, 16, 4, 14, "d", 0.2) }
})

// A hard, dithered ring that widens and thins out (no soft fade): the pixels drop out in a checker pattern.
function ringOf(c, cx, cy, rad, thick, col, thin) {
  const n = Math.max(24, R(rad * 7))
  for (let i = 0; i < n; i++) {
    const a = i * TAU / n
    if (thin && i % 2) continue
    for (let t = 0; t < thick; t++) c.px(R(cx + Math.cos(a) * (rad - t)), R(cy + Math.sin(a) * (rad - t) * 0.8), col)
  }
}
anim("ring_small", { w: S, h: S, frames: 6, fps: 22 }, (c) => {
  const f = c.frame, rad = [2, 5, 8, 10, 12, 13][f]
  ringOf(c, 16, 16, rad, f < 3 ? 2 : 1, f < 2 ? "w" : f < 4 ? "l" : "m", f >= 4)
})
anim("ring_big", { w: B, h: B, frames: 7, fps: 20 }, (c) => {
  const f = c.frame, rad = [4, 10, 16, 21, 25, 28, 30][f]
  ringOf(c, 32, 32, rad, f < 2 ? 3 : f < 4 ? 2 : 1, f < 2 ? "w" : f < 4 ? "l" : "m", f >= 5)
})
