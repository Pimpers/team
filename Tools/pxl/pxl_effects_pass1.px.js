// Redrawn skill effects (cats-smash): hard-edged pixels, dark outline where it must read on a bright sky,
// no partial alpha. 32 px cells (heal 48), the cats pixel size. Strips: frames side by side.
pal({
  k: "#241a2e", w: "#f6fbff", c: "#a8dcf2", B: "#4f86d0", b: "#2b4a8a",
  y: "#ffd65a", o: "#f08a3c", O: "#b85a2c", r: "#e2585e", q: "#ff9fc0",
  g: "#8ccf63", G: "#4c8a44", l: "#c9d2e4", m: "#8d98b6", n: "#5c6688",
})
const S = 32, R = Math.round, TAU = Math.PI * 2
const rnd01 = (a, b) => { const v = Math.sin(a * 127.1 + b * 311.7) * 43758.5453; return v - Math.floor(v) }
// a plus-shaped star of arm length r with a bright centre and a dark rim
function star(c, cx, cy, r, key, core, rim = true) {
  if (rim) {
    for (let i = -r - 1; i <= r + 1; i++) { c.px(cx + i, cy - 1, "k"); c.px(cx + i, cy + 1, "k"); c.px(cx - 1, cy + i, "k"); c.px(cx + 1, cy + i, "k") }
    c.px(cx - r - 2, cy, "k"); c.px(cx + r + 2, cy, "k"); c.px(cx, cy - r - 2, "k"); c.px(cx, cy + r + 2, "k")
  }
  for (let i = -r; i <= r; i++) { c.px(cx + i, cy, key); c.px(cx, cy + i, key) }
  c.px(cx, cy, core)
}

// k_groom: gold and pink twinkles rising off the cat
anim("k_groom_fx", { w: S, h: S, frames: 8, fps: 14 }, (c) => {
  for (let i = 0; i < 3; i++) {
    const t = ((c.frame / 8) + i / 3) % 1
    const x = [10, 17, 24][i] + (i === 1 ? Math.round(Math.sin(t * TAU) * 1) : 0), y = R(27 - t * 21)
    const r = t < 0.25 ? 1 : t < 0.75 ? 2 : 1
    star(c, x, y, r, i === 1 ? "q" : "y", "w", t > 0.12 && t < 0.88)
  }
})

// k_highcut: a tall crescent that sweeps up through, then its tail catches up
anim("k_highcut_fx", { w: S, h: S, frames: 8, fps: 20 }, (c) => {
  const cx = -9, cy = 16, r = 27, A0 = -37, A1 = 37
  const head = c.frame <= 4 ? A0 + (c.frame / 4) * (A1 - A0) : A1
  const tail = c.frame <= 4 ? A0 : A0 + (c.frame - 4) / 3 * (A1 - A0) * 0.95
  if (head - tail < 2) return
  // a thick crescent: dark rim, blue body, light edge, white core on its outer curve
  c.arc(cx, cy, r + 1, tail, head, "b", { inner: r - 13, taper: true })
  c.arc(cx, cy, r, tail, head, "B", { inner: r - 11, taper: true })
  c.arc(cx, cy, r, tail, head, "c", { inner: r - 6, taper: true })
  c.arc(cx, cy, r - 0.5, tail, head, "w", { inner: r - 3, taper: true })
})

// k_knead: a flat shock ring spreading over the floor, dust thrown off both sides
anim("k_knead_fx", { w: S, h: S, frames: 6, fps: 18 }, (c) => {
  const f = c.frame, gy = 28, rx = 4 + f * 2.4, ry = 1 + f * 0.5
  if (f < 5) {
    // a double ring: dark under it, light on top, white inside it while it is strong
    c.ellipse(16, gy + 1, R(rx), R(ry), "b", true); c.ellipse(16, gy + 1, R(rx) - 1, Math.max(1, R(ry) - 1), "b", true)
    c.ellipse(16, gy, R(rx), R(ry), f < 3 ? "w" : "c", true)
    if (R(ry) >= 2) c.ellipse(16, gy, R(rx) - 1, R(ry) - 1, f < 3 ? "c" : "B", true)
  }
  for (const s of [-1, 1]) {
    const px = 16 + s * R(rx * 0.85), py = gy - 1 - Math.min(f, 3) * 2 + (f > 3 ? (f - 3) * 2 : 0), rr = f < 4 ? 2 : 1
    c.ellipse(px, py + 1, rr, rr, "m"); c.ellipse(px, py, rr, rr, "l"); if (f < 4) c.px(px - 1, py - 1, "w")
  }
  if (f < 3) for (const s of [-1, 1]) c.px(16 + s * (3 + f * 3), gy - 5 - f, "y")
})

