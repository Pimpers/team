// The boss bear keeps its original art (Assets/Enemies/Bear, cut from the 8-pose sheet); this file only fixes the motion:
//  - bear_move: the old "walk" was the two idle frames over again. It is now a 4-beat prowl built from the idle frame:
//    the body bobs, and each leg swings from its hip (every row of the leg is moved a little more than the row above, so
//    the foot travels and the hip stays), the swinging foot lifts off the floor.
//  - bear_attack: the four attack frames sat 3 px above the floor and 2 px off to the side of the idle bear, so every
//    swing hopped; they are re-seated on the idle bear's feet (the frames themselves are untouched).
// Frames are 64x64, no outline pass: the output strips are cut straight into bear_move_N.png / bear_attack_N.png.
const SRC = "src/"
const W = 64, HIP = 46, FOOT = 63

// the idle frame, as a body (rows above the hips) and one 1-px row per leg row and leg group
sprite("idle0", { png: SRC + "bear_idle_0.png" })
sprite("_top", { png: SRC + "bear_idle_0.png", area: [0, 0, W, HIP] })
sprite("_hipfill", { png: SRC + "bear_idle_0.png", area: [0, HIP - 1, W, 1] })   // fills the row the bobbing body leaves open above the hips
// leg groups by column: hind (x 0-14), middle (15-35), front (36-63)
const GROUPS = { hind: [0, 15], mid: [15, 21], front: [36, 28] }
for (const [name, [x, w]] of Object.entries(GROUPS))
  for (let y = HIP; y <= FOOT; y++) sprite(`_${name}_${y}`, { png: SRC + "bear_idle_0.png", area: [x, y, w, 1] })

const TAU = Math.PI * 2
// reach (px the foot moves) and lift (px the foot rises) for a leg at phase p (0..1): forward on the way up, back on the ground
const swing = (p, reach, lift) => {
  const a = p * TAU
  return { dx: Math.sin(a) * reach, up: Math.max(0, Math.cos(a)) * lift }
}

function leg(c, name, phase, reach, lift) {
  const [x] = GROUPS[name], { dx, up } = swing(phase, reach, lift)
  for (let y = HIP; y <= FOOT; y++) {
    const t = (y - HIP) / (FOOT - HIP)          // 0 at the hip, 1 at the foot
    const sx = Math.round(dx * t), sy = -Math.round(up * t)
    c.stamp(`_${name}_${y}`, x + sx, y + sy)
  }
}

anim("bear_move", { w: W, h: 64, frames: 4, fps: 6 }, (c) => {
  const p = c.frame / 4
  const bob = (c.frame % 2) ? -1 : 0
  c.stamp("_hipfill", 0, HIP - 1)
  c.stamp("_top", 0, bob)
  leg(c, "hind", p, 4, 4)
  leg(c, "mid", p + 0.5, 4, 4)
  leg(c, "front", p, 5, 4)
})

// attack frames, re-seated: +2 px right, +3 px down
for (let i = 0; i < 4; i++) sprite(`atk${i}`, { png: SRC + `bear_attack_${i}.png` })
anim("bear_attack", { w: W, h: 64, frames: 4, fps: 7 }, (c) => { c.stamp(`atk${c.frame}`, 2, 3) })
