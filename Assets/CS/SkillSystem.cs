using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The run skills (CardPool.Skills). The cats start every run with none; each one is picked from a
// stage-clear draft and is gone when the run ends (levels live in PlayerStats with the other cards).
//   Active  - Q / E / R, filled in the order the skills were picked; each has a cooldown.
//   Passive - runs by itself (shield, regeneration, counter...).
//   Enhance - changes a basic move: the knight's swing / dash / jump attack, the archer's arrows.
// Added to the Player by PlayerStats. PlayerAttack, PlayerController2D and PlayerStats call the
// hooks below (ShootBasic, OnMeleeSwing, OnJumpAttackImpact, TryBlockHit...). Effects come from
// Vfx (sheets in Assets/Effects) and SkillFx; hit checks use the same body boxes as PlayerAttack.
public partial class SkillSystem : MonoBehaviour
{
    public const int MaxActiveSlots = 3;
    public static readonly string[] SlotKeys = { "Q", "E", "R" };
    public static SkillSystem Instance { get; private set; }

    PlayerStats stats;
    PlayerController2D body;
    PlayerAttack attack;

    readonly Dictionary<string, float> cooldownLeft = new Dictionary<string, float>();
    readonly List<string> cooldownKeys = new List<string>(); // reused every frame (no per-frame list)
    Coroutine casting;
    int castSerial, activeCast; // activeCast != 0 while a skill runs (set before it starts: some finish on their first frame)
    float invulnTimer;

    // Passive state.
    bool shieldUp;
    float shieldTimer, groomTimer, counterTimer, glintTimer, auraTimer, shieldGlowTimer;
    int shotCount;

    public bool Busy => activeCast != 0;
    public bool BlocksAttack => activeCast != 0;   // no basic attacks while a skill plays
    public bool Invulnerable => invulnTimer > 0f;

    // ------------------------------------------------------------------ levels & numbers
    int Lv(string id) => stats != null ? stats.CardLevel(id) : 0;
    bool Has(string id) => Lv(id) > 0;
    // Level 2 of an active: x1.5 damage, x0.8 cooldown.
    float Power(string id) => Lv(id) >= 2 ? 1.5f : 1f;

    public float Cooldown(string id)
    {
        var c = CardPool.Get(id);
        return c == null ? 0f : c.cooldown * (Lv(id) >= 2 ? 0.8f : 1f) * (stats != null ? stats.SkillCooldownMultiplier : 1f); // 바람 세트
    }

    public float CooldownLeft(string id) => cooldownLeft.TryGetValue(id, out float t) ? Mathf.Max(0f, t) : 0f;

    // Passives that recharge (식빵 자세, 수염 센서): 1 = ready, 0 = just used. Others are always 1.
    public float PassiveCharge(string id)
    {
        if (id == "k_loaf") return shieldUp ? 1f : 1f - Mathf.Clamp01(shieldTimer / ShieldTime);
        if (id == "k_whisker") return counterTimer <= 0f ? 1f : 1f - Mathf.Clamp01(counterTimer / CounterTime);
        return 1f;
    }

    float ShieldTime => Lv("k_loaf") >= 2 ? 8f : 12f;
    float CounterTime => Lv("k_whisker") >= 2 ? 7f : 10f;
    float GroomTime => Lv("k_groom") >= 2 ? 5f : 8f;

    // Read by PlayerStats (also before this component exists, so they take the stats).
    public static float CritBonus(PlayerStats s) { int lv = s.CardLevel("k_night"); return lv <= 0 ? 0f : lv == 1 ? 0.15f : 0.25f; }
    public static float RangeMultiplier(PlayerStats s) => s.CardLevel("a_watch") > 0 ? 1.3f : 1f;
    public static float AttackSpeedMultiplier(PlayerStats s) { int lv = s.CardLevel("a_wind"); return lv <= 0 ? 1f : lv == 1 ? 1.15f : 1.25f; }
    public static float DashDistanceMultiplier(PlayerStats s) => s.CardLevel("k_leap") > 0 ? 1.25f : 1f;
    // 날카로운 솜방망이: the knight's basic swings.
    public static float MeleeMultiplier(PlayerStats s) { int lv = s != null ? s.CardLevel("k_paw") : 0; return lv <= 0 ? 1f : lv == 1 ? 1.25f : 1.5f; }
    float ArrowSpeedMultiplier => Has("a_wind") ? 1.25f : 1f;

