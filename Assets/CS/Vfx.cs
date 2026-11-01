using System.Collections.Generic;
using UnityEngine;

// One-shot sprite effects: hit flashes, dust puffs, slash arcs, death poofs, heal glow...
//   Vfx.Play("dust_mid", feetPosition);   Vfx.Play("slash", pos, 1.2f, flipX: true, rotationZ: 20f);
// Sheets: DryRain's Pixel Platformer VFX pack (32px, the same pixel size as the tiles) for the small
// stuff and hand-drawn 64px pixel-art sheets for the skills - see Assets/Effects/README_Sources.txt.
// An effect plays once, then destroys itself. It follows game time, so a hit-stop briefly holds it.
// Vfx.Loop keeps cycling an effect (poison bubbles, a beam) for a set time; Vfx.Frames hands out the
// frames themselves, for skill projectiles and HUD icons.
public static class Vfx
{
    const int MaxActive = 96; // a long fight must never pile up effects (skills add a lot of them)
    static VfxLibrary library;
    static readonly Dictionary<string, VfxLibrary.Entry> byId = new Dictionary<string, VfxLibrary.Entry>();
    static int active;
#if UNITY_EDITOR
    static readonly HashSet<string> warnedSnap = new HashSet<string>();
#endif
    // Live one-shot effects, oldest first. When the cap is hit we recycle the oldest instead of dropping
    // the new one on the floor (a boss AoE or a fast skill would silently show no hit otherwise). Looping
    // effects (beams, poison) are never evicted here - they own their own lifetime and would pop out mid-cast.
    static readonly LinkedList<VfxPlayer> oneShots = new LinkedList<VfxPlayer>();

    public static GameObject Play(string id, Vector3 position, float scale = 1f, bool flipX = false,
                                  float rotationZ = 0f, Color? tint = null, Transform parent = null)
    {
        return Spawn(id, position, scale, flipX, rotationZ, tint, parent, 0f);
    }

    // Plays the effect over and over for `seconds` (then it is removed). Destroy the returned object to stop early.
    public static GameObject Loop(string id, Vector3 position, float seconds, float scale = 1f, bool flipX = false,
                                  float rotationZ = 0f, Color? tint = null, Transform parent = null)
    {
        return Spawn(id, position, scale, flipX, rotationZ, tint, parent, Mathf.Max(0.01f, seconds));
    }

    // The effect's frames (null if the library doesn't have it - run Cats Smash > Apply Effects).
    public static Sprite[] Frames(string id)
    {
        return Load() && byId.TryGetValue(id, out var e) && e.frames != null && e.frames.Length > 0 ? e.frames : null;
    }

    public static Sprite Sprite(string id, int frame = 0)
    {
        var f = Frames(id);
        return f == null ? null : f[Mathf.Clamp(frame, 0, f.Length - 1)];
    }

    // World size multiplier the library uses for this effect (so a projectile matches its Vfx.Play size).
    public static float Scale(string id) => Load() && byId.TryGetValue(id, out var e) ? e.scale : 1f;

    static GameObject Spawn(string id, Vector3 position, float scale, bool flipX, float rotationZ, Color? tint,
                            Transform parent, float loopSeconds)
    {
        if (!Load() || !byId.TryGetValue(id, out var e) || e.frames == null || e.frames.Length == 0) return null;
        // At the cap: recycle the oldest one-shot so the new effect still plays (a dropped hit reads as a bug).
        if (active >= MaxActive)
        {
            var node = oneShots.First;
            while (node != null && node.Value == null) { var next = node.Next; oneShots.Remove(node); node = next; } // skip already-destroyed
            if (node != null) node.Value.Retire(); // frees its slot now (Destroy itself only lands at frame end)
            else if (active >= MaxActive) return null; // only loops left (they manage themselves): don't touch them
        }
        if (e.pixelSnap)
        {
            // Pixel-art sheets keep whole pixels: only x0.5 / x1 / x2, and quarter turns unless it
            // points along a flight (a free angle there reads better than a snapped one).
            float snapped = scale < 0.75f ? 0.5f : scale < 1.5f ? 1f : 2f;
#if UNITY_EDITOR
            // A scale that isn't one of the steps lies about the size drawn (1.4 draws at x1, 1.6 at x2): say so once.
            if (!Mathf.Approximately(snapped, scale) && warnedSnap.Add(id))
                Debug.LogWarning($"[Vfx] '{id}' is pixel-snapped: scale {scale} draws at x{snapped}. Pass 0.5, 1 or 2.");
#endif
            scale = snapped;
            if (!e.freeRotation) rotationZ = Mathf.Round(rotationZ / 90f) * 90f;
        }

        var go = new GameObject("Vfx_" + id);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        go.transform.localScale = Vector3.one * e.scale * scale;
        if (parent != null) go.transform.SetParent(parent, true);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = e.frames[0];
        sr.flipX = flipX;
        sr.sortingOrder = e.sortingOrder;
        var col = tint ?? Color.white;
        if (e.pixelSnap && loopSeconds <= 0f) col.a = 1f; // pixel art stays solid: a tint only colours it, it never see-throughs it
        sr.color = col;

        var player = go.AddComponent<VfxPlayer>();
        player.frames = e.frames;
        player.fps = e.fps;
        player.sr = sr;
        player.loopSeconds = loopSeconds;
        active++;
        player.onDone = () => active--;
        if (loopSeconds <= 0f) player.node = oneShots.AddLast(player); // only one-shots are eviction candidates
        return go;
    }

    static bool Load()
    {
        if (library != null) return true;
        library = Resources.Load<VfxLibrary>("VfxLibrary");
        if (library == null) return false;
        byId.Clear();
        foreach (var e in library.entries) if (e != null && !string.IsNullOrEmpty(e.id)) byId[e.id] = e;
        return true;
    }

    // Called by a one-shot VfxPlayer's OnDestroy so it leaves the eviction list (node is null for loops).
    public static void Unlink(LinkedListNode<VfxPlayer> node)
    {
        if (node != null && node.List == oneShots) oneShots.Remove(node);
    }
}

public class VfxPlayer : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 16f;
    public SpriteRenderer sr;
    public System.Action onDone;
    [Tooltip("> 0: keep looping for this many seconds, fading out over the last 0.25 s.")]
    public float loopSeconds;
    [System.NonSerialized] public LinkedListNode<VfxPlayer> node; // its slot in Vfx.oneShots (one-shots only), for eviction
    float t;
    int shown = -1;
    bool done; // OnDestroy can run more than once in edge cases; count the effect down exactly once

    void Update()
    {
        t += Time.deltaTime;
        int i = Mathf.FloorToInt(t * fps);
        if (loopSeconds > 0f)
        {
            if (t >= loopSeconds) { Destroy(gameObject); return; }
            i %= frames.Length;
            float left = loopSeconds - t;
            if (left < 0.25f) { var c = sr.color; c.a = Mathf.Min(c.a, left / 0.25f); sr.color = c; }
        }
        else if (i >= frames.Length) { Destroy(gameObject); return; }
        if (i != shown) { shown = i; sr.sprite = frames[i]; }
    }

    // Evicted at the cap: count down and leave the list right away, then go.
    public void Retire()
    {
        Release();
        Destroy(gameObject);
    }

    void OnDestroy() => Release();

    void Release()
    {
        if (done) return;
        done = true;
        Vfx.Unlink(node);
        node = null;
        onDone?.Invoke();
    }
}
