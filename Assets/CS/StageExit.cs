using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The way from one stage to the next, through portals - stone arches (created by RunManager in the stage scenes;
// they were hollow logs until 2026-11-09, which is why the code still says "log" for the sprite):
//   Closed - from the start, the portals stand at the end of the stage, barred by chains or vines; walking into one
//            shakes it and says 적을 모두 물리치세요.
//   Open   - after the draft card is picked the vines blow off (dust, a creak), a wind swirls in the mouth and
//            fireflies string out a path to it; leaves near it are pulled in, an edge arrow (MonsterTracker) and the
//            minimap show the way. A stage where the end is too far (or a climbing one) gets a single log a few steps
//            away instead, popping up out of the ground.
//   Fork   - stages 1-3 end at a fork: two logs, the hard way (사냥 길: thorns, a skull, a red swirl - elites and
//            treasure next stage) and the easy way (안전 길: flowers, a green swirl - fewer monsters). Each has a sign
//            and a head icon (skull / sprout: shape as well as colour). Walking into one picks that route
//            (RouteProgress) and shuts the other at once.
//   In     - walking into an open mouth: the cat is locked, pulled to the mouth stretching, then shrinking and
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
    const float ClosedWordTime = 1.5f;  // 적을 모두 물리치세요 stays this long (a new bump restarts it, never a second copy)

    // One log: a way out. `kind` is its art ("" plain, "_crimson" boss, "_hunt" / "_safe" the fork).
    class Way
    {
        public SpriteRenderer log;
        public Sprite[] swirl;
        public string kind;
        public float holeX, floorY, holeSide; // holeSide: -1 = the mouth faces left (the cat comes from the left)
        public bool open, shut;               // shut: the other fork was taken
        public bool hunt;                     // the fork's hard way
        public float bumpCooldown, wordLeft;
        public TextMeshPro word;              // the closed message over it
        public Transform sign;                // the fork's sign and icon
        // The fork's two are portals (a standing arch, its opening's middle 1.6 up), the others logs (mouth at the cut end).
        public bool Portal => true; // every way out is a portal now (2026-11-09); the log geometry is kept for reference
        public float Offset => Portal ? 0f : holeSide * HoleFromCentre; // from the hole to the sprite's middle
        public Vector3 signHome;
        public SpriteRenderer rim;
        public Sprite[] rims;
        public Vector3 Mouth => Portal ? new Vector3(holeX, floorY + 1.6f, 0f) : new Vector3(holeX - holeSide * 0.2f, floorY + 1.3f, 0f);
    }

    readonly List<Way> ways = new List<Way>();
    Way taken;                 // the way being entered
    PlayerController2D body;
    string nextScene;
    System.Action<bool> chooseRoute; // the fork: true = 사냥 길
    bool open, entering, crimson, fork;
    float frameClock, pullIn;
    readonly List<Firefly> flies = new List<Firefly>();
    readonly List<Transform> drawn = new List<Transform>();
    Sprite[] flyFrames, leafSprites;
    static string arrivedKind = "_plain"; // the portal the cat left the last stage by: it pops out of the same kind

    class Firefly { public Transform tr; public SpriteRenderer sr; public Vector3 home; public float phase; public bool gone; }

    public static StageExit Create(int stageNumber, bool fork = false)
    {
        var go = new GameObject("StageExit");
        var s = go.AddComponent<StageExit>();
        s.stage = stageNumber;
        s.fork = fork;
        return s;
    }

    int stage;

    // The way out, once open (the minimap and the edge arrows mark each one).
    public bool IsOpen => open && !entering;
    public bool Opened => open; // the stage is done: traps switch off (TrapHazard)
    public bool IsFork => fork && ways.Count == 2;
    public Vector2 ExitPoint => ways.Count > 0 ? Point(ways[0]) : Vector2.zero;
    public int WayCount => ways.Count;
    public Vector2 WayPoint(int i) => Point(ways[i]);
    public bool WayOpen(int i) => open && !entering && ways[i].open && !ways[i].shut;
    public bool WayIsHunt(int i) => ways[i].hunt;
    // The fork's colours: red for the hard way, green for the easy one; 색약: vermilion and yellow (the shapes differ too).
    public Color WayColor(int i)
    {
        var w = ways[i];
        if (w.kind == "_hunt") return GameSettings.Colorblind ? new Color(0.93f, 0.39f, 0.04f) : new Color(0.9f, 0.21f, 0.31f);
        if (w.kind == "_safe") return GameSettings.Colorblind ? UiTheme.ExitMark : new Color(0.52f, 0.84f, 0.15f);
        return GameSettings.Colorblind ? UiTheme.ExitMark : new Color(0.35f, 0.9f, 1f);
    }
    // The fork's way for a route (the full-run bot and the tests walk in by it).
    public Vector2 RoutePoint(bool hunt)
    {
        foreach (var w in ways) if (w.hunt == hunt && (w.kind == "_hunt" || w.kind == "_safe")) return Point(w);
        return ExitPoint;
    }
    static Vector2 Point(Way w) => new Vector2(w.holeX, w.floorY + 0.5f);

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

    static Sprite Load(string kind, string state) => Resources.Load<Sprite>($"StageExit/exit_portal{kind}_{state}");
    // The single way on: plain (cyan), the way to the boss (stage 4: horns and a crown), the boss room's way to the
    // result (gold).
    string PlainKind => stage >= 5 ? "_victory" : stage == 4 ? "_boss" : "_plain";

    // ------------------------------------------------------------------ arriving
    void Arrive()
    {
        StageWipe.ArrivedThroughExit = false;
        // The cat waits inside a log at the start until the iris has opened, then pops out.
        var feet = body.Feet;
        // out of the same kind of portal it went in by
        var arrive = MakeLog(feet.x, feet.y, Load(arrivedKind, "open_0") ?? Load("_plain", "open_0"));
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

    Way AddWay(Vector2 spot, float holeSide, string kind, bool closed)
    {
        var w = new Way { kind = kind, holeX = spot.x, floorY = spot.y, holeSide = holeSide, hunt = kind == "_hunt" };
        w.log = MakeLog(spot.x - w.Offset, spot.y, Load(kind, closed ? "closed" : "open_0"));
        w.log.flipX = holeSide > 0f;
        w.log.sortingOrder = body.Renderer.sortingOrder - 1; // the cat stays in front of it, shrinking into the swirl
        ways.Add(w);
        if (kind == "_hunt" || kind == "_safe") MakeSign(w);
        // still in front of a big prop (no clear spot): a dark silhouette behind it so the arch reads against it
        if (PropsNear(spot))
        {
            var plate = new GameObject("Backdrop").AddComponent<SpriteRenderer>();
            plate.transform.SetParent(w.log.transform, false);
            plate.transform.localScale = new Vector3(1.12f, 1.06f, 1f);
            plate.sprite = w.log.sprite;
            plate.color = new Color(0.08f, 0.06f, 0.08f, 0.9f);
            plate.sortingOrder = w.log.sortingOrder - 1;
        }
        // the opening's inner edge, blinking between two colours once open (RimBlink)
        var rimGo = new GameObject("Rim");
        rimGo.transform.SetParent(w.log.transform, false);
        w.rim = rimGo.AddComponent<SpriteRenderer>();
        w.rim.sortingOrder = w.log.sortingOrder + 1;
        w.rims = new[] { Resources.Load<Sprite>($"StageExit/exit_portal{kind}_rim_0"), Resources.Load<Sprite>($"StageExit/exit_portal{kind}_rim_1") };
        w.rim.enabled = false;
        return w;
    }

    // ------------------------------------------------------------------ the closed logs
    // A side-scrolling stage shows its way out from the start, shut: one log, or the fork's two. (Climbing stages put
    // it near the card chest when the stage is done, so there is no fixed end to show.)
    void PlaceClosed()
    {
        var map = StageMap.Instance;
        if (map == null || map.verticalLayout || map.spawnSlots == null || map.spawnSlots.Length == 0) return;
        if (fork)
        {
            float endX = float.MinValue;
            foreach (var s in map.spawnSlots) endX = Mathf.Max(endX, s.x);
            if (PlaceFork(map, s => s.x >= endX - 24f, closed: true)) return;
        }
        // the farthest roomy floor spot toward the stage's end (the last slot may sit on a ledge too short for it)
        bool found = false; Vector2 end = default;
        foreach (var s in map.spawnSlots)
            if ((!found || s.x > end.x) && Roomy(map, s)) { end = s; found = true; }
        if (!found) return;
        AddWay(end, -1f, PlainKind, closed: true);
    }

    // The fork's two logs among the roomy floor spots `near` allows: the hard way as high as it can go (위), the easy
    // way low (아래), far enough apart that one's pull never reaches the other. False if the place has no room for two.
    // The fork's two portals among the roomy floor spots `near` allows, side by side so both fit one screen (a choice
    // is only a choice when both answers are in view): 7-10 units apart (one's pull never reaches the other), at most
    // a portal's height (3) apart in height, clear of big props (trees, bushes) so they stand out, toward the stage's
    // end. The hard way takes the farther one. False if the place has no such pair.
    bool PlaceFork(StageMap map, System.Func<Vector2, bool> near, bool closed)
    {
        var spots = new List<Vector2>();
        foreach (var s in map.spawnSlots) if (near(s) && Roomy(map, s)) spots.Add(s);
        if (spots.Count < 2) return false;
        float bestScore = float.MaxValue; Vector2 a = default, b = default;
        for (int i = 0; i < spots.Count; i++)
            for (int j = 0; j < spots.Count; j++)
            {
                if (i == j || spots[j].x <= spots[i].x) continue;
                float dx = spots[j].x - spots[i].x, dy = Mathf.Abs(spots[j].y - spots[i].y);
                if (dx < 7f || dx > 10f || dy > 3f || !Walkable(map, spots[i], spots[j])) continue;
                float score = dy * 2f + Mathf.Abs(dx - 8f) - spots[j].x * 0.02f
                            + (PropsNear(spots[i]) ? 6f : 0f) + (PropsNear(spots[j]) ? 6f : 0f);
                if (score < bestScore) { bestScore = score; a = spots[i]; b = spots[j]; }
            }
        if (bestScore == float.MaxValue) return false;
        foreach (var w in ways) DestroyWay(w);
        ways.Clear();
        AddWay(b, -1f, "_hunt", closed); // the farther one: the harder way
        AddWay(a, -1f, "_safe", closed);
        return true;
    }

    // The cat can go from one portal to the other without a dash: walking the floor between them, any step up at most
    // 2.5 (a single jump reaches 14.5^2 / (2 * 32) = 3.3, so with room to spare) and any gap at most 2 wide (one jump
    // at the base speed 3 covers about 2.7). Read column by column, the highest floor near the two spots' heights.
    static bool Walkable(StageMap map, Vector2 a, Vector2 b)
    {
        float lo = Mathf.Min(a.y, b.y) - 1.5f, hi = Mathf.Max(a.y, b.y) + 0.6f;
        float last = a.y, gap = 0f;
        for (float x = a.x; x <= b.x; x += 0.5f)
        {
            float floor = float.NaN;
            for (float y = hi; y >= lo; y -= 0.25f)
            {
                var under = new Vector2(x, y - 0.15f);
                if ((map.IsSolid(under) || map.IsPlatform(under)) && !map.IsSolid(new Vector2(x, y + 0.4f))) { floor = y; break; }
            }
            if (float.IsNaN(floor)) { gap += 0.5f; if (gap > 2f) return false; continue; }
            if (Mathf.Abs(floor - last) > 2.5f) return false; // a step too high to jump back up (both ways)
            last = floor; gap = 0f;
        }
        return Mathf.Abs(last - b.y) <= 2.5f;
    }

    // Big props (trees, bushes: the scene's Props) within a portal's width of the spot - a green portal in front of a
    // green bush is lost in it.
    static readonly List<Bounds> props = new List<Bounds>();
    static bool propsRead;
    bool PropsNear(Vector2 spot)
    {
        if (!propsRead)
        {
            propsRead = true;
            props.Clear();
            var root = GameObject.Find("Props");
            if (root != null)
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.bounds.size.x > 1.2f || sr.bounds.size.y > 1.2f) props.Add(sr.bounds);
        }
        var zone = new Bounds(new Vector3(spot.x, spot.y + 1.5f, 0f), new Vector3(2.5f * 3f, 3f, 10f)); // the portal and a portal's width each side
        foreach (var bo in props) if (bo.Intersects(zone)) return true;
        return false;
    }

    // The fork's sign over a log: the route's name and what it means, with its icon (skull / sprout).
    void MakeSign(Way w)
    {
        var root = new GameObject("ForkSign").transform;
        root.position = new Vector3(w.holeX - w.Offset, w.floorY + 3.75f, 0f); // over the keystone with a gap: never on the arch
        w.sign = root;
        w.signHome = root.position;
        float scale = GameSettings.LargeText ? 1.3f : 1f;
        var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
        icon.transform.SetParent(root, false);
        icon.sprite = Resources.Load<Sprite>(w.hunt ? "StageExit/exit_icon_hunt" : "StageExit/exit_icon_safe");
        icon.sortingOrder = 57;
        // light words on the dark strip (a red word on a red portal can't be read): the colour is the icon's alone
        var text = MakeWord(root, Loc.T(w.hunt ? "route.sign.hunt" : "route.sign.safe"), w.hunt ? new Color(0.95f, 0.63f, 0.37f) : new Color(0.76f, 0.85f, 0.44f), scale);
        text.transform.localPosition = Vector3.zero;
        icon.transform.localPosition = new Vector3(-text.textBounds.extents.x - 0.55f, 0f, 0f);
    }

    Color WayColorOf(Way w) => WayColor(ways.IndexOf(w) >= 0 ? ways.IndexOf(w) : 0);

    // A short line of world text on a dark strip (the fork's signs and the closed message).
    TextMeshPro MakeWord(Transform parent, string label, Color color, float scale)
    {
        var go = new GameObject("Word");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshPro>();
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null) tmp.font = hud.font;
        tmp.text = label;
        tmp.fontSize = 3f * scale;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);
        tmp.sortingOrder = 56;
        tmp.ForceMeshUpdate();
        var back = new GameObject("Back").AddComponent<SpriteRenderer>();
        back.transform.SetParent(go.transform, false);
        back.sprite = RuntimeGeneratedAssets.WhiteSprite(); // 1 unit square
        back.color = new Color(0.08f, 0.05f, 0.08f, 0.85f);
        back.sortingOrder = 54;
        var b = tmp.textBounds;
        back.transform.localPosition = new Vector3(b.center.x, b.center.y, 0f);
        back.transform.localScale = new Vector3(b.size.x + 0.4f, b.size.y + 0.2f, 1f);
        return tmp;
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

    // The fork: both logs open; walking into one calls `choose(hunt)` and goes on to `next`. False when this stage has
    // no room for two logs here (the caller falls back to the route window).
    public bool OpenFork(string next, System.Action<bool> choose)
    {
        var map = StageMap.Instance;
        if (open || body == null || map == null || string.IsNullOrEmpty(next)) return false;
        if (!IsFork)
        {
            // no closed fork (a climbing stage, or no room at the end): two logs near the cat, on its floor or close
            Vector2 at = body.Feet;
            if (!PlaceFork(map, s => Mathf.Abs(s.x - at.x) <= 14f && Mathf.Abs(s.y - at.y) <= 6f, closed: false)) return false;
            foreach (var w in ways) StartCoroutine(Appear(w.log));
        }
        fork = true;
        chooseRoute = choose;
        OpenAll(next);
        // Frame both: the camera eases over to the pair's middle, holds a moment and comes back to the cat (position
        // only - the Pixel Perfect camera keeps its whole-number zoom; the controls stay live).
        if (ways.Count == 2) CameraFollow.Focus((Point(ways[0]) + Point(ways[1])) * 0.5f + Vector2.up * 1f, 1.4f);
        return true;
    }

    public bool Open(string next)
    {
        var map = StageMap.Instance;
        if (open || body == null || map == null || string.IsNullOrEmpty(next)) return false;
        Vector2 feet = body.Feet;
        if (IsFork || (ways.Count == 1 && Mathf.Abs(ways[0].holeX - feet.x) <= FarEnd))
        {
            // the closed log(s) at the end open where they are - a fork opened without a choice (OpenFork) keeps only
            // one log, the plain way
            if (IsFork) { for (int i = 1; i < ways.Count; i++) DestroyWay(ways[i]); ways.RemoveRange(1, ways.Count - 1); ways[0].kind = PlainKind; ways[0].hunt = false; if (ways[0].sign != null) Destroy(ways[0].sign.gameObject); }
        }
        else
        {
            foreach (var w in ways) DestroyWay(w);
            ways.Clear();
            Vector2 spot = PickSpot(map, feet);
            var way = AddWay(spot, feet.x <= spot.x ? -1f : 1f, PlainKind, closed: false); // the mouth faces the cat
            StartCoroutine(Appear(way.log));
        }
        OpenAll(next);
        return true;
    }

    void DestroyWay(Way w)
    {
        if (w.log != null) Destroy(w.log.gameObject);
        if (w.sign != null) Destroy(w.sign.gameObject);
        if (w.word != null) Destroy(w.word.gameObject);
    }

    void OpenAll(string next)
    {
        open = true;
        nextScene = next;
        foreach (var w in ways)
        {
            w.open = true;
            w.swirl = new[] { Load(w.kind, "open_0"), Load(w.kind, "open_1"), Load(w.kind, "open_2"), Load(w.kind, "open_3") };
            if (w.word != null) { Destroy(w.word.gameObject); w.word = null; }
            StartCoroutine(Unblock(w));
        }
        Vector2 feet = body.Feet;
        foreach (var w in ways) StartCoroutine(Gather(feet, w));
    }

    // The vines blow off: a puff of leaves and dust at the mouth, a creak, a little shudder, then the swirl.
    IEnumerator Unblock(Way w)
    {
        var mouth = new Vector3(w.holeX, w.floorY + 1.2f, 0f);
        Vfx.Play("dust_mid", mouth, 1.4f);
        Vfx.Play("smoke", mouth, 0.5f);
        Sfx.Play("land_heavy", 0.6f, 1.4f);
        Sfx.Play("fx_suck", 0.5f, 1.3f); // the wind starts up
        LeafBurst(w, mouth, 6);
        var tr = w.log.transform;
        Vector3 home = tr.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            tr.position = home + Vector3.right * Mathf.Round(Mathf.Sin(t * 70f) * 1.5f) / 16f; // whole pixels
            yield return null;
        }
        tr.position = home;
        w.log.sprite = w.swirl[0];
        if (w.rim != null) w.rim.enabled = true;
        StartCoroutine(Burst(w));
    }

    // The moment a portal opens: one ring of light spreads from its opening (0.2 s), in the portal's colour.
    IEnumerator Burst(Way w)
    {
        var sprite = Resources.Load<Sprite>("StageExit/exit_portal_burst");
        if (sprite == null) yield break;
        var sr = new GameObject("PortalBurst").AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = w.log.sortingOrder + 2;
        sr.transform.position = w.Mouth;
        Color col = w.kind == "_hunt" ? new Color(1f, 0.6f, 0.3f) : w.kind == "_safe" ? new Color(0.75f, 0.95f, 0.45f)
                  : w.kind == "_boss" ? new Color(0.97f, 0.46f, 0.6f) : w.kind == "_victory" ? new Color(1f, 0.9f, 0.45f) : new Color(0.66f, 0.85f, 0.98f);
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            float k = t / 0.2f;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.6f, k);
            sr.color = k < 0.5f ? col : new Color(col.r, col.g, col.b, 0.5f); // two steps, then gone
            yield return null;
        }
        Destroy(sr.gameObject);
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

    // Room for the 4-unit log (or the 2.5-unit portal, 3 tall) on flat floor: both ends on ground, nothing solid through
    // its height, and open sky (no ground, no ledge) up to 1.5 times the portal's height - jammed under the floor above
    // it reads as stuck.
    const float Headroom = 3f * 1.5f;
    static bool Roomy(StageMap map, Vector2 p)
    {
        for (float dx = -2f; dx <= 2.01f; dx += 0.8f)
        {
            if (map.IsSolid(new Vector2(p.x + dx, p.y + 0.4f)) || map.IsSolid(new Vector2(p.x + dx, p.y + 1.4f)) || map.IsSolid(new Vector2(p.x + dx, p.y + 2.3f))) return false;
            for (float dy = 2.6f; dy <= Headroom; dy += 0.5f)
            {
                var above = new Vector2(p.x + dx, p.y + dy);
                if (map.IsSolid(above) || map.IsPlatform(above)) return false;
            }
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
            if (tr == null) yield break;
            tr.localScale = new Vector3(2f - s, s, 1f);
            yield return null;
        }
        if (tr != null) tr.localScale = Vector3.one;
    }

    // Fireflies rise around the cat, then string out along the way to a log's mouth.
    IEnumerator Gather(Vector2 from, Way w)
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
        Vector2 hole = new Vector2(w.holeX, w.floorY + 0.8f);
        float dist = Vector2.Distance(hole, from);
        int n = Mathf.Clamp(Mathf.RoundToInt(dist / 1.1f), 4, ways.Count > 1 ? 10 : 18);
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

    // The fork's signs stay on screen: one over a portal on a high ledge is pulled down to the screen's top edge (and
    // in from the sides) instead of being cut off.
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        // Measured on what the camera really draws into (its pixel rect: the window, or a capture's render texture),
        // never Screen.* - in a batch capture the "screen" is tiny while the picture is 1920x1080.
        float W = cam.pixelWidth, H = cam.pixelHeight;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        foreach (var w in ways)
        {
            if (w.rim != null && w.rim.enabled && w.rims != null) w.rim.sprite = w.rims[(int)(Time.time / 0.25f) % 2]; // a slow two-colour blink
            if (w.sign == null) continue;
            var p = w.signHome;
            // (no sideways clamp: a portal off screen takes its sign with it - the edge arrow shows the way there; pulled in
            // from the side it landed on the other portal's sign)
            float topY = c.y + halfH - 0.9f;
            // pushed down by the screen's top it would sit on the arch: beside the portal instead (on the side the cat
            // comes from, at the opening's height), never over it or away from it
            if (p.y > topY) { p.y = Mathf.Min(w.floorY + 2.2f, topY); p.x = w.holeX + w.holeSide * 4.4f; }
            // the screen-fixed UI's corners (HUD top left, minimap and legend top right) are a safe area: a world sign
            // never goes under them (UI_STYLE.md)
            Vector3 sp = cam.WorldToScreenPoint(p);
            float u = H / 1080f;
            // the sign's own extent (icon on the left, words to the right), not just its middle
            float left = cam.WorldToScreenPoint(p - new Vector3(3.3f, 0f, 0f)).x, right = cam.WorldToScreenPoint(p + new Vector3(2.7f, 0f, 0f)).x;
            bool inHud = left < 560f * u && sp.y > H - 360f * u;
            bool inMap = right > W - 480f * u && sp.y > H - 260f * u;
            // a banner up (starter card, set bonus, stage clear) holds the top middle too while it lasts
            bool inBanner = StageBanner.Up && right > W * 0.5f - 400f * u && left < W * 0.5f + 400f * u
                            && sp.y > H * 0.5f + 40f * u && sp.y < H * 0.5f + 340f * u;
            // under the HUD, the minimap or a banner the sign stands down until the way is clear (pushed down instead it
            // landed on its own portal); the portal's keystone - skull or sprout - still says which way it is
            bool show = !w.shut && !(inHud || inMap || inBanner); // (the way not taken keeps its sign down)
            if (w.sign.gameObject.activeSelf != show) w.sign.gameObject.SetActive(show);
            w.sign.position = p;
        }
    }

    void Update()
    {
        if (body == null || ways.Count == 0) return;
        foreach (var w in ways) UpdateWord(w);
        if (!open) { foreach (var w in ways) Bump(w); return; }

        // the swirls in the mouths; faster while one swallows the cat
        frameClock += Time.deltaTime * (entering ? SuckFps : SwirlFps);
        foreach (var w in ways)
            if (w.swirl != null && !w.shut && w.log != null && w.log.transform.localScale == Vector3.one)
                w.log.sprite = w.swirl[(int)frameClock % w.swirl.Length];

        // Fireflies: drift to their place on the path, bob, and blink; the one the cat passes flies on into the log.
        Vector3 into0 = taken != null ? taken.Mouth : ways[0].Mouth;
        foreach (var f in flies)
        {
            if (f.tr == null) continue;
            f.phase += Time.deltaTime;
            Vector3 target = f.home + new Vector3(Mathf.Sin(f.phase * 1.3f) * 0.12f, Mathf.Sin(f.phase * 2.1f) * 0.15f, 0f);
            if (!f.gone && !entering && Mathf.Abs(body.transform.position.x - f.home.x) < 0.5f && Mathf.Abs(body.transform.position.y - f.home.y) < 2f)
                f.gone = true;
            if (f.gone || entering) target = entering ? into0 : NearestMouth(f.home);
            f.tr.position = Vector3.Lerp(f.tr.position, target, 1f - Mathf.Exp(-(f.gone ? 6f : 3.5f) * Time.deltaTime));
            bool bright = Mathf.Sin(f.phase * 4f) > -0.2f;
            f.sr.sprite = flyFrames[bright ? 0 : 1];
            f.sr.color = new Color(1f, 1f, 1f, bright ? 1f : 0.65f);
            if ((f.gone || entering) && Vector2.Distance(f.tr.position, target) < 0.15f) { Destroy(f.tr.gameObject); f.tr = null; }
        }

        // Near an open log, loose leaves are pulled into its mouth: the way in, without a word.
        foreach (var w in ways)
        {
            if (w.shut) continue;
            float near = Mathf.Abs(body.transform.position.x - w.holeX) + Mathf.Abs(body.transform.position.y - w.floorY) * 0.5f;
            if (near < 7f || (entering && w == taken))
            {
                pullIn -= Time.deltaTime / ways.Count;
                if (pullIn <= 0f) { pullIn = entering ? 0.04f : 0.22f; Drawn(w); }
            }
        }
        for (int i = drawn.Count - 1; i >= 0; i--)
        {
            var d = drawn[i];
            if (d == null) { drawn.RemoveAt(i); continue; }
            Vector3 to = NearestMouth(d.position);
            d.position = Vector3.MoveTowards(d.position, to, (entering ? 9f : 4f) * Time.deltaTime);
            d.Rotate(0f, 0f, 400f * Time.deltaTime);
            float left = Vector2.Distance(d.position, to);
            d.localScale = Vector3.one * Mathf.Clamp01(left / 1.5f + 0.2f);
            if (left < 0.1f) { Destroy(d.gameObject); drawn.RemoveAt(i); }
        }

        // Walking into an open mouth.
        if (entering || body.IsAirborne) return;
        Vector2 feet = body.Feet;
        foreach (var w in ways)
        {
            if (!w.open || w.shut) continue;
            float into = (feet.x - w.holeX) * -w.holeSide; // > 0 once past the opening, toward the log's middle
            // only at the mouth: a cat past the log (behind it, or beyond) is not drawn in
            if (into > -0.45f && into < 1.2f && Mathf.Abs(feet.y - w.floorY) < 0.8f) { StartCoroutine(Enter(w)); return; }
        }
    }

    Vector3 NearestMouth(Vector3 p)
    {
        Way best = null; float bd = float.MaxValue;
        foreach (var w in ways)
        {
            if (w.shut) continue;
            float d = Vector2.Distance(p, w.Mouth);
            if (d < bd) { bd = d; best = w; }
        }
        return best != null ? best.Mouth : ways[0].Mouth;
    }

    // Walking into a closed log: it shudders and says why (one message; a new bump keeps it up longer).
    void Bump(Way w)
    {
        w.bumpCooldown -= Time.deltaTime;
        if (w.bumpCooldown > 0f || body.IsAirborne) return;
        Vector2 feet = body.Feet;
        if (Mathf.Abs(feet.x - w.holeX) > 0.6f || Mathf.Abs(feet.y - w.floorY) > 0.8f) return;
        w.bumpCooldown = 0.6f;
        StartCoroutine(Shudder(w));
        Sfx.Play("ui_error", 0.6f);
        if (w.word == null)
        {
            var at = new GameObject("ClosedWord").transform;
            at.position = w.Mouth + Vector3.up * 1.5f;
            w.word = MakeWord(at, Loc.T("exit.closed"), new Color(1f, 0.85f, 0.6f), GameSettings.LargeText ? 1.3f : 1f);
        }
        w.wordLeft = ClosedWordTime;
    }

    void UpdateWord(Way w)
    {
        if (w.word == null) return;
        w.wordLeft -= Time.deltaTime;
        float a = Mathf.Clamp01(w.wordLeft / 0.15f); // a quick fade at the end
        w.word.alpha = a;
        var back = w.word.GetComponentInChildren<SpriteRenderer>();
        if (back != null) back.color = new Color(0.08f, 0.05f, 0.08f, 0.85f * a);
        if (w.wordLeft <= 0f) { Destroy(w.word.transform.parent.gameObject); w.word = null; }
    }

    IEnumerator Shudder(Way w)
    {
        var tr = w.log.transform;
        Vector3 home = tr.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            tr.position = home + Vector3.right * Mathf.Round(Mathf.Sin(t * 80f) * 1.5f) / 16f;
            yield return null;
        }
        tr.position = home;
    }

    // A leaf from round a log, about to be pulled in.
    void Drawn(Way w)
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
        go.transform.position = w.Mouth + new Vector3(w.holeSide * Random.Range(1.2f, 3.4f), Random.Range(-1.0f, 1.6f), 0f); // from the open side
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = leafSprites[Random.Range(0, leafSprites.Length)];
        sr.sortingOrder = w.log.sortingOrder + 1;
        drawn.Add(go.transform);
    }

    void LeafBurst(Way w, Vector3 at, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Drawn(w);
            if (drawn.Count > 0) drawn[drawn.Count - 1].position = at + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.8f, 0.8f), 0f);
        }
    }

    // Pulled in: the controls lock, the cat is drawn to the mouth stretching toward it, then shrinks spinning in; the
    // camera leans in; the screen irises shut on the mouth and the next stage loads. The fork's other log shuts.
    IEnumerator Enter(Way w)
    {
        entering = true;
        taken = w;
        foreach (var other in ways)
        {
            if (other == w) continue;
            other.shut = true;
            if (other.log != null) other.log.sprite = Load(other.kind, "closed");
            if (other.sign != null) other.sign.gameObject.SetActive(false);
            if (other.rim != null) other.rim.enabled = false;
        }
        if (chooseRoute != null) chooseRoute(w.hunt);
        arrivedKind = w.kind;
        body.skillLock = true;
        body.skillHover = true;
        body.FaceTowards(-w.holeSide);
        Sfx.Play("fx_suck");
        var visual = body.visual != null ? body.visual : body.Renderer.transform;
        Vector3 scale = visual.localScale;
        Vector3 start = body.transform.position;
        Vector3 mouth = w.Mouth;
        // the cat's middle to the mouth's middle: lifted off the floor as it is drawn in
        Vector3 target = new Vector3(mouth.x, start.y + (mouth.y - 0.45f - body.Feet.y), start.z);
        var cam = Camera.main;
        var ppc = cam != null ? cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>() : null;
        float size0 = cam != null ? cam.orthographicSize : 0f;
        if (ppc != null) { if (ppc.orthographicSize > 0f) size0 = ppc.orthographicSize; ppc.enabled = false; } // it would hold the size
        // shake off in the settings: only a gentle lean in
        float zoom = GameSettings.ShakeStrength > 0f ? 0.86f : 0.95f;
        CameraFollow.Focus(mouth, 1.2f);
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
            visual.localRotation = Quaternion.Euler(0f, 0f, k < 0.5f ? 0f : (k - 0.5f) * 2f * 540f * w.holeSide);
            if (cam != null) cam.orthographicSize = Mathf.Lerp(size0, size0 * zoom, Mathf.SmoothStep(0f, 1f, k));
            if (!closing && k >= 0.55f)
            {
                closing = true;
                Vector2 mouthOnScreen = cam != null ? (Vector2)cam.WorldToScreenPoint(mouth) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (toResult != null) StageWipe.CloseAndOpen(mouthOnScreen, toResult);
                else SceneLoader.LoadThroughExit(nextScene, mouthOnScreen);
            }
            yield return null;
        }
        body.Renderer.enabled = false; // inside now (the iris covers the screen by then)
        visual.localScale = scale;
        visual.localRotation = Quaternion.identity;
    }

    // For the capture tool: the log(s) open, or the cat put at a mouth (the fork's hard way when `hunt`) so it is
    // being pulled in.
    public void ForCapture(bool pull, bool hunt = false)
    {
        if (!open) Open("Stage3");
        if (!pull || body == null || ways.Count == 0) return;
        Way w = ways[0];
        foreach (var x in ways) if (x.hunt == hunt) w = x;
        body.transform.position += new Vector3(w.holeX + w.holeSide * 0.3f - body.Feet.x, w.floorY - body.Feet.y, 0f); // feet on the floor
    }
}