// k_leap (dash start / end): soft smoke puffs that swell, then thin and drift
anim("k_leap_fx", { w: S, h: S, frames: 6, fps: 16 }, (c) => {
  const f = c.frame
  for (const [bx, by, d] of [[9, 22, 0], [16, 19, 1], [23, 22, 0]]) {
    const t = Math.min(1, (f + d) / 5), r = R(2 + Math.sin(t * Math.PI * 0.8) * 4), x = bx + R((bx - 16) * t * 0.3), y = by - R(t * 4)
    if (r <= 1 && f > 2) continue
    c.ellipse(x, y + 1, r + 1, r + 1, "k"); c.ellipse(x, y + 1, r, r, "n"); c.ellipse(x, y, r, r, "l")
    if (r > 2) c.ellipse(x - 1, y - 1, r - 2, r - 2, "w")
  }
})

// k_night: a bright twinkle in the cats eye
anim("k_night_fx", { w: S, h: S, frames: 8, fps: 12 }, (c) => {
  const f = c.frame, r = [0, 1, 2, 3, 3, 2, 1, 0][f]
  if (r === 0) return
  star(c, 16, 16, r, "y", "w")
  if (f === 3 || f === 4) { c.px(16 + 5, 16 - 4, "w"); c.px(16 - 5, 16 + 4, "y") }
})

// k_paw: a gold impact burst on the blade, rays then sparks
anim("k_paw_fx", { w: S, h: S, frames: 6, fps: 20 }, (c) => {
  const f = c.frame, cx = 16, cy = 16
  if (f < 3) c.ellipse(cx, cy, 3 - f + 1, 3 - f + 1, "w")
  for (let i = 0; i < 8; i++) {
    const a = i * TAU / 8 + 0.2, r0 = 3 + f * 2, r1 = r0 + (f < 4 ? 5 - f : 2)
    const x0 = R(cx + Math.cos(a) * r0), y0 = R(cy + Math.sin(a) * r0), x1 = R(cx + Math.cos(a) * r1), y1 = R(cy + Math.sin(a) * r1)
    c.line(x0, y0, x1, y1, i % 2 ? "y" : "o"); if (f < 3) c.px(x1, y1, "w")
  }
})

// k_rush: speed streaks trailing behind (to the left), dust at the feet
anim("k_rush_fx", { w: S, h: S, frames: 6, fps: 18 }, (c) => {
  const f = c.frame
  for (const [y, len, d] of [[10, 14, 0], [15, 18, 2], [20, 12, 1], [25, 15, 3]]) {
    const head = 24 - ((f + d) % 6) * 2, L = Math.max(3, len - ((f + d) % 6) * 2)
    c.hline(head - L, y, L, "B"); c.hline(head - L + 2, y, L - 2, "c"); c.px(head - 1, y, "w"); c.px(head - 2, y, "w")
    c.hline(head - L, y - 1, L - 3, "b")
  }
  for (const [x, d] of [[20, 0], [12, 3]]) {
    const t = ((f + d) % 6) / 5, rr = R(1 + t * 2)
    c.ellipse(x - R(t * 6), 28 - R(t * 3), rr, rr, "l"); c.px(x - R(t * 6) - 1, 27 - R(t * 3), "w")
  }
})

// k_whisker: six whisker lines flash out of the cat, a spark at each tip
anim("k_whisker_fx", { w: S, h: S, frames: 6, fps: 18 }, (c) => {
  const f = c.frame, cx = 16, cy = 16, L = [3, 8, 12, 12, 9, 6][f]
  if (f < 2) star(c, cx, cy, 3 - f, "c", "w")
  for (const s of [-1, 1]) for (const dy of [-6, -1, 5]) {
    const x1 = cx + s * (3 + L), y1 = cy + R(dy * (L / 12))
    c.line(cx + s * 3, cy + R(dy * 0.15), x1, y1, "k"); c.line(cx + s * 3, cy + R(dy * 0.15), x1, y1, f < 4 ? "w" : "c")
    if (f > 0 && f < 5) c.px(x1, y1, "y")
  }
})

// a_backstep: an electric muzzle flash thrown to the right (the game flips it left), sparks flying off
anim("a_backstep_fx", { w: S, h: S, frames: 6, fps: 20 }, (c) => {
  const f = c.frame, L = [6, 12, 16, 14, 10, 6][f], h = [3, 5, 5, 4, 3, 1][f], x0 = 8
  c.poly([x0, 16 - h - 1, x0 + L + 1, 16, x0, 16 + h + 1], "b")
  c.poly([x0, 16 - h, x0 + L, 16, x0, 16 + h], f < 4 ? "c" : "B")
  c.poly([x0, 16 - Math.max(1, h - 2), x0 + L - 3, 16, x0, 16 + Math.max(1, h - 2)], "w")
  for (let i = 0; i < 6; i++) {
    const a = (rnd01(i, 1) - 0.5) * 1.6, d = 4 + f * 3 + rnd01(i, 2) * 6
    if (f < 5) c.px(R(x0 + 6 + Math.cos(a) * d), R(16 + Math.sin(a) * d), i % 2 ? "y" : "w")
  }
})

