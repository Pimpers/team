// The boss bear (Assets/Enemies/Bear), drawn in pxl as a posed rig: one body, a 4-beat prowl, a rear-up claw swipe,
// a hurt flinch and a three-frame fall. Faces right, 64x64 cells (death 80x64), feet on the bottom row, flat fur ramp,
// dark outline, a red open mouth and white chest / claws. repixel_bear.ps1 is replaced by this (it ate the mouth).
pal({
  k: "#120b1a", a: "#24192e", b: "#38294a", c: "#4f3d64", d: "#6c5a84", e: "#8f7ca8",
  w: "#f4eee2", g: "#b9b2c8", r: "#d0384c", R: "#7a1830", p: "#f08a96", y: "#ff5a4a",
})
const TAU = Math.PI * 2, R = Math.round

// pose: dx, dy shift everything; tilt rotates the body about (px, py); head: extra head rotation about the neck
function bearRig(c, o) {
  const P = { dx: 0, dy: 0, tilt: 0, px: 14, py: 62, head: 0, mouth: 0, hx: 0, hy: 0, shut: false, ...o }
  const rot = (x, y, a, cx, cy) => { const s = Math.sin(a), k = Math.cos(a), X = x - cx, Y = y - cy; return [cx + X * k - Y * s, cy + X * s + Y * k] }
  const body = (x, y) => { const [X, Y] = rot(x, y, P.tilt, P.px, P.py); return [X + P.dx, Y + P.dy] }
  const headT = (x, y) => { const [X, Y] = rot(x + P.hx, y + P.hy, P.head, 44, 32); return body(X, Y) }
  const poly = (pts, col, tr = body) => c.poly(pts.flatMap(([x, y]) => { const [X, Y] = tr(x, y); return [R(X), R(Y)] }), col)
  const ell = (cx, cy, rx, ry, col, tr = body, rot0 = 0) => {
    const n = 26, pts = []
    for (let i = 0; i < n; i++) { const a = i * TAU / n; pts.push(rot(cx + Math.cos(a) * rx, cy + Math.sin(a) * ry, rot0, cx, cy)) }
    poly(pts, col, tr)
  }
  const px = (x, y, col, tr = body) => { const [X, Y] = tr(x, y); c.px(R(X), R(Y), col) }
  // a tapered leg from (x0,y0) to (x1,y1) with a paw and three claws
  const leg = (x0, y0, x1, y1, w0, w1, col, hi, kneeShift = 0, round = false) => {
    const kx = (x0 + x1) / 2 + kneeShift, ky = (y0 + y1) / 2
    const seg = (ax, ay, bx, by, wa, wb) => {
      const dx = bx - ax, dy = by - ay, L = Math.hypot(dx, dy) || 1, nx = -dy / L, ny = dx / L
      poly([[ax + nx * wa / 2, ay + ny * wa / 2], [bx + nx * wb / 2, by + ny * wb / 2], [bx - nx * wb / 2, by - ny * wb / 2], [ax - nx * wa / 2, ay - ny * wa / 2]], col)
    }
    seg(x0, y0, kx, ky, w0, (w0 + w1) / 2 + 1); seg(kx, ky, x1, y1, (w0 + w1) / 2 + 1, w1)
    ell(kx, ky, (w0 + w1) / 4 + 1, (w0 + w1) / 4 + 1, col)
    const pr = w1 / 2 + 1.5
    ell(x1 + 1, y1, pr, round ? pr : 2.6, col)                    // paw
    px(x0 - 1, y0 + 1, hi)
    if (round) { for (const dx of [-3, -1, 1, 3]) { px(x1 + 1 + dx, y1 - pr - 1, "w"); px(x1 + 1 + dx, y1 - pr - 2, "w") } }   // a raised paw shows its claws on top
    else for (const cx of [x1 + 3, x1 + 5, x1 + 7]) { px(cx, y1 + 1, "w"); px(cx, y1 + 2, "w") }   // claws on the floor
  }
  const FRONT_FAR = P.ff, FRONT_NEAR = P.fn, HIND_FAR = P.hf, HIND_NEAR = P.hn
  // far legs first (darkest)
  leg(34, 40, HIND_FAR ? HIND_FAR[0] : 22, HIND_FAR ? HIND_FAR[1] : 62, 9, 6, "a", "b", -2)
  leg(36, 40, FRONT_FAR ? FRONT_FAR[0] : 38, FRONT_FAR ? FRONT_FAR[1] : 62, 9, 6, "a", "b", P.fk === undefined ? 1 : P.fk)
  // torso: rump, belly, hump
  ell(30, 35, 21, 14, "b")
  ell(13, 37, 11, 11, "b")
  ell(37, 28, 13, 12, "c")
  ell(24, 28, 12, 7, "c")                                         // back
  ell(34, 22, 9, 4, "d")                                          // light along the top of the hump
  ell(22, 42, 15, 5, "a")                                         // belly shade
  for (const [x, y] of [[8, 30], [16, 25], [27, 21], [44, 22], [30, 26], [20, 33]]) px(x, y, "d")
  for (const [x, y] of [[12, 44], [26, 46], [36, 44]]) px(x, y, "a")
  // chest patch
  poly([[41, 36], [49, 35], [47, 40], [44, 43], [41, 41]], "w")
  poly([[41, 41], [44, 43], [47, 40], [45, 43], [43, 45]], "g")
  // near legs
  leg(16, 41, HIND_NEAR ? HIND_NEAR[0] : 12, HIND_NEAR ? HIND_NEAR[1] : 62, 11, 7, "c", "d", -2)
  leg(44, 40, FRONT_NEAR ? FRONT_NEAR[0] : 44, FRONT_NEAR ? FRONT_NEAR[1] : 62, P.fw ? P.fw[0] : 11, P.fw ? P.fw[1] : 7, P.fc || "c", "e", P.fk === undefined ? 1 : P.fk, !!P.fr)
  // head (turned with headT): ears, skull, muzzle, jaw, eye
  ell(47, 22, 3.4, 3.4, "a", headT); ell(55, 22, 3.4, 3.4, "a", headT); ell(47, 22, 1.8, 1.8, "c", headT)
  ell(51, 30, 10, 9, "c", headT)
  ell(50, 25, 6, 3, "d", headT)
  if (P.mouth > 0) {
    // open jaw: dark red inside, white teeth, a tongue
    const m = P.mouth
    ell(56, 37 + m * 0.6, 7, 3 + m * 0.5, "b", headT)           // lower jaw
    poly([[52, 34], [62, 34], [61, 36 + m], [55, 38 + m * 1.2]], "R", headT)
    poly([[54, 35], [60, 35], [59, 36 + m * 0.8], [56, 37 + m]], "r", headT)
    px(55, 36 + m * 0.7, "p", headT)
    for (const x of [53, 55, 57, 59, 61]) px(x, 35, "w", headT)
    for (const x of [54, 58, 60]) px(x, 36 + m, "w", headT)
  }
  ell(57, 33, 6, 4.5, "d", headT)                                // muzzle
  if (P.mouth > 0) { poly([[51, 34], [63, 33], [62, 35], [52, 36]], "R", headT); for (const x of [53, 56, 59, 62]) px(x, 35, "w", headT) }
  else { for (let x = 53; x <= 61; x++) px(x, 36, "k", headT) }
  poly([[60, 29], [64, 30], [64, 33], [61, 33]], "k", headT)      // nose
  px(61, 30, "g", headT)
  if (P.shut) { for (let x = 51; x <= 54; x++) px(x, 28, "k", headT) }
  else { px(53, 27, "y", headT); px(54, 27, "y", headT); px(53, 28, "R", headT); px(52, 26, "k", headT); px(53, 26, "k", headT); px(54, 26, "k", headT); px(55, 26, "k", headT) }
  px(48, 28, "c", headT)
}

