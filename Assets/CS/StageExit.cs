using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The way from one stage to the next, through a big hollow log (created by RunManager in the stage scenes):
//   Closed - from the start, the log lies at the end of the stage with vines and a plank across its mouth; walking
//            into it shakes it and says 적을 모두 물리치세요.
//   Open   - after the draft card is picked the vines blow off (dust, a creak), a wind swirls in the mouth and
//            fireflies string out a path to it; leaves near it are pulled in, an edge arrow (MonsterTracker) shows
//            the way when it is off screen. A stage where the end is too far (or a climbing one) gets the log a few
//            steps away instead, popping up out of the ground.
//   In     - walking into the open mouth: the cat is locked, pulled to the mouth stretching, then shrinking and
//            spinning in; the camera leans in; the screen irises shut on the mouth (StageWipe) and the next stage
//            loads. About a second.
//   Out    - the next stage opens its iris on the cat inside a log at the start; it pops out, lands with a squash
//            and a puff of dust.
// The art is pxl (Tools/pxl/pxl_stage_exit.px.js) in Resources/StageExit at the tiles' 16 px per unit.
public class StageExit : MonoBehaviour
{
    public static StageExit Instance { get; private set; }
    // True while the cat is being pulled in: pause, the Tab sheet and the like keep out (PauseMenu, BuildSheet).
    public static bool Entering => Instance != null && Instance.entering;

    const float HoleFromCentre = 1.25f; // the mouth: 20 px left of the middle of the 64 px log
    const float FarEnd = 22f;           // a closed log at the stage's end is used if the cat is no further than this
    const float SwirlFps = 10f, SuckFps = 24f;

    PlayerController2D body;
    SpriteRenderer log;
    Sprite[] swirl;
    float holeX, floorY, holeSide; // holeSide: -1 = the mouth faces left (the cat comes from the left)
    string nextScene;
    bool open, entering, crimson;
    float frameClock, bumpCooldown, pullIn;
    readonly List<Firefly> flies = new List<Firefly>();
    readonly List<Transform> drawn = new List<Transform>();
    Sprite[] flyFrames, leafSprites;

    class Firefly { public Transform tr; public SpriteRenderer sr; public Vector3 home; public float phase; public bool gone; }

    public static StageExit Create(int stageNumber)
    {
        var go = new GameObject("StageExit");
        var s = go.AddComponent<StageExit>();
        s.stage = stageNumber;
        return s;
    }

    int stage;

