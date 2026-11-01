# Generates the enemy pixel-art animation frames into Assets/Enemies/<Name>/:
#   White Rat   (rat_*)      - fast, bite        } replaced by the sprite-sheet frames of
#   (see the end of this file)                   } generate_rat_spider_sprites.ps1
#   Demon Spider (spider_*)  - slow, stab
#   Werewolf boss (wolf_*)   - normal speed, claw
# Every frame of one creature uses the same canvas size with the feet on the bottom row,
# so Unity can use a bottom-center pivot for all of them (set by EnemyAssets in the editor).
# Deterministic - re-run any time to regenerate.
$ErrorActionPreference = "Stop"
$root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) "Assets\Enemies"

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public class Cv
{
    public int W, H;
    public Color[,] P;
    public Cv(int w, int h) { W = w; H = h; P = new Color[w, h]; }

    public static Color C(string hex) { return ColorTranslator.FromHtml(hex); }
    public bool In(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
    public bool Opaque(int x, int y) { return In(x, y) && P[x, y].A > 0; }

    public void Px(int x, int y, Color c) { if (In(x, y)) P[x, y] = c; }
    public void Px(double x, double y, Color c) { Px((int)Math.Round(x), (int)Math.Round(y), c); }

    public void Ellipse(double cx, double cy, double rx, double ry, Color c)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                if (dx * dx + dy * dy <= 1.0) P[x, y] = c;
            }
    }

    public void Line(double x0, double y0, double x1, double y1, Color c)
    {
        int n = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0))) + 1;
        for (int i = 0; i <= n; i++)
        {
            double t = n == 0 ? 0 : (double)i / n;
            Px(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, c);
        }
    }

    // Line with thickness (radius r) - limbs.
    public void Thick(double x0, double y0, double x1, double y1, double r, Color c)
    {
        int n = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) * 2) + 1;
        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / n;
            double cx = x0 + (x1 - x0) * t + 0.5, cy = y0 + (y1 - y0) * t + 0.5;
            Ellipse(cx, cy, r, r, c);
        }
    }

    public void Tri(double ax, double ay, double bx, double by, double cx, double cy, Color c)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                double d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
                double d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
                double d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) P[x, y] = c;
            }
    }

    // 1px dark outline around everything opaque (classic pixel-art sprite edge).
    public void Outline(Color c)
    {
        var add = new bool[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (Opaque(x, y)) continue;
                if (Opaque(x + 1, y) || Opaque(x - 1, y) || Opaque(x, y + 1) || Opaque(x, y - 1)) add[x, y] = true;
            }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (add[x, y]) P[x, y] = c;
    }

    public void FlipVertical()
    {
        var n = new Color[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) n[x, H - 1 - y] = P[x, y];
        P = n;
    }

    // Rotate 90 degrees counter-clockwise (square canvases only).
    public void RotateCCW()
    {
        var n = new Color[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) n[y, W - 1 - x] = P[x, y];
        P = n;
    }

    // Moves everything so the lowest opaque pixel touches the bottom row and the figure
    // is centered horizontally on centerX.
    public void Settle(int centerX)
    {
        int minX = W, maxX = -1, maxY = -1;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                if (Opaque(x, y)) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
        if (maxY < 0) return;
        int dy = (H - 1) - maxY, dx = centerX - (minX + maxX) / 2;
        var n = new Color[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < W && ny < H) n[nx, ny] = P[x, y];
            }
        P = n;
    }

    public void Save(string path)
    {
        using (var b = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) b.SetPixel(x, y, P[x, y]);
            b.Save(path, ImageFormat.Png);
        }
    }
}

public static class EnemyGen
{
    static Color C(string h) { return Cv.C(h); }
    static readonly Color OutlineC = C("#120c14");

