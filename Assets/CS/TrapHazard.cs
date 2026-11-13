using System.Collections.Generic;
using UnityEngine;

// Floor traps (Foozle Trap Pack, CC0, Resources/Traps). Stage 1 has none; later stages add more.
// Fire, toxic: idle -> 0.7 s warning (flicker + "!") -> burst that hurts 0.5 heart -> rest.
// Arrow: pops out of the floor during the warning, then fires one arrow at the player's side.
// Spikes: a spiked block under a low ceiling; it shakes for the warning, slams down, then rises.
// Saw: always spinning, slides along its own floor strip, so it is always in plain sight.
// Traps sit on StageMap.spawnSlots, away from the start, the room seals and the exit, and
// at least 8 tiles apart so there is always a safe tile to land on.
public class TrapHazard : MonoBehaviour
{
    public enum Kind { Fire, Toxic, Saw, Arrow, Spikes }

    public const float Damage = 0.5f;
    const float WarnTime = 0.7f;
    const float ActiveRange = 16f;   // only runs when the player is near (on screen)

    Kind kind;
    Sprite[] frames;
    SpriteRenderer sr;
    Vector2 feet;           // floor point the trap stands on
    Vector2 halfSize;       // hurt box
    Vector2 hurtOffset;     // hurt box centre, from the floor point
    float timer;
    int state;              // 0 idle, 1 warning, 2 burst
    float idleTime, burstTime;
    float railLeft, railRight, sawDir = 1f;
    bool shot;
    GameObject aim; // the arrow trap's line of fire, shown while it aims
    PlayerController2D player;

    static readonly Dictionary<string, Sprite[]> sheetCache = new Dictionary<string, Sprite[]>();

    public static void SpawnForStage(int stage)
    {
        var map = StageMap.Instance;
        if (map == null || map.spawnSlots == null || map.spawnSlots.Length == 0 || stage <= 1) return;

        var plan = new List<Kind>();
        if (stage == 2) { plan.Add(Kind.Arrow); plan.Add(Kind.Arrow); plan.Add(Kind.Fire); }
        else if (stage == 3) { plan.Add(Kind.Spikes); plan.Add(Kind.Spikes); plan.Add(Kind.Fire); plan.Add(Kind.Fire); }
        else
        {
            plan.Add(Kind.Saw); plan.Add(Kind.Saw);
            plan.Add(Kind.Toxic); plan.Add(Kind.Toxic);
            plan.Add(Kind.Arrow); plan.Add(Kind.Fire);
        }

        var pool = SafeSlots(map);
        // Stage 4's upper half was the quiet part: half its traps go there first.
        bool upperFirst = stage >= 4 && map.verticalLayout;
        var picked = new List<Vector2>();
        var root = new GameObject("Traps").transform;
        for (int i = 0; i < plan.Count; i++)
        {
            bool wantUpper = upperFirst && i % 2 == 0;
            Vector2? spot = null;
            if (plan[i] == Kind.Spikes) spot = Pick(pool, picked, s => LowCeiling(map, s));
            if (spot == null && wantUpper) spot = Pick(pool, picked, s => s.y > map.mapSize.y * 0.55f);
            if (spot == null) spot = Pick(pool, picked, null);
            if (spot == null) break;
            var kind = plan[i] == Kind.Spikes && !LowCeiling(map, spot.Value) ? Kind.Fire : plan[i];
            Create(root, kind, spot.Value, map);
        }
    }

