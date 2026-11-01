// Enemy role icons for Cats Smash (shown over an enemy instead of the words 추격! / 원거리! / 단단함! / 돌진!).
// node tools/role-icons.mjs <out dir> -> role_<name>.png (16 x 16), drawn from the maps below.
import { writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { encodePNG } from "./lib/png.mjs";

const out = process.argv[2];
mkdirSync(out, { recursive: true });
const C = {
  "#": [34, 16, 20, 255],    // outline
  r: [214, 58, 52, 255], R: [255, 120, 96, 255],   // chaser red
  o: [230, 140, 40, 255], O: [255, 210, 110, 255], // ranged orange
  b: [80, 120, 170, 255], B: [160, 200, 235, 255], // tank steel
  y: [240, 190, 40, 255], Y: [255, 240, 150, 255], // ambusher yellow
  w: [255, 250, 240, 255],
};
const icons = {
  // a speech bubble with "!": it has seen you and is coming
  chaser: [
    "................",
    "...##########...",
    "..#RRRRRRRRRR#..",
    ".#RrrrrwwrrrrR#.",
    ".#rrrrrwwrrrrr#.",
    ".#rrrrrwwrrrrr#.",
    ".#rrrrrwwrrrrr#.",
    ".#rrrrrwwrrrrr#.",
    ".#rrrrrrrrrrrr#.",
    ".#rrrrrwwrrrrr#.",
    "..#rrrrwwrrrr#..",
    "...####rr####...",
    "......#rr#......",
    ".......##.......",
    "................",
    "................",
  ],
  // a target: it shoots from afar
  ranged: [
    "................",
    ".....######.....",
    "...##OOOOOO##...",
    "..#OOoooooooO#..",
    "..#Oo######oO#..",
    ".#Oo#OOOOOO#oO#.",
    ".#Oo#O####O#oO#.",
    ".#Oo#O#ww#O#oO#.",
    ".#Oo#O#ww#O#oO#.",
    ".#Oo#O####O#oO#.",
    ".#Oo#OOOOOO#oO#.",
    "..#Oo######oO#..",
    "..#OOoooooooO#..",
    "...##OOOOOO##...",
    ".....######.....",
    "................",
  ],
  // a shield: it takes a beating
  tank: [
    "................",
    "..############..",
    "..#BBBBBBBBBB#..",
    "..#BbbbbbbbbB#..",
    "..#Bbbbbbbbbb#..",
    "..#Bbbb##bbbb#..",
    "..#Bbb#BBw#bb#..",
    "..#Bbb#BBB#bb#..",
    "..#Bbbb##bbbb#..",
    "...#bbbbbbbb#...",
    "...#bbbbbbbb#...",
    "....#bbbbbb#....",
    ".....#bbbb#.....",
    "......####......",
    "................",
    "................",
  ],
  // a double chevron: it charges in
  ambusher: [
    "................",
    "................",
    ".###....###.....",
    ".#YY#...#YY#....",
    "..#yY#...#yY#...",
    "...#yY#...#yY#..",
    "....#yY#...#yY#.",
    ".....#yw#...#yw#",
    ".....#yw#...#yw#",
    "....#yY#...#yY#.",
    "...#yY#...#yY#..",
    "..#yY#...#yY#...",
    ".#YY#...#YY#....",
    ".###....###.....",
    "................",
    "................",
  ],
};
for (const [name, rows] of Object.entries(icons)) {
  const px = new Uint8ClampedArray(16 * 16 * 4);
  rows.forEach((row, y) => [...row].forEach((ch, x) => { if (C[ch]) px.set(C[ch], (y * 16 + x) * 4); }));
  writeFileSync(join(out, `role_${name}.png`), encodePNG(16, 16, px));
}
console.log("ok", Object.keys(icons).join(" "));