    // ===================== WHITE RAT (30x18, facing right) =====================
    static readonly Color RatFur = C("#ece8e4"), RatShade = C("#cfc7c4"), RatHi = C("#ffffff"),
        RatPink = C("#ee9fb2"), RatEye = C("#e0193a"), RatMouth = C("#5a1a2a");

    // bob: body up(-)/down(+); leg: walk phase 0-3 (-1 = standing); lunge: head forward;
    // mouth: open; eyeShut: hurt/dead.
    static Cv Rat(int bob, int leg, int lunge, bool mouth, bool eyeShut, int tail)
    {
        var c = new Cv(30, 18);
        double bx = 13, by = 10 + bob;
        // tail
        c.Line(bx - 7, by + 1, bx - 9, by, RatPink);
        c.Line(bx - 9, by, bx - 11, by - 2 + tail, RatPink);
        c.Line(bx - 11, by - 2 + tail, bx - 12, by - 4 + tail, RatPink);
        // far legs (behind body)
        int a = leg < 0 ? 0 : (leg % 2 == 0 ? -1 : 1);
        c.Line(bx - 3, by + 2, bx - 3 - a, 16, RatShade);
        c.Line(bx + 5, by + 2, bx + 5 + a, 16, RatShade);
        // body
        c.Ellipse(bx, by, 7.2, 4.4, RatFur);
        for (int x = 0; x < 30; x++) for (int y = (int)by + 2; y < 18; y++) if (c.P[x, y] == RatFur) c.P[x, y] = RatShade;
        c.Line(bx - 4, by - 3, bx + 1, by - 4, RatHi);
        // near legs
        c.Line(bx - 5, by + 2, bx - 5 + a, 16, RatFur);
        c.Line(bx + 3, by + 2, bx + 3 - a, 16, RatFur);
        c.Px(bx - 5 + a + 1, 16, RatPink); c.Px(bx + 3 - a + 1, 16, RatPink);
        // head + snout
        double hx = 20 + lunge, hy = 9 + bob;
        c.Ellipse(hx, hy, 4.2, 3.4, RatFur);
        c.Ellipse(hx + 3.2, hy + 0.8, 2.6, 1.9, RatFur);
        // ear
        c.Ellipse(hx - 1.2, hy - 3.2, 2.2, 2.2, RatFur);
        c.Px(hx - 1.4, hy - 3.4, RatPink); c.Px(hx - 1.4, hy - 2.4, RatPink);
        // eye
        if (eyeShut) { c.Px(hx + 0.6, hy - 1, RatMouth); c.Px(hx + 1.6, hy - 0.6, RatMouth); }
        else { c.Px(hx + 1, hy - 1, RatEye); c.Px(hx + 1, hy - 2, C("#ff6b7f")); }
        // nose
        c.Px(hx + 5.6, hy + 0.4, RatPink);
        // mouth / teeth
        if (mouth)
        {
            c.Line(hx + 2.5, hy + 2.6, hx + 5.5, hy + 2.6, RatMouth);
            c.Px(hx + 4.5, hy + 3.6, RatHi); c.Px(hx + 5.5, hy + 3.6, RatHi);
            c.Px(hx + 4.5, hy + 1.6, RatHi);
        }
        else c.Px(hx + 4.6, hy + 2.2, RatHi);
        // whiskers
        c.Px(hx + 4, hy - 0.4, RatShade); c.Px(hx + 6.4, hy - 0.6, RatShade);
        c.Outline(OutlineC);
        return c;
    }