    static List<Vector2> SafeSlots(StageMap map)
    {
        var edges = new List<float>();
        if (map.verticalLayout && map.verticalGateHeights != null) edges.AddRange(map.verticalGateHeights);
        if (!map.verticalLayout)
        {
            if (map.horizontalGatePositions != null && map.horizontalGatePositions.Length > 0) edges.AddRange(map.horizontalGatePositions);
            else if (map.mapSize.x >= 80) edges.AddRange(new[] { map.mapSize.x * 0.25f, map.mapSize.x * 0.5f, map.mapSize.x * 0.75f });
        }
        // (The exit log only appears after the clear, beside the player; traps are off by then - see StageDone.)
        var pool = new List<Vector2>();
        foreach (var s in map.spawnSlots)
        {
            if (Vector2.Distance(s, map.playerStart) < 8f) continue;
            bool nearGate = false;
            foreach (float e in edges)
                if (Mathf.Abs((map.verticalLayout ? s.y : s.x) - e) < 3.5f) { nearGate = true; break; }
            if (nearGate) continue;
            // Needs floor under both sides so the burst does not hang over a gap.
            if (!Standable(map, s + Vector2.left * 0.7f) || !Standable(map, s + Vector2.right * 0.7f)) continue;
            pool.Add(s);
        }
        return pool;
    }

    static Vector2? Pick(List<Vector2> pool, List<Vector2> picked, System.Predicate<Vector2> want)
    {
        var options = want == null ? new List<Vector2>(pool) : pool.FindAll(want);
        while (options.Count > 0)
        {
            int i = RunRng.Range(0, options.Count);
            var s = options[i];
            options.RemoveAt(i);
            bool tooClose = false;
            foreach (var p in picked) if (Vector2.Distance(p, s) < 8f) { tooClose = true; break; }
            if (tooClose) continue;
            pool.Remove(s);
            picked.Add(s);
            return s;
        }
        return null;
    }

    static bool Standable(StageMap map, Vector2 feet)
    {
        Vector2 below = feet + Vector2.down * 0.3f;
        return (map.IsSolid(below) || map.IsPlatform(below)) && !map.IsSolid(feet + Vector2.up * 0.3f);
    }

    // The spike sheet is 2 tiles tall with its chains at the top edge: it needs a ceiling right there.
    static bool LowCeiling(StageMap map, Vector2 feet)
        => map.solid != null && map.solid.HasTile(map.solid.WorldToCell(feet + Vector2.up * 2.4f)) &&
           !map.IsSolid(feet + Vector2.up * 1.0f) && !map.IsSolid(feet + Vector2.up * 1.8f);

    static void Create(Transform root, Kind kind, Vector2 feet, StageMap map)
    {
        string sheet = kind == Kind.Fire ? "fire" : kind == Kind.Toxic ? "toxic" : kind == Kind.Saw ? "saw"
                     : kind == Kind.Arrow ? "arrow_trap" : "ceiling_spikes";
        var frames = Frames(sheet);
        if (frames == null || frames.Length == 0) return;

        var go = new GameObject("Trap_" + sheet);
        go.transform.SetParent(root, false);
        var trap = go.AddComponent<TrapHazard>();
        trap.kind = kind;
        trap.frames = frames;
        trap.feet = feet;
        trap.sr = go.AddComponent<SpriteRenderer>();
        trap.sr.sprite = frames[0];
        trap.sr.sortingOrder = 8;

        float h = frames[0].bounds.size.y;
        go.transform.position = new Vector3(feet.x, feet.y + h * 0.5f, 0f);
        switch (kind)
        {
            case Kind.Fire:
                trap.halfSize = new Vector2(0.35f, 0.85f);
                trap.hurtOffset = new Vector2(0f, 0.9f);
                trap.idleTime = RunRng.Range(1.6f, 2.6f);
                trap.burstTime = 0.9f;
                break;
            case Kind.Toxic:
                trap.halfSize = new Vector2(0.8f, 0.8f);
                trap.hurtOffset = new Vector2(0f, 0.8f);
                trap.idleTime = RunRng.Range(2.2f, 3.2f);
                trap.burstTime = 1.4f;
                break;
            case Kind.Arrow:
                trap.idleTime = RunRng.Range(2.0f, 3.0f);
                trap.burstTime = 1.0f;
                break;
            case Kind.Spikes:
                trap.halfSize = new Vector2(0.85f, 0.45f);
                trap.hurtOffset = new Vector2(0f, 0.45f);
                trap.idleTime = RunRng.Range(1.8f, 2.8f);
                trap.burstTime = 1.3f;
                trap.sr.sprite = frames[frames.Length - 1]; // starts raised
                break;
            default:
                // The blade sinks into the floor a bit, like it runs in a slot.
                go.transform.position = new Vector3(feet.x, feet.y + h * 0.3f, 0f);
                trap.halfSize = new Vector2(0.6f, 0.6f);
                trap.FindRail(map);
                break;
        }
        trap.timer = RunRng.Range(0f, trap.idleTime); // traps do not all fire together
    }