    // ------------------------------------------------------------------ lifecycle
    void Awake()
    {
        Instance = this;
        stats = GetComponent<PlayerStats>();
        body = GetComponent<PlayerController2D>();
        attack = GetComponent<PlayerAttack>();
        if (body != null)
        {
            body.DashStarted += OnDashStarted;
            body.DashEnded += OnDashEnded;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Skills carried over from the previous stage come back ready.
        shieldUp = Has("k_loaf");
        if (Has("a_watch")) Vfx.Play("a_watch_fx", Feet, 1f);
    }

    // PlayerStats.ApplyCard: a skill was just picked from the draft.
    public void OnSkillLearned(string id)
    {
        var c = CardPool.Get(id);
        if (c == null) return;
        if (id == "k_loaf") shieldUp = true;
        if (id == "a_watch") Vfx.Play("a_watch_fx", Feet, 1f);
        DamagePopup.SpawnLabel(transform.position + Vector3.up * 1.4f, c.name, new Color(1f, 0.85f, 0.4f));
    }

    // PlayerStats.TakeDamage: a hit landed (not blocked or dodged).
    public void OnHurt()
    {
        groomTimer = 0f;
        StopCast();
    }

    void Update()
    {
        if (CardDraftUI.IsPaused || body == null || stats == null) return;
        float dt = Time.deltaTime;

        if (cooldownLeft.Count > 0)
        {
            cooldownKeys.Clear();
            cooldownKeys.AddRange(cooldownLeft.Keys); // (a dictionary can't be written while it's walked)
            foreach (var k in cooldownKeys) cooldownLeft[k] = Mathf.Max(0f, cooldownLeft[k] - dt);
        }
        if (invulnTimer > 0f) invulnTimer -= dt;
        if (body.IsDead) { StopCast(); return; }

        UpdatePassives(dt);

        if (PauseMenu.IsOpen || (RunManager.Instance != null && RunManager.Instance.RunEnded)) return;
        var actives = stats.ActiveSkills;
        for (int i = 0; i < actives.Count && i < MaxActiveSlots; i++)
            if (GameInput.SkillPressed(i)) TryCast(actives[i]); // Q / E / R or LB / RB / Y
    }

    void TryCast(string id)
    {
        if (Busy || !body.CanAttack) return;
        if (CooldownLeft(id) > 0f)
        {
            Sfx.Play("ui_error", 0.5f);
            return;
        }
        IEnumerator routine = CastRoutine(id);
        if (routine == null) return;
        cooldownLeft[id] = Cooldown(id);
        if (attack != null) attack.CancelCombo();
        StartCast(routine);
    }

    void StartCast(IEnumerator routine)
    {
        int id = ++castSerial;
        activeCast = id;
        var co = StartCoroutine(Run(routine, id));
        if (activeCast == id) casting = co; // still running after its first step
    }

    IEnumerator CastRoutine(string id)
    {
        switch (id)
        {
            case "k_combo": return NyangCombo();
            case "k_highcut": return HighCut();
            case "k_knead": return Knead();
            case "k_ilseom": return Ilseom();
            case "k_rush": return Rush();
            case "k_wheel": return CatWheel();
            case "k_hiss": return Hiss();
            case "k_tail": return TailGuard();
            case "a_smile": return Smile();
            case "a_meteor": return MeteorRain();
            case "a_rapid": return RapidShot();
            case "a_backstep": return BackstepShot();
            case "a_wire": return BellWire();
            case "a_trap": return StickyTrap();
            case "a_churu": return Churu();
            case "a_homing": return HomingYarn();
            case "a_can": return CanPiercer();
        }
        return null;
    }

    // Runs a skill step by step (so StopCast really stops all of it), then hands the body back.
    IEnumerator Run(IEnumerator routine, int id)
    {
        while (routine.MoveNext()) yield return routine.Current;
        if (activeCast != id) yield break;
        activeCast = 0;
        casting = null;
        Release();
    }

    void StopCast()
    {
        if (activeCast == 0) return;
        if (casting != null) StopCoroutine(casting);
        activeCast = 0;
        casting = null;
        Release();
    }

    void Release()
    {
        if (body == null) return;
        body.skillLock = false;
        body.skillHover = false;
    }

    // ------------------------------------------------------------------ helpers
    float Facing => body.FacingSign;
    Vector3 Me => transform.position;          // body centre
    Vector3 Feet => body != null ? body.Feet : transform.position;
    Vector3 Head => Me + Vector3.up * (body != null ? body.bodyHeight * 0.55f : 1.3f);
    float VisualScale => body.visual != null ? Mathf.Abs(body.visual.lossyScale.x) : 1f;

    Vector2 MouseWorld()
    {
        return GameInput.AimPoint(Camera.main, Me, Facing, 4f); // the mouse, or the right stick / facing on a gamepad
    }

    Vector2 Aim()
    {
        Vector2 d = MouseWorld() - (Vector2)Me;
        return d.sqrMagnitude > 0.0001f ? d.normalized : new Vector2(Facing, 0f);
    }

    // Knight skills go the way the cat is running (A / D held), else the way it faces - not toward the mouse,
    // so a slash never comes out backwards while running away from the cursor.
    float MoveDir()
    {
        float x = GameInput.Move.x;
        return x != 0f ? Mathf.Sign(x) : Facing;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // Plays a combo move's frames as the skill's pose (the knight's chops, the archer's bow draw).
    void Pose(int comboIndex, float speed = 1f)
    {
        var ch = body.character;
        if (ch != null && ch.combo != null && ch.combo.Length > 0)
        {
            var m = ch.combo[Mathf.Clamp(comboIndex, 0, ch.combo.Length - 1)];
            body.PlayAttack(m.frames, m.framesPerSecond * speed);
        }
        else body.PlayAttack(body.attackFrames, body.attackFramesPerSecond * speed);
    }

    // Starts the pose and returns how long until its strike frame (the move's hitFrame): wait that long before the
    // slash effect / damage / arrow, so they land when the paw or bow actually swings through, not at wind-up.
    float Windup(int comboIndex, float speed = 1f, float max = 0.25f)
    {
        Pose(comboIndex, speed);
        var ch = body.character;
        if (ch == null || ch.combo == null || ch.combo.Length == 0) return 0f;
        var m = ch.combo[Mathf.Clamp(comboIndex, 0, ch.combo.Length - 1)];
        return Mathf.Min(max, m.hitFrame / Mathf.Max(1f, m.framesPerSecond * speed));
    }

    public static Enemy NearestEnemy(Vector2 from, float maxDistance, HashSet<Enemy> exclude)
    {
        Enemy best = null;
        float bestD = maxDistance;
        foreach (var e in Enemy.All)
        {
            if (e.IsDead || (exclude != null && exclude.Contains(e))) continue;
            Vector2 c = e.Renderer != null ? (Vector2)e.Renderer.bounds.center : (Vector2)e.transform.position;
            float d = Vector2.Distance(c, from);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    // Hits every monster whose body overlaps the box. Returns how many were hit.
    int HitBox(Vector2 min, Vector2 max, float mult, float pushDir = 0f, HashSet<Enemy> skip = null,
               System.Action<Enemy> after = null, bool strong = true)
    {
        int n = 0;
        foreach (var e in Enemy.All.ToArray())
        {
            if (e.IsDead || (skip != null && skip.Contains(e))) continue;
            float x = e.BodyX, half = e.BodyHalfWidth;
            if (x + half < min.x || x - half > max.x) continue;
            Bounds b = e.Renderer != null ? e.Renderer.bounds : new Bounds(e.transform.position, Vector3.one);
            if (b.max.y < min.y || b.min.y > max.y) continue;
            Damage(e, mult, pushDir != 0f ? pushDir : Mathf.Sign(x - Me.x), strong);
            skip?.Add(e);
            after?.Invoke(e);
            n++;
        }
        return n;
    }

    // Hits every monster whose sprite box comes within `radius` of `center`.
    int HitCircle(Vector2 center, float radius, float mult, HashSet<Enemy> skip = null,
                  System.Action<Enemy> after = null, bool strong = true)
    {
        int n = 0;
        foreach (var e in Enemy.All.ToArray())
        {
            if (e.IsDead || (skip != null && skip.Contains(e))) continue;
            Vector2 closest = e.Renderer != null ? (Vector2)e.Renderer.bounds.ClosestPoint(center) : (Vector2)e.transform.position;
            if (Vector2.Distance(closest, center) > radius) continue;
            float dx = e.BodyX - center.x;
            Damage(e, mult, Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : Facing, strong);
            skip?.Add(e);
            after?.Invoke(e);
            n++;
        }
        return n;
    }

    // Box in front of the body: `reach` ahead (a little behind too), from below the feet to above the head.
    int HitFront(float reach, float mult, float below = 0.2f, float above = 0.3f, HashSet<Enemy> skip = null,
                 System.Action<Enemy> after = null)
    {
        float x0 = Me.x - Facing * 0.4f, x1 = Me.x + Facing * reach;
        float feet = Feet.y;
        return HitBox(new Vector2(Mathf.Min(x0, x1), feet - below), new Vector2(Mathf.Max(x0, x1), feet + body.bodyHeight + above),
                      mult, Facing, skip, after);
    }

    // Attack x mult, crit rolled (the 필살 일격 card only boosts the basic combo, not skills).
    int Roll(float mult, out bool crit) => Mathf.Max(1, Mathf.RoundToInt(stats.RollDamage(1f, out crit) * mult));

    void Damage(Enemy e, float mult, float push, bool strong)
    {
        if (e.IsDead) return;
        int dmg = Roll(mult, out bool crit);
        e.ReceiveDamage(dmg, push, strong, crit);
    }

    void Explode(Vector3 at, float radius, float mult, string fx = "explosion")
    {
        Vfx.Play(fx, at, radius >= 2.7f ? 2f : radius >= 1.35f ? 1f : 0.5f); // the pixel-art step closest to radius / 1.8
        Sfx.Play("slam", 0.8f, 1.3f);
        CameraFollow.Shake(0.15f, 0.12f);
        HitCircle(at, radius, mult);
    }

    // Top of the floor under `from` (searching down), or from.y if there is none within `maxDown`.
    static float GroundY(Vector2 from, float maxDown = 8f)
    {
        var map = StageMap.Instance;
        if (map == null) return from.y;
        for (float d = 0f; d <= maxDown; d += 0.1f)
        {
            var p = new Vector2(from.x, from.y - d);
            if (map.IsSolid(p) || map.IsPlatform(p))
            {
                // Step back up to the surface.
                for (int i = 0; i < 10 && (map.IsSolid(p) || map.IsPlatform(p)); i++) p.y += 0.02f;
                return p.y;
            }
        }
        return from.y;
    }

    static bool Solid(Vector2 p) => StageMap.Instance != null && StageMap.Instance.IsSolid(p);

    // Afterimages of the cat for `seconds`.
    IEnumerator Trail(float seconds, Color tint, float every = 0.03f)
    {
        for (float t = 0f; t < seconds; t += every)
        {
            SkillFx.Ghost(body.Renderer, tint, 0.2f);
            yield return new WaitForSeconds(Mathf.Max(every, 0.06f)); // a few clear afterimages, not a smear
        }
    }

    static readonly Color Orange = new Color(1f, 0.62f, 0.25f, 0.55f);
    static readonly Color Gold = new Color(1f, 0.86f, 0.35f, 0.9f);

}