    static void RatAll(string dir)
    {
        Rat(0, -1, 0, false, false, 0).Save(Path.Combine(dir, "rat_idle_0.png"));
        Rat(1, -1, 0, false, false, 1).Save(Path.Combine(dir, "rat_idle_1.png"));
        for (int i = 0; i < 4; i++)
            Rat(i % 2 == 0 ? 0 : -1, i, 0, false, false, i % 2).Save(Path.Combine(dir, "rat_move_" + i + ".png"));
        Rat(1, -1, -1, false, false, 1).Save(Path.Combine(dir, "rat_attack_0.png"));   // wind-up
        Rat(-1, 1, 3, true, false, 0).Save(Path.Combine(dir, "rat_attack_1.png"));     // lunge, jaws open (hit)
        Rat(0, -1, 2, false, false, 0).Save(Path.Combine(dir, "rat_attack_2.png"));    // snap shut
        Rat(-1, -1, -1, false, true, 2).Save(Path.Combine(dir, "rat_hurt_0.png"));
        Rat(2, -1, 0, false, true, 2).Save(Path.Combine(dir, "rat_death_0.png"));
        var dead = Rat(0, -1, 0, false, true, 2); dead.FlipVertical(); dead.Settle(15);
        dead.Save(Path.Combine(dir, "rat_death_1.png"));
    }

    // ===================== DEMON SPIDER (44x28, facing right) =====================
    static readonly Color SpBody = C("#2b2135"), SpHi = C("#45354f"), SpHead = C("#33263f"),
        SpLeg = C("#1c1424"), SpFarLeg = C("#140e1a"), SpJoint = C("#5a4668"), SpMark = C("#b0203a"),
        SpEye = C("#ff3b52"), SpFang = C("#d8d0c0");

    // Knee/foot spread for the 4 legs on one side (0 = rearmost ... 3 = frontmost).
    static readonly double[] KneeX = { -7, -3, 3, 7 };
    static readonly double[] FootX = { -15, -8, 7, 14 };

    static void SpiderLeg(Cv c, double cx, double cy, int i, int phase, bool hurt, bool far, Color col)
    {
        double atx = cx - 1 + (i - 1.5) * 1.2, aty = cy - 1;
        int step = phase < 0 ? 0 : (((i + phase + (far ? 1 : 0)) % 2 == 0) ? 1 : -1);
        double lift = (phase >= 0 && step > 0) ? -1.5 : 0;
        double spread = far ? 0.8 : 1.0;
        double kx = atx + KneeX[i] * spread, ky = cy - 10 + lift;
        double fx = atx + FootX[i] * spread + step, fy = 26;
        if (hurt) { ky = cy - 7; fx = atx + FootX[i] * 1.2; }
        c.Line(atx, aty, kx, ky, col);
        c.Line(kx, ky, fx, fy, col);
        if (!far) c.Px(kx, ky, SpJoint);
    }

    // stab: 0 none, 1 raise, 2 thrust, 3 retract
    static Cv Spider(int bob, int phase, int stab, bool hurt)
    {
        var c = new Cv(44, 28);
        double ax = 14, ay = 17 + bob, cx = 24, cy = 18 + bob;
        // far-side legs behind the body
        for (int i = 0; i < 4; i++) SpiderLeg(c, cx, cy, i, phase, hurt, true, SpFarLeg);
        // abdomen with crimson hourglass mark
        c.Ellipse(ax, ay, 7.5, 5.8, SpBody);
        c.Ellipse(ax - 2, ay - 2.5, 3.5, 2, SpHi);
        c.Ellipse(ax + 0.5, ay + 0.5, 1.8, 1.2, SpMark);
        c.Px(ax - 2.5, ay + 1, SpMark); c.Px(ax + 3.5, ay, SpMark); c.Px(ax + 0.5, ay - 2, SpMark);
        // head
        c.Ellipse(cx, cy, 4.8, 3.6, SpHead);
        // near-side legs over the body (the front one is the stabbing leg when attacking)
        for (int i = 0; i < 4; i++)
        {
            if (i == 3 && stab > 0) continue;
            SpiderLeg(c, cx, cy, i, phase, hurt, false, SpLeg);
        }
        if (stab > 0)
        {
            double atx = cx + 2, aty = cy - 1;
            double kx, ky, fx, fy;
            if (stab == 1) { kx = cx + 4; ky = cy - 12; fx = cx + 10; fy = cy - 15; }
            else if (stab == 2) { kx = cx + 9; ky = cy - 4; fx = cx + 19; fy = cy - 3; }
            else { kx = cx + 7; ky = cy - 8; fx = cx + 13; fy = cy - 7; }
            c.Line(atx, aty, kx, ky, SpLeg);
            c.Line(kx, ky, fx, fy, SpLeg);
            c.Px(kx, ky, SpJoint);
            c.Px(fx, fy, stab == 2 ? C("#ffd0d8") : SpMark);
        }
        // eyes + fangs
        if (hurt) { c.Px(cx + 3, cy - 1, SpJoint); c.Px(cx + 4, cy - 1, SpJoint); }
        else
        {
            c.Px(cx + 3, cy - 1, SpEye); c.Px(cx + 4, cy - 1, SpEye);
            c.Px(cx + 2, cy - 2, SpEye); c.Px(cx + 4, cy - 2, C("#ff8a9a"));
        }
        c.Px(cx + 4, cy + 2, SpFang); c.Px(cx + 5, cy + 3, SpFang);
        c.Outline(OutlineC);
        return c;
    }