    // The log's mouth, once the way out is open (the minimap and the edge arrow mark it).
    public bool IsOpen => open && !entering;
    public bool Opened => open; // the stage is done: traps switch off (TrapHazard)
    public Vector2 ExitPoint => new Vector2(holeX, floorY + 0.5f);

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; StageWipe.Revealed -= PopOut; }

    void Start()
    {
        body = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
        if (body == null) return;
        crimson = stage >= 4; // the way to the boss, and the boss room's way out
        if (StageWipe.ArrivedThroughExit) Arrive();
        PlaceClosed();
    }

    Sprite Load(string state) => Resources.Load<Sprite>($"StageExit/exit_logbig{(crimson ? "_crimson" : "")}_{state}");

    // ------------------------------------------------------------------ arriving
    void Arrive()
    {
        StageWipe.ArrivedThroughExit = false;
        // The cat waits inside a log at the start until the iris has opened, then pops out.
        var feet = body.Feet;
        var arrive = MakeLog(feet.x - HoleFromCentre, feet.y, Load("open_0"));
        arrive.flipX = true; // mouth on the right, at the cat
        arrive.sortingOrder = body.Renderer.sortingOrder - 1;
        body.skillLock = true;
        body.Renderer.enabled = false;
        StageWipe.Revealed += PopOut;
        if (!StageWipe.Busy) PopOut(); // (no iris running: straight out)
    }

    void PopOut()
    {
        StageWipe.Revealed -= PopOut;
        if (body == null) return;
        StartCoroutine(PopOutRoutine());
    }

    IEnumerator PopOutRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        body.Renderer.enabled = true;
        body.FaceTowards(1f);
        body.Launch(8f);
        Sfx.Play("fx_pop");
        Vfx.Play("dust_small", body.Feet, 1f);
        var visual = body.visual != null ? body.visual : body.Renderer.transform;
        Vector3 scale = visual.localScale;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime) // out, stretched tall, drifting right
        {
            visual.localScale = new Vector3(scale.x * 0.85f, scale.y * 1.2f, scale.z);
            body.TryMove(3.2f * Time.deltaTime);
            yield return null;
        }
        visual.localScale = scale;
        float until = Time.time + 1.2f;
        while (body.IsAirborne && Time.time < until) yield return null;
        // landing: a squash and a puff
        Vfx.Play("dust_mid", body.Feet, 1f);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.12f;
            visual.localScale = new Vector3(scale.x * (1f + 0.25f * k), scale.y * (1f - 0.22f * k), scale.z);
            yield return null;
        }
        visual.localScale = scale;
        body.skillLock = false;
    }

    SpriteRenderer MakeLog(float x, float feetY, Sprite sprite)
    {
        var go = new GameObject("ExitLog");
        go.transform.position = new Vector3(x, feetY - 0.06f, 0f); // sits a pixel into the grass
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -47; // with the bushes and rocks
        return sr;
    }

    // ------------------------------------------------------------------ the closed log
    // A side-scrolling stage shows its way out from the start, shut. (Climbing stages put it near the card chest
    // when the stage is done, so there is no fixed end to show.)
    void PlaceClosed()
    {
        var map = StageMap.Instance;
        if (map == null || map.verticalLayout || map.spawnSlots == null || map.spawnSlots.Length == 0) return;
        // the farthest roomy floor spot toward the stage's end (the last slot may sit on a ledge too short for it)
        bool found = false; Vector2 end = default;
        foreach (var s in map.spawnSlots)
            if ((!found || s.x > end.x) && Roomy(map, s)) { end = s; found = true; }
        if (!found) return;
        floorY = end.y;
        holeSide = -1f; // the cat comes from the left
        holeX = end.x;
        log = MakeLog(holeX - holeSide * HoleFromCentre, floorY, Load("closed"));
        log.sortingOrder = body.Renderer.sortingOrder - 1; // the cat stays in front of it, shrinking into the swirl
    }

    // ------------------------------------------------------------------ the way out
    // Called when the draft card is picked. Returns false if there is no stage map to put a log on (the caller then
    // just loads the next scene).
    // The boss room: the log leads to the result board (shown behind the iris by `show`) instead of a scene.
    System.Action toResult;
    public bool OpenToResult(System.Action show)
    {
        toResult = show;
        return Open("(result)");
    }

    public bool Open(string next)
    {
        var map = StageMap.Instance;
        if (open || body == null || map == null || string.IsNullOrEmpty(next)) return false;
        open = true;
        nextScene = next;
        swirl = new[] { Load("open_0"), Load("open_1"), Load("open_2"), Load("open_3") };
        Vector2 feet = body.Feet;
        if (log != null && Mathf.Abs(holeX - feet.x) <= FarEnd)
            StartCoroutine(Unblock()); // the closed log at the end opens where it is
        else
        {
            if (log != null) Destroy(log.gameObject);
            Vector2 spot = PickSpot(map, feet);
            floorY = spot.y;
            holeSide = feet.x <= spot.x ? -1f : 1f; // the mouth faces the cat
            holeX = spot.x;
            log = MakeLog(spot.x - holeSide * HoleFromCentre, floorY, swirl[0]);
            log.flipX = holeSide > 0f;
            log.sortingOrder = body.Renderer.sortingOrder - 1;
            StartCoroutine(Appear(log));
        }
        StartCoroutine(Gather(feet));
        return true;
    }

    // The vines blow off: a puff of leaves and dust at the mouth, a creak, a little shudder, then the swirl.
    IEnumerator Unblock()
    {
        var mouth = new Vector3(holeX, floorY + 1.2f, 0f);
        Vfx.Play("dust_mid", mouth, 1.4f);
        Vfx.Play("smoke", mouth, 0.5f);
        Sfx.Play("land_heavy", 0.6f, 1.4f);
        Sfx.Play("fx_suck", 0.5f, 1.3f); // the wind starts up
        LeafBurst(mouth, 6);
        var tr = log.transform;
        Vector3 home = tr.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            tr.position = home + Vector3.right * Mathf.Round(Mathf.Sin(t * 70f) * 1.5f) / 16f; // whole pixels
            yield return null;
        }
        tr.position = home;
        log.sprite = swirl[0];
    }

    // Where the log goes when the end is far: a floor spot a few steps ahead of the cat (or behind it), or near the
    // card chest on a climbing stage. Floor spots are the map's spawn slots.
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

    // Room for the 4-unit log on flat floor (both ends on ground, nothing solid through its 2.5-unit height).
    static bool Roomy(StageMap map, Vector2 p)
    {
        for (float dx = -2f; dx <= 2.01f; dx += 0.8f)
        {
            if (map.IsSolid(new Vector2(p.x + dx, p.y + 0.4f)) || map.IsSolid(new Vector2(p.x + dx, p.y + 1.4f)) || map.IsSolid(new Vector2(p.x + dx, p.y + 2.3f))) return false;
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

    // Fireflies rise around the cat, then string out along the way to the log's mouth.
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
        Vector2 hole = new Vector2(holeX, floorY + 0.8f);
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
    }

    void Update()
    {
        if (body == null || log == null) return;
        if (!open) { Bump(); return; }

        // the swirl in the mouth; faster while it swallows the cat
        frameClock += Time.deltaTime * (entering ? SuckFps : SwirlFps);
        if (swirl != null && log.transform.localScale == Vector3.one) log.sprite = swirl[(int)frameClock % swirl.Length];

        // Fireflies: drift to their place on the path, bob, and blink; the one the cat passes flies on into the log.
        foreach (var f in flies)
        {
            if (f.tr == null) continue;
            f.phase += Time.deltaTime;
            Vector3 target = f.home + new Vector3(Mathf.Sin(f.phase * 1.3f) * 0.12f, Mathf.Sin(f.phase * 2.1f) * 0.15f, 0f);
            if (!f.gone && !entering && Mathf.Abs(body.transform.position.x - f.home.x) < 0.5f && Mathf.Abs(f.home.x - holeX) > 1f)
                f.gone = true;
            if (f.gone || entering) target = Mouth;
            f.tr.position = Vector3.Lerp(f.tr.position, target, 1f - Mathf.Exp(-(f.gone ? 6f : 3.5f) * Time.deltaTime));
            bool bright = Mathf.Sin(f.phase * 4f) > -0.2f;
            f.sr.sprite = flyFrames[bright ? 0 : 1];
            f.sr.color = new Color(1f, 1f, 1f, bright ? 1f : 0.65f);
            if ((f.gone || entering) && Vector2.Distance(f.tr.position, target) < 0.15f) { Destroy(f.tr.gameObject); f.tr = null; }
        }

        // Near the open log, loose leaves are pulled into the mouth: the way in, without a word.
        float near = Mathf.Abs(body.transform.position.x - holeX);
        if (near < 7f || entering)
        {
            pullIn -= Time.deltaTime;
            if (pullIn <= 0f) { pullIn = entering ? 0.04f : 0.22f; Drawn(); }
        }
        for (int i = drawn.Count - 1; i >= 0; i--)
        {
            var d = drawn[i];
            if (d == null) { drawn.RemoveAt(i); continue; }
            Vector3 to = Mouth;
            d.position = Vector3.MoveTowards(d.position, to, (entering ? 9f : 4f) * Time.deltaTime);
            d.Rotate(0f, 0f, 400f * Time.deltaTime);
            float left = Vector2.Distance(d.position, to);
            d.localScale = Vector3.one * Mathf.Clamp01(left / 1.5f + 0.2f);
            if (left < 0.1f) { Destroy(d.gameObject); drawn.RemoveAt(i); }
        }

        // Walking into the open mouth.
        if (entering || body.IsAirborne) return;
        Vector2 feet = body.Feet;
        float into = (feet.x - holeX) * -holeSide; // > 0 once past the opening, toward the log's middle
        // only at the mouth: a cat past the log (behind it, or beyond) is not drawn in
        if (into > -0.45f && into < 1.2f && Mathf.Abs(feet.y - floorY) < 0.8f) StartCoroutine(Enter());
    }

    Vector3 Mouth => new Vector3(holeX - holeSide * 0.2f, floorY + 1.3f, 0f);

    // Walking into the closed log: it shudders and says why.
    void Bump()
    {
        bumpCooldown -= Time.deltaTime;
        if (bumpCooldown > 0f || body.IsAirborne) return;
        Vector2 feet = body.Feet;
        if (Mathf.Abs(feet.x - holeX) > 0.6f || Mathf.Abs(feet.y - floorY) > 0.8f) return;
        bumpCooldown = 1.6f;
        StartCoroutine(Shudder());
        Sfx.Play("ui_error", 0.6f);
        DamagePopup.SpawnLabel(Mouth + Vector3.up * 1.2f, Loc.T("exit.closed"), new Color(1f, 0.85f, 0.6f));
    }

    IEnumerator Shudder()
    {
        var tr = log.transform;
        Vector3 home = tr.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            tr.position = home + Vector3.right * Mathf.Round(Mathf.Sin(t * 80f) * 1.5f) / 16f;
            yield return null;
        }
        tr.position = home;
    }

    // A leaf from round the log, about to be pulled in.
    void Drawn()
    {
        if (leafSprites == null)
        {
            var list = new List<Sprite>();
            foreach (var n in crimson ? new[] { "red_a", "red_b", "gold_a" } : new[] { "green_a", "green_b", "gold_a" })
            {
                var s = Resources.Load<Sprite>("StageExit/leaf_" + n);
                if (s != null) list.Add(s);
            }
            leafSprites = list.ToArray();
        }
        if (leafSprites.Length == 0) return;
        var go = new GameObject("DrawnLeaf");
        go.transform.position = Mouth + new Vector3(holeSide * Random.Range(1.2f, 3.4f), Random.Range(-1.0f, 1.6f), 0f); // from the open side
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = leafSprites[Random.Range(0, leafSprites.Length)];
        sr.sortingOrder = log.sortingOrder + 1;
        drawn.Add(go.transform);
    }

    void LeafBurst(Vector3 at, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Drawn();
            if (drawn.Count > 0) drawn[drawn.Count - 1].position = at + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.8f, 0.8f), 0f);
        }
    }

    // Pulled in: the controls lock, the cat is drawn to the mouth stretching toward it, then shrinks spinning in; the
    // camera leans in; the screen irises shut on the mouth and the next stage loads.
    IEnumerator Enter()
    {
        entering = true;
        body.skillLock = true;
        body.skillHover = true;
        body.FaceTowards(-holeSide);
        Sfx.Play("fx_suck");
        var visual = body.visual != null ? body.visual : body.Renderer.transform;
        Vector3 scale = visual.localScale;
        Vector3 start = body.transform.position;
        // the cat's middle to the mouth's middle: lifted off the floor as it is drawn in
        Vector3 target = new Vector3(Mouth.x, start.y + (Mouth.y - 0.45f - body.Feet.y), start.z);
        var cam = Camera.main;
        var ppc = cam != null ? cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>() : null;
        float size0 = cam != null ? cam.orthographicSize : 0f;
        if (ppc != null) { if (ppc.orthographicSize > 0f) size0 = ppc.orthographicSize; ppc.enabled = false; } // it would hold the size
        // shake off in the settings: only a gentle lean in
        float zoom = GameSettings.ShakeStrength > 0f ? 0.86f : 0.95f;
        CameraFollow.Focus(Mouth, 1.2f);
        const float Pull = 1.0f;
        bool closing = false;
        for (float t = 0f; t < Pull; t += Time.deltaTime)
        {
            float k = t / Pull;
            body.transform.position = Vector3.Lerp(start, target, k * k);
            // stretch toward the mouth for the first half, then shrink and spin away
            float stretch = k < 0.5f ? k / 0.5f : 1f;
            float shrink = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            visual.localScale = new Vector3(scale.x * (1f + 0.45f * stretch) * shrink, scale.y * (1f - 0.3f * stretch) * shrink, scale.z);
            visual.localRotation = Quaternion.Euler(0f, 0f, k < 0.5f ? 0f : (k - 0.5f) * 2f * 540f * holeSide);
            if (cam != null) cam.orthographicSize = Mathf.Lerp(size0, size0 * zoom, Mathf.SmoothStep(0f, 1f, k));
            if (!closing && k >= 0.55f)
            {
                closing = true;
                Vector2 mouthOnScreen = cam != null ? (Vector2)cam.WorldToScreenPoint(Mouth) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (toResult != null) StageWipe.CloseAndOpen(mouthOnScreen, toResult);
                else SceneLoader.LoadThroughExit(nextScene, mouthOnScreen);
            }
            yield return null;
        }
        body.Renderer.enabled = false; // inside now (the iris covers the screen by then)
        visual.localScale = scale;
        visual.localRotation = Quaternion.identity;
    }

    // For the capture tool: the log open (or the cat put at its mouth so it is being pulled in).
    public void ForCapture(bool pull)
    {
        if (!open) Open("Stage3");
        if (pull && body != null) body.transform.position += new Vector3(holeX + holeSide * 0.3f - body.Feet.x, floorY - body.Feet.y, 0f); // feet on the floor
    }
}
