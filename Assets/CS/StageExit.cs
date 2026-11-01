using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The way from one stage to the next, through the forest instead of a portal (created by RunManager in the stage
// scenes):
//   Out - after the draft card is picked, a hollow log turns up on the floor at the end of the stage (or a few steps
//         ahead, if the end is far away) and fireflies gather into a dotted path to it. Walking into its open end,
//         the cat ducks and crawls in, and leaves sweep over the screen (LeafWipe) while the next stage loads.
//   In  - a stage entered that way opens with the leaves blowing away and the cat pops out of a log at the start
//         (the stage's title at the top is StageTitle's, shown on every entry).
// The art (log, fireflies) is pxl art in Resources/StageExit at the tiles' 16 px per unit.
public class StageExit : MonoBehaviour
{
    public static StageExit Instance { get; private set; }

    const float HoleFromCentre = 1f;   // the log's open end: 16 px left of the middle of the 48 px log
    const float FarEnd = 22f;          // the end of the stage is used when no further than this from the cat

    PlayerController2D body;
    SpriteRenderer log;
    float holeX, floorY, holeSide; // holeSide: -1 = the open end faces left (the cat comes from the left)
    string nextScene;
    bool open, entering;
    readonly List<Firefly> flies = new List<Firefly>();
    Sprite[] flyFrames;

    class Firefly { public Transform tr; public SpriteRenderer sr; public Vector3 home; public float phase; public bool gone; }

    public static StageExit Create(int stageNumber)
    {
        var go = new GameObject("StageExit");
        var s = go.AddComponent<StageExit>();
        s.stage = stageNumber;
        return s;
    }

    int stage;

    // The log's open end, once the way out has appeared (the minimap marks it).
    public bool IsOpen => open && !entering;
    public bool Opened => open; // the stage is done: traps switch off (TrapHazard)
    public Vector2 ExitPoint => new Vector2(holeX, floorY + 0.5f);

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; LeafWipe.Revealed -= PopOut; }

    void Start()
    {
        body = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
        if (body == null || !LeafWipe.ArrivedThroughLeaves) return;
        LeafWipe.ArrivedThroughLeaves = false;
        // Came through the leaves: the cat waits inside a log at the start until the curtain has blown away.
        var feet = body.Feet;
        var arrive = MakeLog(feet.x - HoleFromCentre, feet.y, false); // open end on the right, at the cat
        arrive.flipX = true;
        arrive.sortingOrder = body.Renderer.sortingOrder - 1;
        body.skillLock = true;
        body.Renderer.enabled = false;
        LeafWipe.Revealed += PopOut;
        if (!LeafWipe.Busy) PopOut(); // (no curtain running: straight out)
    }

    void PopOut()
    {
        LeafWipe.Revealed -= PopOut;
        if (body == null) return;
        StartCoroutine(PopOutRoutine());
    }

    IEnumerator PopOutRoutine()
    {
        yield return new WaitForSeconds(0.15f);
        body.Renderer.enabled = true;
        body.FaceTowards(1f);
        body.Launch(7f);
        Sfx.Play("jump", 1f, 1.2f);
        Vfx.Play("dust_small", body.Feet, 1f);
        for (float t = 0f; t < 0.22f; t += Time.deltaTime) { body.TryMove(3.2f * Time.deltaTime); yield return null; }
        body.skillLock = false;
    }

    SpriteRenderer MakeLog(float x, float feetY, bool crimson)
    {
        var go = new GameObject("ExitLog");
        go.transform.position = new Vector3(x, feetY - 0.06f, 0f); // sits a pixel into the grass
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Resources.Load<Sprite>(crimson ? "StageExit/exit_log_crimson" : "StageExit/exit_log");
        sr.sortingOrder = -47; // with the bushes and rocks
        return sr;
    }

    // ------------------------------------------------------------------ the way out
    // Called when the draft card is picked. Returns false if there is no stage map to put a log on (the caller then
    // just loads the next scene).
    public bool Open(string next)
    {
        var map = StageMap.Instance;
        if (open || body == null || map == null || string.IsNullOrEmpty(next)) return false;
        open = true;
        nextScene = next;
        Vector2 feet = body.Feet;
        Vector2 spot = PickSpot(map, feet);
        floorY = spot.y;
        // The open end faces the cat.
        holeSide = feet.x <= spot.x ? -1f : 1f;
        float centre = spot.x - holeSide * HoleFromCentre;
        holeX = spot.x;
        log = MakeLog(centre, floorY, next == "Stage5");
        log.flipX = holeSide > 0f;
        log.sortingOrder = body.Renderer.sortingOrder + 2; // the cat crawls in behind its front
        StartCoroutine(Appear(log));
        StartCoroutine(Gather(feet));
        return true;
    }

    // Where the log goes: the floor at the far end of the stage when that is close enough, otherwise a floor spot a
    // few steps ahead of the cat (or behind it, if there's none ahead). Floor spots are the map's spawn slots.
    Vector2 PickSpot(StageMap map, Vector2 feet)
    {
        var slots = map.spawnSlots;
        if (slots == null || slots.Length == 0) return feet + Vector2.right * 4f;
        Vector2 end = slots[0];
        if (map.verticalLayout)
        {
            // The card chest was opened beside the player. Keep the exit on this floor,
            // a few steps away, instead of making the player climb to the top again.
            Vector2 nearby = feet;
            float nearbyScore = float.MaxValue;
            foreach (var s in slots)
            {
                float dx = Mathf.Abs(s.x - feet.x), dy = Mathf.Abs(s.y - feet.y);
                if (dx < 3.5f || dx > 8f || dy > 1.2f || !Roomy(map, s)) continue;
                float score = Mathf.Abs(dx - 5f) + dy * 3f;
                if (score < nearbyScore) { nearbyScore = score; nearby = s; }
            }
            if (nearbyScore < float.MaxValue) return nearby;

            // A narrow floor may have no roomy point. Use the closest roomy ledge.
            foreach (var s in slots)
            {
                if (!Roomy(map, s)) continue;
                float score = Vector2.Distance(s, feet) + Mathf.Abs(s.y - feet.y) * 2f;
                if (score < nearbyScore) { nearbyScore = score; nearby = s; }
            }
            if (nearbyScore < float.MaxValue) return nearby;
        }
        foreach (var s in slots) if (s.x > end.x) end = s;
        if (Mathf.Abs(end.x - feet.x) <= FarEnd && Roomy(map, end)) return end;
        float ahead = body.FacingSign;
        Vector2 best = end; float bestScore = float.MaxValue;
        foreach (var s in slots)
        {
            float dx = (s.x - feet.x) * ahead;
            if (Mathf.Abs(s.y - feet.y) > 3f || Mathf.Abs(dx) < 3.5f || !Roomy(map, s)) continue;
            float score = Mathf.Abs(Mathf.Abs(dx) - 6f) + (dx < 0f ? 8f : 0f) + Mathf.Abs(s.y - feet.y) * 2f;
            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    // Room for the 3-unit log on flat floor (both ends on ground, nothing solid through it).
    static bool Roomy(StageMap map, Vector2 p)
    {
        for (float dx = -1.4f; dx <= 1.41f; dx += 0.7f)
        {
            if (map.IsSolid(new Vector2(p.x + dx, p.y + 0.4f)) || map.IsSolid(new Vector2(p.x + dx, p.y + 1.2f))) return false;
            var under = new Vector2(p.x + dx, p.y - 0.15f);
            if (!map.IsSolid(under) && !map.IsPlatform(under)) return false;
        }
        return true;
    }

    // The log pops up out of the undergrowth: a puff of leaves and dust, a little squash and stretch.
    IEnumerator Appear(SpriteRenderer sr)
    {
        var tr = sr.transform;
        Vfx.Play("dust_mid", tr.position, 1.4f);
        Sfx.Play("land_heavy", 0.6f, 1.3f);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            float s = k < 0.6f ? Mathf.Lerp(0.2f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f);
            tr.localScale = new Vector3(2f - s, s, 1f);
            yield return null;
        }
        tr.localScale = Vector3.one;
    }

    // Fireflies rise around the cat, then string out along the way to the log's open end.
    IEnumerator Gather(Vector2 from)
    {
        if (flyFrames == null)
        {
            var sheet = Resources.Load<Sprite>("StageExit/firefly");
            if (sheet == null) yield break;
            var tex = sheet.texture;
            flyFrames = new[]
            {
                Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 16f),
                Sprite.Create(tex, new Rect(5, 0, 5, 5), new Vector2(0.5f, 0.5f), 16f),
            };
        }
        Sfx.Play("card_pick", 0.5f, 1.6f);
        Vector2 hole = new Vector2(holeX, floorY + 0.55f);
        float dist = Vector2.Distance(hole, from);
        int n = Mathf.Clamp(Mathf.RoundToInt(dist / 1.1f), 4, 18);
        for (int i = 0; i < n; i++)
        {
            float k = (i + 1f) / (n + 1f);
            // Along the way, floating at about the cat's chest height, a gentle wave in it.
            var home = new Vector3(Mathf.Lerp(from.x, hole.x, k), Mathf.Lerp(from.y + 1f, hole.y + 0.4f, k) + Mathf.Sin(k * 9f) * 0.25f, 0f);
            var go = new GameObject("Firefly");
            go.transform.position = (Vector3)from + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.2f, 1.4f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = flyFrames[0];
            sr.sortingOrder = body.Renderer.sortingOrder + 3;
            flies.Add(new Firefly { tr = go.transform, sr = sr, home = home, phase = Random.Range(0f, 6f) });
            if (i % 3 == 0) Sfx.Play("ui_click", 0.25f, 2.2f + i * 0.04f);
            yield return new WaitForSeconds(0.05f);
        }
        // Two more hover at the open end.
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("Firefly");
            go.transform.position = (Vector3)from + Vector3.up;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = flyFrames[0];
            sr.sortingOrder = body.Renderer.sortingOrder + 3;
            flies.Add(new Firefly { tr = go.transform, sr = sr, home = (Vector3)hole + new Vector3(holeSide * (0.5f + 0.3f * i), 0.35f + 0.4f * i, 0f), phase = i * 2f });
        }
    }

    void Update()
    {
        if (!open) return;
        // Fireflies: drift to their place on the path, bob, and blink; the one the cat passes flies on into the log.
        foreach (var f in flies)
        {
            if (f.tr == null) continue;
            f.phase += Time.deltaTime;
            Vector3 target = f.home + new Vector3(Mathf.Sin(f.phase * 1.3f) * 0.12f, Mathf.Sin(f.phase * 2.1f) * 0.15f, 0f);
            if (!f.gone && !entering && body != null && Mathf.Abs(body.transform.position.x - f.home.x) < 0.5f && Mathf.Abs(f.home.x - holeX) > 1f)
                f.gone = true;
            if (f.gone) target = new Vector3(holeX, floorY + 0.5f, 0f);
            f.tr.position = Vector3.Lerp(f.tr.position, target, 1f - Mathf.Exp(-(f.gone ? 6f : 3.5f) * Time.deltaTime));
            bool bright = Mathf.Sin(f.phase * 4f) > -0.2f;
            f.sr.sprite = flyFrames[bright ? 0 : 1];
            f.sr.color = new Color(1f, 1f, 1f, bright ? 1f : 0.65f);
            if (f.gone && Vector2.Distance(f.tr.position, target) < 0.15f) { Destroy(f.tr.gameObject); f.tr = null; }
        }

        // Walking into the open end.
        if (entering || body == null || body.IsAirborne) return;
        Vector2 feet = body.Feet;
        float into = (feet.x - holeX) * -holeSide; // > 0 once past the opening, toward the log's middle
        if (into > -0.35f && Mathf.Abs(feet.y - floorY) < 0.6f) StartCoroutine(Enter());
    }

    // Duck, crawl in out of sight behind the log's front, and the leaves take over.
    IEnumerator Enter()
    {
        entering = true;
        body.skillLock = true;
        body.FaceTowards(-holeSide);
        Sfx.Play("dash", 0.7f, 1.3f);
        var visual = body.visual != null ? body.visual : body.Renderer.transform;
        Vector3 scale = visual.localScale;
        for (float t = 0f; t < 0.1f; t += Time.deltaTime) // duck
        {
            visual.localScale = new Vector3(scale.x * 1.1f, scale.y * 0.82f, scale.z);
            yield return null;
        }
        bool leaving = false;
        for (float t = 0f; t < 0.45f; t += Time.deltaTime) // crawl in
        {
            body.TryMove(-holeSide * 4f * Time.deltaTime);
            if (!leaving && t > 0.12f)
            {
                leaving = true;
                Vfx.Play("dust_small", new Vector3(holeX, floorY, 0f), 1f, flipX: holeSide < 0f);
                SceneLoader.LoadThroughLeaves(nextScene);
            }
            yield return null;
        }
        body.Renderer.enabled = false; // inside now (the leaves cover the screen by then)
        visual.localScale = scale;
    }
}