    void FindRail(StageMap map)
    {
        railLeft = railRight = feet.x;
        // At most 2 tiles each way: slots are 3.5 from a seal, so the blade never slides into one.
        for (float d = 0.5f; d <= 2f; d += 0.5f)
        {
            if (!Standable(map, new Vector2(feet.x - d, feet.y))) break;
            railLeft = feet.x - d;
        }
        for (float d = 0.5f; d <= 2f; d += 0.5f)
        {
            if (!Standable(map, new Vector2(feet.x + d, feet.y))) break;
            railRight = feet.x + d;
        }
    }

    static Sprite[] Frames(string sheet)
    {
        if (sheetCache.TryGetValue(sheet, out var cached)) return cached;
        var all = Resources.LoadAll<Sprite>("Traps/" + sheet);
        System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
        sheetCache[sheet] = all;
        return all;
    }

    void Update()
    {
        if (player == null)
        {
            player = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
            if (player == null) return;
        }
        if (StageDone)
        {
            // Every monster is down: the reward chest and the exit log can land anywhere, so the traps go quiet.
            sr.color = Color.Lerp(sr.color, new Color(1f, 1f, 1f, 0f), Time.deltaTime * 4f);
            if (sr.color.a < 0.05f) Destroy(gameObject);
            return;
        }
        // Only on screen: a trap on another floor (or behind a seal) never fires out of sight.
        if (Vector2.Distance(player.transform.position, transform.position) > ActiveRange || !OnScreen()) return;

        if (kind == Kind.Saw) { UpdateSaw(); return; }
        if (kind == Kind.Spikes) { UpdateSpikes(); return; }
        if (kind == Kind.Arrow) { UpdateArrow(); return; }

        timer += Time.deltaTime;
        int n = frames.Length;
        int warnFrames = Mathf.Max(1, n / 4);
        if (state == 0)
        {
            sr.sprite = frames[0];
            sr.color = new Color(1f, 1f, 1f, 0.55f);
            if (timer >= idleTime) { state = 1; timer = 0f; Warn(); }
        }
        else if (state == 1)
        {
            // Flicker the first frames, brighter and redder: something is coming.
            int f = Mathf.Min(warnFrames - 1, (int)(timer / WarnTime * warnFrames));
            sr.sprite = frames[f];
            sr.color = WarnColor();
            if (timer >= WarnTime) { state = 2; timer = 0f; }
        }
        else
        {
            float t = timer / burstTime;
            int f = warnFrames + Mathf.Min(n - warnFrames - 1, (int)(t * (n - warnFrames)));
            sr.sprite = frames[Mathf.Clamp(f, 0, n - 1)];
            sr.color = Color.white;
            if (t < 0.85f) TryHurt();
            if (timer >= burstTime) { state = 0; timer = 0f; }
        }
    }

    bool OnScreen()
    {
        var cam = Camera.main;
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(new Vector3(feet.x, feet.y + 1f, 0f));
        return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }

    public static bool StageDone => CardRewardChest.Instance != null || (StageExit.Instance != null && StageExit.Instance.Opened);

    Color WarnColor() => Color.Lerp(new Color(1f, 0.55f, 0.45f, 0.9f), Color.white, Mathf.PingPong(timer * 10f, 1f));

    void UpdateSaw()
    {
        sr.sprite = frames[(int)(Time.time * 20f) % frames.Length];
        if (railRight - railLeft > 0.5f)
        {
            var p = transform.position;
            p.x += sawDir * 2.2f * Time.deltaTime;
            if (p.x > railRight) { p.x = railRight; sawDir = -1f; }
            if (p.x < railLeft) { p.x = railLeft; sawDir = 1f; }
            transform.position = p;
        }
        TryHurt();
    }

