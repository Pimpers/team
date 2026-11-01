Effect sprites used by the game (see Assets/Editor/VfxAssets.cs for how they are sliced).

DryRain/  - "DryRain's Pixel Platformer VFX General Pack 1" by DryRainEnt
            https://dryrainent.itch.io/ppvfx-general-1
            Free for personal and commercial projects, modification allowed, no reselling the pack.
            32x32 frames, hand-drawn (the author states no generative AI was used).

BDragon/  - "750 Effect and FX Pixel All" (free part) by BDragon1727
            https://bdragon1727.itch.io/750-effect-and-fx-pixel-all
            FREE licence: free to use on non-commercial games; for a commercial game, please contribute
            (any amount) on the page. Modifying is allowed. Do NOT resell or redistribute the files
            (so don't put this folder in a public repository or share it on its own).
            64x64 hand-drawn pixel art. Each file here is ONE colour row cropped from the pack's
            9-colour sheet, renamed to the effect id it plays as (e.g. slash.png = sheet 589, row 2).
            Rows: 0 orange, 1 pink, 2 cyan, 3 green, 4 brown, 5 white, 6 mauve, 7 red, 8 indigo.
              slash 589/2   slash_gold 589/0  xcut 232/5     heal 485/3      rupture 702/4
              explosion 79/0 parry 586/2      blink 13/8     charge 711/2    poison_hit 519/3
              poison_loop 335/3 zap 125/2     trap_snap 283/0 lure 296/1     wind 387/5
              land_dust 466/4 tower_slam 467/4 beam 578/0    beam_end 293/0  stun 315/0
              hiss_wave 26/7 charm 486/1      aura_hit 230/0 yarn_hit 612/1  smoke 476/5
              sparkle 474/1  watch 439/2
            Imported at the cats' pixel size (12.5 px/unit) and played only at x0.5 / x1 / x2 and in
            90 degree turns (VfxLibrary "pixelSnap"), so their pixels stay whole.

Custom/   - Skill props no pack had (fishbone arrow, yarn ball, can, churu, mouse trap, "!" mark,
            heart, bell, eye glint). Drawn by generate_skill_sprites.ps1 in the project root,
            16x16 pixel art made for this game. status_stun / status_poison (ids "stun" / "poison_loop",
            24 px, 12 frames each) are drawn by generate_status_fx.ps1 the same way.

RetroDiffusion/ - Generated with Retro Diffusion (retrodiffusion.ai, style "rd_animation__vfx") on the
            project owner's account, with a 32-colour palette taken from the game's tiles and cats
            (input_palette), then cut from the 4x4 grid into strips. Check Retro Diffusion's terms before
            a commercial release.
              rd_stun   (32 px, 16 frames) - no longer used: replaced by Custom/status_stun
              rd_heal   (id "heal",        48 px, 16 frames) - player healing glow
              rd_poison (32 px,  8 frames) - no longer used: replaced by Custom/status_poison

SpriteFusion/ - Generated with Sprite Fusion's pixel art generator (spritefusion.com, operation "generate", 16 px)
            on the project owner's account; 1 of 12 variations kept. Check Sprite Fusion's terms before a
            commercial release.
              sf_fishbone (id "sf_fishbone", 16 px) - the 생선가시 화살 arrow

Pimen/ - pimen, "Battle VFX: Slashes and Thrusts" free sample (Smear VFX 01): https://pimen.itch.io/battle-vfx-slashes-and-thrusts
  Licence: personal and commercial use, may be modified; credit not required but appreciated; no resale / redistribution.
  smear_arc(_gold) = "Smear 01 Horizontal 1/2", smear_thrust(_gold) = "Smear 01 Vertical 01/02" (renamed, pixels unchanged).
  Used for the knight's basic 3-hit combo (PlayerAttack.SwingEffect).

pimen - Thunder Spell Effect 01, Wind Spell Effect 01, Acid Spell Effect (VFX 01 free part)
  https://pimen.itch.io/thunder-spell-effect-01  https://pimen.itch.io/wind  https://pimen.itch.io/acid-spell-effect
  Free downloads (name your own price). Use and modify for personal and commercial projects; credit appreciated; no resale/redistribution.
  Used as Effects/Pimen/p_thunder_bolt, p_thunder_hit, p_wind_breath, p_wind_hit (re-laid into a strip), p_acid_hit (the Hit frames).