const WALK_STEPS = 4
const swing = (phase, off, reach, lift) => {
  const a = (phase + off) * TAU
  return [R(Math.sin(a) * reach), Math.max(0, Math.cos(a)) * lift]
}
function stance(c, phase, extra = {}) {
  const fn = swing(phase, 0, 5, 4), ff = swing(phase, 0.5, 5, 4), hn = swing(phase, 0.5, 4, 4), hf = swing(phase, 0, 4, 4)
  const bob = -Math.round(Math.abs(Math.sin(phase * TAU)) * 1.2)
  bearRig(c, {
    dy: bob, head: Math.sin(phase * TAU * 2) * 0.03,
    fn: [44 + fn[0], 62 - Math.round(fn[1])], ff: [38 + ff[0], 62 - Math.round(ff[1])],
    hn: [12 + hn[0], 62 - Math.round(hn[1])], hf: [22 + hf[0], 62 - Math.round(hf[1])], ...extra,
  })
}

anim("bear_idle", { w: 64, h: 64, frames: 2, fps: 2.5, outline: "k" }, (c) => {
  const up = c.frame === 1
  bearRig(c, { dy: up ? -1 : 0, head: up ? -0.04 : 0.02, mouth: 0, hn: [12, 62], hf: [22, 62], fn: [44, 62], ff: [38, 62] })
})

