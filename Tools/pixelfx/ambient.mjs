// Looping ambience for Cats Smash's world map and village: each pixelfx loop rendered to transparent frames
// (<out>/<name>/<name>_NN.png, cropped to the loop's overall bounds so every frame shares one size and pivot).
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { loadEffect, renderFrames } from "./lib/fxlib.mjs";
import { encodePNG } from "./lib/png.mjs";
import { PAL } from "../src/core/color.js";

const out = process.argv[2];
const list = [
  // world map
  ["fireflies", "weather/fireflies", {}],
  ["volcano", "fire/torch", {}],
  ["stormcloud", "lightning/storm-cloud", {}],
  ["snow", "weather/snow", {}],
  ["whirlpool", "water/whirlpool", {}],
  ["clouds", "weather/clouds", {}],
  // village
  ["campfire", "fire/campfire", {}],
  ["chimney", "nature/poison-cloud", { palette: PAL.smoke }],
  ["birds", "critters/bird", {}],
  ["ripples", "water/ripples", {}],
];

for (const [name, id, options] of list) {
  const fx = await loadEffect(id);
  const r = renderFrames(fx, { bg: "none", options, fps: ["fireflies", "snow", "clouds"].includes(name) ? 6 : 12 });
  // one box round every frame, so the loop doesn't jitter
  let x0 = r.w, y0 = r.h, x1 = -1, y1 = -1;
  for (const f of r.frames) for (let y = 0; y < r.h; y++) for (let x = 0; x < r.w; x++)
    if (f.clear[(y * r.w + x) * 4 + 3] > 20) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  if (x1 < 0) { console.log(name, "empty"); continue; }
  const w = x1 - x0 + 1, h = y1 - y0 + 1;
  mkdirSync(join(out, name), { recursive: true });
  r.frames.forEach((f, i) => {
    const px = new Uint8ClampedArray(w * h * 4);
    for (let y = 0; y < h; y++) px.set(f.clear.subarray(((y0 + y) * r.w + x0) * 4, ((y0 + y) * r.w + x0 + w) * 4), y * w * 4);
    if (name === "fireflies") for (let i = 3; i < px.length; i += 4) if (px[i] < 90) px[i] = 0; // no glow haze box
    writeFileSync(join(out, name, `${name}_${String(i).padStart(2, "0")}.png`), encodePNG(w, h, px));
  });
  console.log(name, id, `${r.n} frames`, `${w}x${h}`, `loop=${fx.meta.loop}`);
}
