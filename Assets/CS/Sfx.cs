using System.Collections.Generic;
using UnityEngine;

// One-shot sound effects. Clips live in Assets/Audio/Sfx (Kenney CC0 packs) and are listed
// by id in Assets/Resources/SfxLibrary.asset, which the editor tool AudioAssets builds from
// the file names (swing_0.ogg, swing_1.ogg -> id "swing", one picked at random per play).
//   Sfx.Play("hit");                    Sfx.Play("slam", 0.8f, 0.7f);  // volume, pitch
// Plays on a small pool of 2D AudioSources that survives scene loads; volume follows the
// settings slider through the AudioListener like the music.
public static class Sfx
{
    const int PoolSize = 10;
    const float RepeatGap = 0.035f; // the same sound twice within this is skipped (multi-hits)

    static SfxLibrary library;
    static AudioSource[] pool;
    static int next;
    static readonly Dictionary<string, SfxLibrary.Entry> byId = new Dictionary<string, SfxLibrary.Entry>();
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    public static void Play(string id, float volume = 1f, float pitch = 1f)
    {
        if (!Init() || !byId.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float t) && now - t < RepeatGap) return;
        lastPlayed[id] = now;

        var clip = e.clips[Random.Range(0, e.clips.Length)];
        if (clip == null) return;
        var src = pool[next];
        next = (next + 1) % pool.Length;
        src.pitch = pitch * e.pitch * Random.Range(1f - e.pitchJitter, 1f + e.pitchJitter);
        src.PlayOneShot(clip, e.volume * volume * GameSettings.SfxVolume);
    }

    static bool Init()
    {
        if (pool != null && pool[0] != null) return library != null;
        if (library == null)
        {
            library = Resources.Load<SfxLibrary>("SfxLibrary");
            byId.Clear();
            if (library != null)
                foreach (var e in library.entries) if (e != null && !string.IsNullOrEmpty(e.id)) byId[e.id] = e;
            SynthSfx.Register(byId); // the few sounds made in code (voice blip, preview tick, tip chime)
        }
        var go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
            pool[i] = src;
        }
        return library != null;
    }
}