anim("bear_move", { w: 64, h: 64, frames: 4, fps: 6, outline: "k" }, (c) => { stance(c, c.frame / 4, { mouth: 0 }) })

// attack: crouch and gather (0), rear up with the claws raised (1), the swipe comes down and forward (2, the hit), recover (3)
anim("bear_attack", { w: 64, h: 64, frames: 4, fps: 7, outline: "k" }, (c) => {
  const f = c.frame
  if (f === 0) bearRig(c, { dx: -2, dy: 2, tilt: 0.04, head: 0.12, mouth: 1, fk: -4, fn: [28, 38], ff: [34, 54], hn: [10, 62], hf: [20, 62] })
  else if (f === 1) bearRig(c, { dx: -1, dy: 0, tilt: -0.2, head: -0.12, mouth: 3, px: 10, fk: -7, fc: "d", fr: true, fw: [13, 10], fn: [57, 17], ff: [47, 25], hn: [10, 62], hf: [20, 62] })
  else if (f === 2) bearRig(c, { dx: 3, dy: 2, tilt: 0.1, head: 0.06, mouth: 4, px: 12, fk: -1, fc: "d", fn: [55, 54], ff: [40, 28], hn: [12, 62], hf: [22, 62] })
  else bearRig(c, { dx: 1, dy: 1, tilt: 0.03, head: 0.04, mouth: 1, fn: [56, 60], ff: [40, 62], hn: [12, 62], hf: [22, 62] })
})

anim("bear_hurt", { w: 64, h: 64, frames: 1, fps: 4, outline: "k" }, (c) => {
  bearRig(c, { dx: -3, dy: 1, tilt: -0.1, head: -0.25, mouth: 2, shut: true, hn: [10, 62], hf: [20, 62], fn: [42, 60], ff: [36, 62] })
})

// death (80x64): staggers back (0), drops onto its belly with the head still up (1), flat out with the eyes shut (2)
function lying(c, k) {
  const ell = (cx, cy, rx, ry, col) => { const n = 28, pts = []; for (let i = 0; i < n; i++) { const a = i * TAU / n; pts.push(R(cx + Math.cos(a) * rx), R(cy + Math.sin(a) * ry)) } c.poly(pts, col) }
  const flat = k === 2, top = flat ? 50 : 46, ry = flat ? 9 : 12, headY = flat ? 56 : 50
  // far legs, then the body, then the near legs and the head
  ell(24, 60, 5, 3, "a"); ell(58, 61, 6, 3, "a")
  ell(34, top + 6, 26, ry, "b"); ell(18, top + 7, 12, ry - 1, "b")
  ell(36, top + 1, 17, ry - 5, "c"); ell(34, top - 3, 10, 3, "d")
  ell(32, top + 12, 20, 3, "a")
  for (const [x, y] of [[14, top + 2], [26, top - 4], [44, top - 2]]) c.px(x, y, "d")
  // near hind leg folded under, near front leg stretched out ahead of the head
  ell(14, 59, 8, 4, "c"); ell(14, 61, 5, 2, "b"); for (const x of [7, 9, 11]) { c.px(x, 62, "w"); c.px(x, 63, "w") }
  ell(60, 60, 10, 3, "c"); for (const x of [67, 69, 71]) { c.px(x, 61, "w"); c.px(x, 62, "w") }
  // head resting on its paws
  ell(58, headY, 10, 8, "c"); ell(57, headY - 4, 6, 3, "d"); ell(51, headY - 7, 3, 3, "a")
  ell(67, headY + 3, 6, 4.5, "d"); c.poly([69, headY + 1, 73, headY + 2, 73, headY + 5, 70, headY + 5], "k")
  if (k === 0) { c.px(60, headY - 2, "y"); c.px(61, headY - 2, "y") } else for (let x = 58; x <= 62; x++) c.px(x, headY - 1, "k")
  for (let x = 62; x <= 70; x++) c.px(x, headY + 6, "k")
  c.poly([40, top, 47, top - 1, 45, top + 4, 42, top + 6, 39, top + 4], "w")   // the white chest shows from the side
}
anim("bear_death", { w: 80, h: 64, frames: 3, fps: 3, loop: false, outline: "k" }, (c) => {
  const f = c.frame
  if (f === 0) bearRig(c, { dx: 10, dy: 0, tilt: -0.32, px: 22, py: 62, head: -0.18, mouth: 3, fk: -4, fc: "d", fn: [52, 24], ff: [46, 30], hn: [18, 62], hf: [28, 62] })
  else lying(c, f)
})