    static void SpiderAll(string dir)
    {
        Spider(0, -1, 0, false).Save(Path.Combine(dir, "spider_idle_0.png"));
        Spider(1, -1, 0, false).Save(Path.Combine(dir, "spider_idle_1.png"));
        for (int i = 0; i < 4; i++)
            Spider(i % 2 == 0 ? 0 : -1, i, 0, false).Save(Path.Combine(dir, "spider_move_" + i + ".png"));
        Spider(-1, -1, 1, false).Save(Path.Combine(dir, "spider_attack_0.png"));   // raise foreleg
        Spider(1, -1, 2, false).Save(Path.Combine(dir, "spider_attack_1.png"));    // thrust (hit)
        Spider(0, -1, 3, false).Save(Path.Combine(dir, "spider_attack_2.png"));    // pull back
        Spider(-1, -1, 0, true).Save(Path.Combine(dir, "spider_hurt_0.png"));
        Spider(2, -1, 0, true).Save(Path.Combine(dir, "spider_death_0.png"));
        var dead = Spider(0, 0, 0, true); dead.FlipVertical(); dead.Settle(22);
        dead.Save(Path.Combine(dir, "spider_death_1.png"));
    }

    // ===================== WEREWOLF BOSS (64x64, facing right) =====================
    static readonly Color WFur = C("#5a5464"), WLight = C("#7a7486"), WDark = C("#3a3542"),
        WBelly = C("#948a9c"), WEye = C("#ff3040"), WClaw = C("#ece4d4"), WMouth = C("#5a1a2a"),
        WVein = C("#b0203a"), WNose = C("#1a1620"), WSlash = C("#ffd6dc");