    // Sheet order: frame 0 is slammed down, the last frame is fully raised.
    void UpdateSpikes()
    {
        timer += Time.deltaTime;
        int n = frames.Length;
        var rest = new Vector3(feet.x, feet.y + frames[0].bounds.size.y * 0.5f, 0f);
        if (state == 0)
        {
            sr.sprite = frames[n - 1];
            sr.color = Color.white;
            transform.position = rest;
            if (timer >= idleTime) { state = 1; timer = 0f; Warn(); }
        }
        else if (state == 1)
        {
            sr.color = WarnColor();
            transform.position = rest + Vector3.right * Mathf.Sin(timer * 70f) * 0.05f; // rattles on its chains
            if (timer >= WarnTime) { state = 2; timer = 0f; transform.position = rest; }
        }
        else
        {
            sr.color = Color.white;
            const float Drop = 0.12f, Hold = 0.35f;
            if (timer < Drop) sr.sprite = frames[Mathf.Clamp((int)((1f - timer / Drop) * (n - 1)), 0, n - 1)];
            else if (timer < Drop + Hold)
            {
                if (sr.sprite != frames[0]) { CameraFollow.Shake(0.08f, 0.1f); Sfx.Play("land_heavy", 0.5f, 1.2f); }
                sr.sprite = frames[0];
                TryHurt();
            }
            else
            {
                float k = Mathf.Clamp01((timer - Drop - Hold) / (burstTime - Drop - Hold));
                sr.sprite = frames[Mathf.Clamp((int)(k * (n - 1)), 0, n - 1)];
            }
            if (timer >= burstTime) { state = 0; timer = 0f; }
        }
    }

    void UpdateArrow()
    {
        timer += Time.deltaTime;
        int n = frames.Length;
        const int PopFrames = 8; // frames 0-7: rises out of the floor and aims
        if (state == 0)
        {
            sr.sprite = frames[0];
            sr.color = Color.white;
            if (timer >= idleTime) { state = 1; timer = 0f; shot = false; Warn(); }
        }
        else if (state == 1)
        {
            sr.flipX = player.transform.position.x > transform.position.x; // the bow points left in the sheet
            sr.sprite = frames[Mathf.Min(PopFrames - 1, (int)(timer / ArrowWarnTime * PopFrames))];
            sr.color = new Color(1f, 0.6f, 0.5f); // steady: the blink hid the bow it was meant to show
            ShowAim(true);
            if (timer >= ArrowWarnTime) { state = 2; timer = 0f; }
        }
        else
        {
            sr.color = Color.white;
            float k = timer / burstTime;
            sr.sprite = frames[Mathf.Clamp(PopFrames + (int)(k * (n - PopFrames)), 0, n - 1)];
            if (!shot)
            {
                shot = true;
                ShowAim(false);
                float dir = sr.flipX ? 1f : -1f;
                TrapArrow.Fire(transform.position + new Vector3(dir * 0.45f, 0.05f, 0f), dir);
                Sfx.Play("swing", 0.6f, 1.8f);
            }
            if (timer >= burstTime) { state = 0; timer = 0f; }
        }
    }

    const float ArrowWarnTime = 1f; // (the other traps' 0.7 s was too short to read which way the bow points)

    // A thin steady red line from the bow to the first wall (14 tiles at most, the arrow's range): where it will fly.
    void ShowAim(bool on)
    {
        if (!on) { if (aim != null) aim.SetActive(false); return; }
        if (aim == null)
        {
            aim = new GameObject("TrapAim");
            var line = aim.AddComponent<SpriteRenderer>();
            line.sprite = RuntimeGeneratedAssets.WhiteSprite();
            line.color = new Color(1f, 0.3f, 0.25f, 0.6f);
            line.sortingOrder = 8;
        }
        float dir = sr.flipX ? 1f : -1f, length = 0f;
        var map = StageMap.Instance;
        Vector3 from = transform.position + new Vector3(dir * 0.45f, 0.05f, 0f);
        while (length < 14f && (map == null || !map.IsSolid(from + Vector3.right * dir * (length + 0.5f)))) length += 0.5f;
        aim.transform.position = from + Vector3.right * dir * length * 0.5f;
        aim.transform.localScale = new Vector3(Mathf.Max(0.5f, length), 0.09f, 1f);
        aim.SetActive(true);
    }

