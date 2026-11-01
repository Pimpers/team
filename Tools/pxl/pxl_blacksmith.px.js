// Cats Smash: the village blacksmith's dialogue portrait (an orange tabby in a leather apron, hammer on the shoulder).
pal({
  k: "#2a1a1e", O: "#b8622a", o: "#e8913a", y: "#f7c27a", w: "#fff3e0", p: "#e88a9a", g: "#ffffff",
  s: "#c9d4dc", S: "#8b99a6", a: "#7a4a2a", A: "#4f2e1a", b: "#d9b25a",
  m: "#b8c2cc", M: "#6b7682", h: "#8a5a33", c: "#5a4a52",
})
ramps("kOoy", "kSs", "kAab", "kMm")

// Head: left half, mirrored (stripes on the forehead, white muzzle, pink nose).
sprite("_head", { mx: true }, `
................
...kk...........
...kOk..........
...kpOk.........
...kppOk...kkkkk
...kppOOkkkooooo
..koooooooOooooo
..kooOoooooooooo
.kooOooooooooooo
.koooooooooooooo
.kooookgkooooooo
.kooookkkooooooo
.kyooooooooooooo
.kyyoooooowwwwww
.kyyyooowwwwwwwp
.kyyyyowwwwwwwkw
..kyyyyowwwwwwww
...kkyyyowwwwwww
.....kkkyyyyyyyy
........kkkkkkkk`)

// Shoulders: a grey shirt under a leather apron with a brass buckle.
sprite("_body", { mx: true }, `
........kkkkkkkkkkkk
....kssssssskaaaaaaa
...ksssssssskaaaaaaa
..ksSssssssskaaaaaaa
..kSssssssskaaaaabaa
.kSsssssssskaaaaaaaa
.kSssssssskAaaaaaaaa
.kSSsssssskAaaaaaaaa
.kSSssssskAAaaaaaaaa
.kSSSsssskAAaaaaaaaa
.kSSSsssskAAaaaaaaaa
.kSSSsssskAAaaaaaaaa`)

// Hammer resting on the shoulder, head up by the ear.
sprite("_hammer", `
.kkkkk......
kmmmmmk.....
kmmmmMk.....
kMMMMMk.....
.kkkhk......
....khk.....
.....khk....
......khk...
.......khk..
........khk.
.........kk.`)

sprite("blacksmith", { w: 48, h: 34 },
  stamp("_body", 4, 22),
  stamp("_head", 8, 2),
  stamp("_hammer", 1, 4),
  // soot on one cheek, from the forge
  rect(12, 15, 1, 1, "c"), rect(14, 16, 1, 1, "c"))