    // arm: 0 idle, 1 raise (wind-up), 2 swipe (hit), 3 follow-through, 4 walk-swing A, 5 walk-swing B, 6 hurt
    static Cv Wolf(int bob, int legPhase, int arm, bool mouth, bool hurt, int kneel)
    {
        var c = new Cv(64, 64);
        double ox = 4; // shift the whole figure so it sits centered in the canvas
        double hipX = 24 + ox, hipY = 42 + bob + kneel;
        // ---- legs (digitigrade). far leg darker, drawn first
        for (int l = 0; l < 2; l++)
        {
            double p = legPhase < 0 ? 0 : (((legPhase + l) % 2 == 0) ? 2.5 : -2.5);
            if (kneel > 0) p = l == 0 ? -3 : 3;
            Color col = l == 0 ? WDark : WFur;
            double hx = hipX + (l == 0 ? -2 : 2);
            double kx = hx - 2 + p, ky = hipY + 7 - kneel * 0.6;
            double anx = hx + 3 + p, any = 56 - (kneel > 0 ? 1 : 0);
            double tx = hx + 7 + p, ty = 61;
            c.Thick(hx, hipY, kx, ky, 3.0, col);
            c.Thick(kx, ky, anx, any, 2.0, col);
            c.Thick(anx, any, tx, ty, 1.5, col);
            c.Px(tx + 1, ty + 1, WClaw); c.Px(tx + 2, ty + 1, WClaw);
        }
        // ---- far arm (dark)
        double shX = 30 + ox, shY = 27 + bob + kneel;
        double bAx = shX - 4, bAy = shY + 1;
        double sw = (arm == 4 ? -2 : arm == 5 ? 2 : 0);
        c.Thick(bAx, bAy, bAx - 2 - sw, bAy + 9, 2.2, WDark);
        c.Thick(bAx - 2 - sw, bAy + 9, bAx - sw, bAy + 16, 1.8, WDark);
        // ---- torso: hunched back, light chest
        c.Ellipse(hipX + 2, hipY - 10, 9.5, 11, WFur);
        c.Ellipse(shX - 4, shY - 2, 9, 7, WFur);
        c.Ellipse(shX + 1, shY + 6, 5, 8, WBelly);
        // mane spikes along the back + corruption veins
        for (int i = 0; i < 5; i++) c.Tri(shX - 12 + i * 3, shY - 4 + i, shX - 9 + i * 3, shY - 9 + i, shX - 7 + i * 3, shY - 3 + i, WDark);
        c.Line(shX - 8, shY + 2, shX - 5, shY + 6, WVein); c.Line(shX - 5, shY + 6, shX - 6, shY + 10, WVein);
        // ---- head
        double hdX = 40 + ox + (hurt ? -2 : 0), hdY = 20 + bob + kneel + (hurt ? -1 : 0);
        c.Tri(hdX - 4, hdY - 3, hdX - 2, hdY - 11, hdX + 1, hdY - 4, WDark);   // ears
        c.Tri(hdX - 1, hdY - 4, hdX + 2, hdY - 10, hdX + 4, hdY - 3, WFur);
        c.Ellipse(hdX, hdY, 6.5, 5.5, WFur);
        c.Ellipse(hdX + 6, hdY + 2, 5, 2.8, WLight);                           // snout
        c.Px(hdX + 10.5, hdY + 0.8, WNose); c.Px(hdX + 10.5, hdY + 1.8, WNose);
        if (mouth)
        {
            c.Ellipse(hdX + 5.5, hdY + 5.2, 4.2, 1.6, WLight);                  // open jaw
            c.Line(hdX + 3, hdY + 4, hdX + 9, hdY + 4, WMouth);
            c.Px(hdX + 4, hdY + 3.4, WClaw); c.Px(hdX + 7, hdY + 3.4, WClaw); c.Px(hdX + 5.5, hdY + 5, WClaw);
        }
        else { c.Line(hdX + 4, hdY + 4, hdX + 9, hdY + 3.6, WMouth); c.Px(hdX + 6, hdY + 4.6, WClaw); }
        if (hurt) { c.Line(hdX + 1, hdY - 2, hdX + 3, hdY - 1, WMouth); }
        else
        {
            c.Px(hdX + 2, hdY - 1.5, WEye); c.Px(hdX + 3, hdY - 1.5, WEye); c.Px(hdX + 3, hdY - 2.5, C("#ff8a9a"));
        }
        // ---- near arm with claws
        double elX, elY, haX, haY, clawDx, clawDy;
        switch (arm)
        {
            case 1: elX = shX - 3; elY = shY - 8; haX = shX - 8; haY = shY - 15; clawDx = -1; clawDy = -1; break;   // raised back
            case 2: elX = shX + 7; elY = shY + 2; haX = shX + 16; haY = shY + 8; clawDx = 1; clawDy = 0.6; break;   // swipe
            case 3: elX = shX + 4; elY = shY + 9; haX = shX + 8; haY = shY + 17; clawDx = 0.4; clawDy = 1; break;  // follow-through
            case 6: elX = shX + 5; elY = shY - 4; haX = shX + 9; haY = shY - 10; clawDx = 0.5; clawDy = -1; break;  // flinch
            default: elX = shX + 2 + sw; elY = shY + 9; haX = shX + 4 + sw; haY = shY + 17; clawDx = 0.3; clawDy = 1; break;
        }
        c.Thick(shX, shY, elX, elY, 2.6, WFur);
        c.Thick(elX, elY, haX, haY, 2.2, WFur);
        c.Ellipse(haX + 0.5, haY + 0.5, 2.6, 2.6, WLight);
        for (int k = -1; k <= 1; k++)
        {
            double px = -clawDy, py = clawDx; // perpendicular for claw spacing
            double sx = haX + clawDx * 2 + px * k * 1.4, sy = haY + clawDy * 2 + py * k * 1.4;
            c.Line(sx, sy, sx + clawDx * 2.5, sy + clawDy * 2.5, WClaw);
        }
        c.Outline(OutlineC);
        // claw slash trail (no outline): three pale arcs ahead of the swipe
        if (arm == 2)
        {
            for (int k = 0; k < 3; k++)
                for (int i = 0; i <= 12; i++)
                {
                    double t = i / 12.0;
                    double x = shX + 9 + k * 2 + 9 * Math.Sin(t * Math.PI * 0.8);
                    double y = shY - 10 + k * 2 + t * 22;
                    c.Px(x, y, i % 3 == 0 ? WVein : WSlash);
                }
        }
        return c;
    }

