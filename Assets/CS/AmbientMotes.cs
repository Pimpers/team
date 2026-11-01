using UnityEngine;

// Drifting bits of atmosphere in a stage (added by StageSceneBuilder, one per stage mood):
//   Pollen - pale specks floating up in the morning forest      Leaves - warm leaves blowing down
//   Dust   - lavender motes hanging in the temple air           Spores - crimson spores rising: the
//   Embers - sparks rising off the boss hall's braziers                    corruption seeping in
// A few dozen 2x2-pixel sprites kept inside the camera's view (they wrap around its edges), so
// the cost doesn't depend on the map size. Snapped to the pixel grid to stay in the art style.
public class AmbientMotes : MonoBehaviour
{
    public enum Kind { Pollen, Leaves, Dust, Spores, Embers }

    [System.Serializable]
    public class Layer
    {
        public Kind kind;
        public int count = 24;
    }

    public Layer[] layers;
    public int sortingOrder = 6;

    class Mote
    {
        public Transform t;
        public SpriteRenderer sr;
        public Kind kind;
        public Vector2 pos, vel;
        public float phase, wobble, speed, size, spin;
        public Color color;
    }

    Mote[] motes;
    Camera cam;
    Sprite sprite;

    void Start()
    {
        cam = Camera.main;
        if (cam == null || layers == null) return;

        var tex = RuntimeGeneratedAssets.Texture(2, 2);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        sprite = RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), 16f);

        int total = 0;
        foreach (var l in layers) total += l.count;
        motes = new Mote[total];
        int n = 0;
        foreach (var l in layers)
            for (int i = 0; i < l.count; i++) motes[n++] = Make(l.kind);
    }

    Mote Make(Kind kind)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        var m = new Mote { t = go.transform, sr = sr, kind = kind };
        Randomize(m, true);
        return m;
    }

    // Gives a mote its look and start (anywhere in view at first, at an edge when recycled).
    void Randomize(Mote m, bool anywhere)
    {
        Vector2 c = cam.transform.position;
        float h = cam.orthographicSize, w = h * cam.aspect;
        m.phase = Random.value * 6.28f;
        m.speed = Random.Range(0.6f, 1.4f);
        switch (m.kind)
        {
            case Kind.Pollen:
                m.vel = new Vector2(Random.Range(0.1f, 0.35f), Random.Range(0.08f, 0.25f));
                m.wobble = Random.Range(0.15f, 0.4f); m.size = Random.Range(0.5f, 0.9f);
                m.color = new Color(1f, 0.96f, 0.7f); break;
            case Kind.Leaves:
                m.vel = new Vector2(Random.Range(0.5f, 1.1f), Random.Range(-1.1f, -0.55f));
                m.wobble = Random.Range(0.3f, 0.7f); m.size = Random.Range(0.8f, 1.2f); m.spin = 0f;
                m.color = Random.value < 0.5f ? new Color(0.96f, 0.6f, 0.2f) : Random.value < 0.5f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.85f, 0.28f, 0.22f); break;
            case Kind.Dust:
                m.vel = new Vector2(Random.Range(-0.05f, 0.18f), Random.Range(-0.05f, 0.1f));
                m.wobble = Random.Range(0.1f, 0.3f); m.size = Random.Range(0.5f, 0.85f);
                m.color = new Color(0.86f, 0.8f, 1f); break;
            case Kind.Spores:
                m.vel = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0.25f, 0.6f));
                m.wobble = Random.Range(0.2f, 0.45f); m.size = Random.Range(0.6f, 1f);
                m.color = new Color(1f, 0.26f, 0.34f); break;
            default: // Embers
                m.vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.8f));
                m.wobble = Random.Range(0.1f, 0.35f); m.size = Random.Range(0.5f, 0.9f);
                m.color = new Color(1f, 0.62f, 0.22f); break;
        }
        if (anywhere) m.pos = c + new Vector2(Random.Range(-w - 1f, w + 1f), Random.Range(-h - 1f, h + 1f));
        else if (Mathf.Abs(m.vel.y) > Mathf.Abs(m.vel.x))
            m.pos = c + new Vector2(Random.Range(-w - 1f, w + 1f), (m.vel.y > 0f ? -h - 1f : h + 1f));
        else
            m.pos = c + new Vector2(m.vel.x > 0f ? -w - 1f : w + 1f, Random.Range(-h - 1f, h + 1f));
        // Leaves are small wide flecks (3:2); everything else is a square pixel.
        m.t.localScale = m.kind == Kind.Leaves ? new Vector3(m.size * 1.5f, m.size, 1f) : Vector3.one * m.size;
    }

    void Update()
    {
        if (motes == null || cam == null) return;
        float dt = Time.deltaTime, time = Time.time;
        Vector2 c = cam.transform.position;
        float h = cam.orthographicSize + 1.5f, w = cam.orthographicSize * cam.aspect + 1.5f;

        foreach (var m in motes)
        {
            m.pos += m.vel * dt;
            if (Mathf.Abs(m.pos.x - c.x) > w || Mathf.Abs(m.pos.y - c.y) > h) { Randomize(m, false); }

            float sway = Mathf.Sin(time * m.speed + m.phase) * m.wobble;
            Vector2 p = m.pos + new Vector2(sway, m.kind == Kind.Leaves ? Mathf.Cos(time * m.speed * 1.3f + m.phase) * 0.15f : 0f);
            // Snap to the 1/16-unit pixel grid so they move in whole art-pixels.
            m.t.position = new Vector3(Mathf.Round(p.x * 16f) / 16f, Mathf.Round(p.y * 16f) / 16f, 0f);

            float tw = 0.5f + 0.5f * Mathf.Sin(time * m.speed * 2.2f + m.phase);
            var col = m.color;
            switch (m.kind)
            {
                case Kind.Pollen: col.a = Mathf.Lerp(0.3f, 0.85f, tw); break;
                case Kind.Leaves: col.a = 0.95f; break;
                case Kind.Dust: col.a = Mathf.Lerp(0.15f, 0.5f, tw); break;
                case Kind.Spores: col.a = Mathf.Lerp(0.35f, 0.9f, tw); break;
                default:
                    // Embers cool from orange to dark red and fade as they climb through the view.
                    float up = Mathf.Clamp01((m.pos.y - (c.y - h)) / (2f * h));
                    col = Color.Lerp(m.color, new Color(0.7f, 0.1f, 0.12f), up);
                    col.a = Mathf.Lerp(0.95f, 0.1f, up) * Mathf.Lerp(0.7f, 1f, tw);
                    break;
            }
            m.sr.color = col;
        }
    }
}
