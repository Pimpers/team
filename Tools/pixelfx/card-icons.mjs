// Stat-card icons for Cats Smash: one frame of a pixelfx effect per card, cropped and scaled (nearest) to 64 x 64.
// node tools/card-icons.mjs <out dir>   -> <out>/<card>.png and <out>/_preview.png
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { loadEffect, renderFrames } from "./lib/fxlib.mjs";
import { encodePNG } from "./lib/png.mjs";
import { PAL } from "../src/core/color.js";

const out = process.argv[2];
mkdirSync(out, { recursive: true });
const SIZE = 64;
// card id -> effect and its options
const cards = [
  ["attack", "melee/claw-swipe", {}],
  ["speed", "nature/tornado", { palette: PAL.leaf }],
  ["heart", "pickups/heart-pickup", {}],
  ["heal", "nature/healing-bloom", {}],
  ["lifesteal", "holy/heal", { palette: PAL.blood }],
  ["crit", "impacts/crit-hit", {}],
  ["reach", "melee/heavy-cleave", { at: 0.2 }],
  ["haste", "lightning/spark-arc", {}],
  ["dash", "arcane/blink-out", { palette: PAL.shadow }],
  ["dash_more", "status/haste", {}],
  ["guard", "holy/divine-shield", { palette: PAL.wood }],
  ["greed", "pickups/coin-spin", {}],
  ["jump", "spawn/poof", {}],
  ["finisher", "explosions/cartoon-pow", {}],
];

function bbox(rgba, w, h) {
  let x0 = w, y0 = h, x1 = -1, y1 = -1;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (rgba[(y * w + x) * 4 + 3] > 40) {
    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
  }
  return x1 < 0 ? null : { x0, y0, x1, y1 };
}

function icon(rgba, w, h) {
  rgba = rgba.slice();
  for (let i = 3; i < rgba.length; i += 4) rgba[i] = rgba[i] < 150 ? 0 : 255;
  const b = bbox(rgba, w, h);
  const res = new Uint8ClampedArray(SIZE * SIZE * 4);
  if (!b) return res;
  const bw = b.x1 - b.x0 + 1, bh = b.y1 - b.y0 + 1, side = Math.max(bw, bh);
  const k = Math.max(1, Math.floor((SIZE - 4) / side));               // whole-pixel scale when it fits
  const scale = side * k > SIZE - 4 ? (SIZE - 4) / side : k;           // else shrink (nearest)
  const ox = (SIZE - bw * scale) / 2, oy = (SIZE - bh * scale) / 2;
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
    const sx = Math.floor((x - ox) / scale), sy = Math.floor((y - oy) / scale);
    if (sx < 0 || sy < 0 || sx >= bw || sy >= bh) continue;
    const si = ((b.y0 + sy) * w + (b.x0 + sx)) * 4, di = (y * SIZE + x) * 4;
    for (let c = 0; c < 4; c++) res[di + c] = rgba[si + c];
  }
  return res;
}

// A dark 2 px rim round every shape, like the hand-drawn skill icons: without it the glowing effects looked like a
// different set next to them.
function outline(px) {
  const res = px.slice(), R = 2, ink = [34, 16, 20, 255];
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
    if (px[(y * SIZE + x) * 4 + 3] > 0) continue;
    let near = false;
    for (let dy = -R; dy <= R && !near; dy++) for (let dx = -R; dx <= R; dx++) {
      const nx = x + dx, ny = y + dy;
      if (nx >= 0 && ny >= 0 && nx < SIZE && ny < SIZE && px[(ny * SIZE + nx) * 4 + 3] > 0) { near = true; break; }
    }
    if (near) res.set(ink, (y * SIZE + x) * 4);
  }
  return res;
}

const icons = [];
for (const [card, id, opts] of cards) {
  const fx = await loadEffect(id);
  const { seed, at, ...options } = opts;
  const r = renderFrames(fx, { bg: "none", options, ...(seed !== undefined ? { seed } : {}) });
  // the frame with the most solid pixels reads best as a still
  let best = null, score = -1;
  if (at !== undefined) best = r.frames[Math.round(at * (r.frames.length - 1))];
  else for (const f of r.frames) {
    let n = 0;
    for (let i = 3; i < f.clear.length; i += 4) if (f.clear[i] > 128) n++;
    if (n > score) { score = n; best = f; }
  }
  const px = outline(icon(best.clear, r.w, r.h));
  writeFileSync(join(out, card + ".png"), encodePNG(SIZE, SIZE, px));
  icons.push([card, px]);
  console.log(card, id, `${r.w}x${r.h}`, "t=" + best.t.toFixed(2));
}

// preview: all icons at 2x on a dark strip
const k = 2, pad = 8, cell = SIZE * k + pad, W = cell * 7 + pad, H = cell * 2 + pad;
const prev = new Uint8ClampedArray(W * H * 4);
for (let i = 0; i < W * H; i++) prev.set([40, 30, 34, 255], i * 4);
icons.forEach(([, px], n) => {
  const cx = pad + (n % 7) * cell, cy = pad + Math.floor(n / 7) * cell;
  for (let y = 0; y < SIZE * k; y++) for (let x = 0; x < SIZE * k; x++) {
    const si = (Math.floor(y / k) * SIZE + Math.floor(x / k)) * 4, a = px[si + 3] / 255;
    const di = ((cy + y) * W + cx + x) * 4;
    for (let c = 0; c < 3; c++) prev[di + c] = prev[di + c] * (1 - a) + px[si + c] * a;
  }
});
writeFileSync(join(out, "_preview.png"), encodePNG(W, H, prev));