    static void WolfAll(string dir)
    {
        Wolf(0, -1, 0, false, false, 0).Save(Path.Combine(dir, "wolf_idle_0.png"));
        Wolf(1, -1, 0, true, false, 0).Save(Path.Combine(dir, "wolf_idle_1.png"));
        for (int i = 0; i < 4; i++)
            Wolf(i % 2 == 0 ? 0 : -1, i, i % 2 == 0 ? 4 : 5, false, false, 0).Save(Path.Combine(dir, "wolf_move_" + i + ".png"));
        Wolf(-1, -1, 1, true, false, 0).Save(Path.Combine(dir, "wolf_attack_0.png"));   // raise claw
        Wolf(1, -1, 2, true, false, 0).Save(Path.Combine(dir, "wolf_attack_1.png"));    // swipe (hit)
        Wolf(1, -1, 3, false, false, 0).Save(Path.Combine(dir, "wolf_attack_2.png"));   // follow-through
        Wolf(0, -1, 0, false, false, 0).Save(Path.Combine(dir, "wolf_attack_3.png"));   // recover
        Wolf(-1, -1, 6, true, true, 0).Save(Path.Combine(dir, "wolf_hurt_0.png"));
        Wolf(0, -1, 3, true, true, 6).Save(Path.Combine(dir, "wolf_death_0.png"));       // drops to a knee
        var dead = Wolf(0, -1, 0, false, true, 0); dead.RotateCCW(); dead.Settle(32);
        dead.Save(Path.Combine(dir, "wolf_death_1.png"));                               // falls over
    }

    public static void Run(string root)
    {
        string[] names = { "Rat", "Spider", "Werewolf" };
        foreach (var n in names) Directory.CreateDirectory(Path.Combine(root, n));
        // The white rat now uses PixelLab art (Assets/Enemies/Rat) - not regenerated here.
        // RatAll(Path.Combine(root, "Rat"));
        SpiderAll(Path.Combine(root, "Spider"));
        WolfAll(Path.Combine(root, "Werewolf"));
    }
}
'@

[EnemyGen]::Run($root)
Get-ChildItem $root -Recurse -Filter *.png | ForEach-Object { $_.Name }

# The rat and the spider are cut from their sprite sheets now: put those frames back over the drawn ones.
& (Join-Path $PSScriptRoot "generate_rat_spider_sprites.ps1")