// a_rapid: four chevrons fired one after another, each fading as the next goes
anim("a_rapid_fx", { w: S, h: S, frames: 8, fps: 20 }, (c) => {
  for (let i = 0; i < 4; i++) {
    const age = c.frame - i * 1.5
    if (age < 0 || age > 3.5) continue
    const x = 5 + i * 6, key = age < 1.5 ? "w" : age < 2.5 ? "c" : "B"
    for (let d = 0; d <= 3; d++) { c.px(x + d, 16 - 3 + d, "k"); c.px(x + d, 16 + 3 - d, "k") }
    for (let d = 0; d <= 3; d++) { c.px(x + d - 1, 16 - 3 + d, key); c.px(x + d - 1, 16 + 3 - d, key) }
  }
})

// a_wire: a lightning zigzag along the wire (horizontal), crackling frame to frame
anim("a_wire_fx", { w: S, h: S, frames: 6, fps: 14 }, (c) => {
  let px = 1, py = 16
  for (let i = 1; i <= 10; i++) {
    const x = 1 + i * 3, y = i === 10 ? 16 : 16 + R((rnd01(i, c.frame) - 0.5) * 8)
    c.line(px, py + 1, x, y + 1, "b"); c.line(px, py, x, y, "y"); px = x; py = y
  }
  for (let i = 0; i < 4; i++) {
    const x = R(4 + rnd01(i, c.frame + 9) * 24), y = 16 + R((rnd01(i, c.frame + 3) - 0.5) * 14)
    c.px(x, y, "w")
  }
})

// a_churu (the thrown tube): a squeeze stick with a white cap and a dab of paste, as one sprite
anim("a_churu_fx", { w: S, h: S, frames: 4, fps: 8 }, (c) => {
  const f = c.frame
  c.rect(5, 12, 20, 8, "k"); c.rect(24, 13, 5, 6, "k")
  c.rect(6, 13, 18, 6, "o"); c.rect(6, 13, 18, 2, "y"); c.rect(6, 17, 18, 2, "O")
  c.rect(6, 13, 4, 6, "l"); c.rect(6, 13, 4, 2, "w"); c.rect(6, 17, 4, 2, "m")
  c.px(13, 14, "w"); c.px(14, 14, "w"); c.px(18, 15, "r"); c.px(19, 15, "r")
  c.rect(24, 14, 4 + (f % 2), 4, "q"); c.px(25, 14, "w"); c.px(25, 15, "w")
  if (f >= 2) c.px(27 + (f - 2), 18 + (f - 2), "q")
})

// rd_heal (48 px, player heal): a pulsing ring on the ground, pluses and hearts rising in a soft column
anim("rd_heal", { w: 48, h: 48, frames: 12, fps: 16 }, (c) => {
  const f = c.frame, pulse = f < 6 ? f : 12 - f
  c.ellipse(24, 43, 13 + pulse, 3 + (pulse > 3 ? 1 : 0), "G", true)
  c.ellipse(24, 42, 13 + pulse, 3 + (pulse > 3 ? 1 : 0), "g", true)
  for (let y = 14; y < 42; y += 2) for (const x of [12, 36]) if (((y >> 1) + f) % 3 === 0) c.px(x + (x < 24 ? 1 : -1), y, "g")
  for (let i = 0; i < 5; i++) {
    const t = ((f / 12) + i / 5) % 1, x = 13 + R(rnd01(i, 4) * 22), y = R(40 - t * 32)
    if (i % 2) {
      c.px(x, y - 1, "r"); c.px(x - 2, y - 1, "r"); c.px(x + 2, y - 1, "r"); c.px(x - 1, y, "q"); c.px(x + 1, y, "q"); c.px(x, y + 1, "r")
      c.px(x - 1, y - 2, "q"); c.px(x + 1, y - 2, "q"); c.px(x - 2, y - 2, "r"); c.px(x + 2, y - 2, "r")
    } else {
      for (let d = -2; d <= 2; d++) { c.px(x + d, y, "k"); c.px(x, y + d, "k") }
      for (let d = -1; d <= 1; d++) { c.px(x + d, y, "g"); c.px(x, y + d, "g") }
      c.px(x, y, "w")
    }
  }
})