    void OnDestroy() { if (aim != null) Destroy(aim); }

    void Warn()
    {
        if (Vector2.Distance(player.transform.position, transform.position) < 10f)
            DamagePopup.SpawnLabel(new Vector3(feet.x, feet.y + 2.4f, 0f), "!", new Color(1f, 0.55f, 0.35f), 0.8f);
    }

    void TryHurt()
    {
        Vector2 c = kind == Kind.Saw ? (Vector2)transform.position : feet + hurtOffset;
        if (Hits(player, c, halfSize))
            PlayerStats.Instance.TakeDamage(Damage, kind == Kind.Fire ? Loc.T("trap.fire") : kind == Kind.Toxic ? Loc.T("trap.poison")
                                                   : kind == Kind.Spikes ? Loc.T("trap.spike") : Loc.T("trap.saw"));
    }

    public static bool Hits(PlayerController2D player, Vector2 centre, Vector2 half)
    {
        if (player == null || player.IsDead || PlayerStats.Instance == null) return false;
        Vector2 f = player.Feet;
        float w = player.feetHalfWidth;
        float top = f.y + player.feetOffset * 2f;
        return f.x + w > centre.x - half.x && f.x - w < centre.x + half.x &&
               top > centre.y - half.y && f.y < centre.y + half.y;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => sheetCache.Clear();
}

// The arrow trap's shot: flies straight until it hits a wall, the player, or 14 tiles.
public class TrapArrow : MonoBehaviour
{
    const float Speed = 9f, MaxDistance = 14f;
    float dir, travelled;
    PlayerController2D player;
    static Sprite sprite;

    public static void Fire(Vector3 from, float dir)
    {
        var go = new GameObject("TrapArrow");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 2f; // 12 x 3 px at x1 was a hair, seen too late to dodge (the hit box is unchanged)
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ArrowSprite();
        sr.flipX = dir > 0f;
        sr.sortingOrder = 9;
        var arrow = go.AddComponent<TrapArrow>();
        arrow.dir = dir;
        arrow.player = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
    }

    void Update()
    {
        if (TrapHazard.StageDone) { Destroy(gameObject); return; }
        float step = Speed * Time.deltaTime;
        transform.position += Vector3.right * dir * step;
        travelled += step;
        var map = StageMap.Instance;
        if (travelled > MaxDistance || (map != null && map.IsSolid(transform.position))) { Destroy(gameObject); return; }
        if (TrapHazard.Hits(player, transform.position, new Vector2(0.3f, 0.12f)))
        {
            PlayerStats.Instance.TakeDamage(TrapHazard.Damage, Loc.T("trap.arrow"));
            Destroy(gameObject); // a dodged arrow is spent too
        }
    }

    // 12x3 px arrow pointing left: head, shaft, fletching.
    static Sprite ArrowSprite()
    {
        if (sprite != null) return sprite;
        var tex = RuntimeGeneratedAssets.Texture(12, 3);
        var clear = new Color32(0, 0, 0, 0);
        var head = new Color32(0xd8, 0xd8, 0xe0, 0xff);
        var shaft = new Color32(0x7a, 0x52, 0x30, 0xff);
        var feather = new Color32(0xe8, 0x55, 0x58, 0xff);
        var px = new Color32[36];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        for (int x = 1; x < 10; x++) px[1 * 12 + x] = shaft;
        px[1 * 12 + 0] = head; px[0 * 12 + 1] = head; px[2 * 12 + 1] = head;
        px[0 * 12 + 10] = feather; px[2 * 12 + 10] = feather; px[1 * 12 + 11] = feather;
        tex.SetPixels32(px);
        tex.Apply();
        sprite = RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), 16f);
        return sprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => sprite = null;
}
