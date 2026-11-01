// The demon spider's spit (cats-smash): a ball of web that flies at the cat, and the web left stuck on the cat
// (it slows it down for a moment). Silk white with a lavender shade and a dark plum edge so it reads over the
// bright sky and the dark ground alike. 16 px per unit like the enemies.
pal({ k: "#3a2546", w: "#f4f0fb", l: "#c9bce3", m: "#9a86c2", p: "#e86aa8" })

// Flying: a wobbling knot of web with strands trailing behind (it flies to the right; flipped in game).
sprite("web_ball_0", { w: 12, h: 9 }, `
  ............
  .......kkk..
  ..k...kwwwk.
  .kl.kkwlwwwk
  kllmlwwwlwwk
  .kl.kkwwwlk.
  ..k...kwwwk.
  .......kkk..
  ............`)
sprite("web_ball_1", { w: 12, h: 9 }, `
  ............
  .......kkk..
  .k....kwwlk.
  k.l.kkwwwwwk
  .lmllwlwwwwk
  k.l.kkwwlwk.
  .k....kwwwk.
  .......kkk..
  ............`)

// Stuck on the cat: a ragged net over the body.
sprite("web_stuck", { w: 16, h: 14 }, `
  ....k......k....
  ...kwk....kwk...
  ..kw.wkkkkw.wk..
  .kw..lw..wl..wk.
  kw..kwlwwlwk..wk
  .w.kw..ww..wk.w.
  .lkw..kwwk..wkl.
  .klwwwlkklwwwlk.
  .lkw..kwwk..wkl.
  .w.kw..ww..wk.w.
  kw..kwlwwlwk..wk
  .kw..lw..wl..wk.
  ..kw.wkkkkw.wk..
  ...k........k...`)

sheet("web_preview", ["web_ball_0", "web_ball_1", "web_stuck"], { pad: 2 })
