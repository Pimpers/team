using UnityEngine;

// Sounds made in code (no audio file): small, soft tones for the places where a borrowed effect sounded wrong -
// the blacksmith's voice was a card flip played high, the draft preview a UI click. Built once on first use with
// AudioClip.Create and registered with Sfx under their own ids, so they play, pool and follow the volume
// slider like every other effect:
//   "voice_blip" - a short rounded chirp that rises a little (a cat's chatter), one per few letters of dialogue
//   "preview_tick" - a very short, quiet tick for a card's preview line appearing
//   "tip_chime" - two soft rising notes for a one-time tip
// Levels: each clip is normalised to TargetRms (the mean loudness of the game's recorded effects, measured by
// AudioLevelTests) with its peak held under -1 dBFS; the per-sound volume then sits it in the mix like the
// recorded ones' library volumes do.
public static class SynthSfx
{
    const int Rate = 22050;
    // Mean RMS of the recorded effects (44 SfxLibrary clips), measured in AudioLevelTests: -19.6 dBFS; as played
    // (x their library volume) -25.1 dBFS, which the volumes below aim the synth sounds at (within 3 dB).
    public const float TargetRms = 0.106f;
    public const float PeakLimit = 0.891f; // -1 dBFS

    // Skill sounds for the Aether effects (they borrowed a UI click, a card flip, a slam or the boss's roar):
    //   "fx_bell"  - three quick jingles of an inharmonic bell (the bell wire)
    //   "fx_trap"  - a snap: a noise click on a short low thump (the mousetrap)
    //   "fx_can"   - a struck tin: inharmonic partials ringing out fast (the can opener's hit)
    //   "fx_hiss"  - rough breath: noise through a slow wobble, rising then dying (the hiss)
    //   "fx_churu" - a soft squish and a small rising pop (the churu landing)
    //   "fx_groom" - three soft brushing strokes (grooming)
    public static readonly string[] Ids = { "voice_blip", "preview_tick", "tip_chime", "fx_bell", "fx_trap", "fx_can", "fx_hiss", "fx_churu", "fx_groom" };

    public static void Register(System.Collections.Generic.Dictionary<string, SfxLibrary.Entry> byId)
    {
        foreach (var id in Ids)
        {
            if (byId.ContainsKey(id)) continue; // a real file under the same id wins
            var (clip, volume, jitter) = Build(id);
            byId[id] = new SfxLibrary.Entry { id = id, clips = new[] { clip }, volume = volume, pitchJitter = jitter, pitch = 1f };
        }
    }

    // The clip, its library volume and pitch jitter. (Public for AudioLevelTests.)
    public static (AudioClip clip, float volume, float jitter) Build(string id)
    {
        switch (id)
        {
            case "voice_blip": return (Make(id, Chirp(0.07f, 520f, 700f)), 0.5f, 0.08f);   // many per line: at the mean, not over it
            case "preview_tick": return (Make(id, Chirp(0.025f, 1400f, 1300f)), 0.45f, 0.03f); // a tick: a little under the mean
            case "fx_bell": return (Make(id, Bell()), 0.55f, 0.05f);
            case "fx_trap": return (Make(id, Trap()), 0.6f, 0.06f);
            case "fx_can": return (Make(id, Metal(0.35f, 820f)), 0.55f, 0.05f);
            case "fx_hiss": return (Make(id, Hiss()), 0.55f, 0.04f);
            case "fx_churu": return (Make(id, Churu()), 0.5f, 0.06f);
            case "fx_groom": return (Make(id, Groom()), 0.45f, 0.08f);
            default: return (Make(id, Notes(new[] { 660f, 880f }, 0.11f)), 0.6f, 0f);    // news: a little over
        }
    }

    // A tone gliding from `from` to `to` Hz: a sine with a little of its octave (rounder than a pure sine), a fast
    // attack and an exponential fall so it never clicks.
    static float[] Chirp(float seconds, float from, float to)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            phase += 2 * System.Math.PI * Mathf.Lerp(from, to, k) / Rate;
            float env = Mathf.Min(1f, i / (Rate * 0.004f)) * Mathf.Exp(-4f * k);
            data[i] = env * (0.8f * (float)System.Math.Sin(phase) + 0.2f * (float)System.Math.Sin(phase * 2));
        }
        return data;
    }

    static float[] Notes(float[] freqs, float each)
    {
        int per = Mathf.CeilToInt(each * Rate);
        var data = new float[per * freqs.Length + per]; // the last note rings on
        for (int f = 0; f < freqs.Length; f++)
        {
            int len = f == freqs.Length - 1 ? per * 2 : per;
            for (int i = 0; i < len; i++)
            {
                float k = i / (float)len;
                float env = Mathf.Min(1f, i / (Rate * 0.005f)) * Mathf.Exp(-3.5f * k);
                data[f * per + i] += env * 0.7f * Mathf.Sin(2f * Mathf.PI * freqs[f] * i / Rate);
            }
        }
        return data;
    }

    // ---- Skill sounds. Noise from a fixed seed, so every build of a clip is the same.
    static System.Random rng;
    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

    // A struck metal body: partials at the inharmonic ratios of a small plate (1, 2.76, 5.40, 8.93), the higher
    // ones dying faster - the "tin" in a can, the "ting" in a bell.
    static void Strike(float[] data, int start, float seconds, float baseHz, float level)
    {
        float[] ratio = { 1f, 2.76f, 5.4f, 8.93f }, amp = { 1f, 0.6f, 0.35f, 0.2f }, decay = { 6f, 9f, 14f, 20f };
        int n = Mathf.Min(data.Length - start, Mathf.CeilToInt(seconds * Rate));
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, s = 0f;
            for (int p = 0; p < ratio.Length; p++) s += amp[p] * Mathf.Exp(-decay[p] * t) * Mathf.Sin(2f * Mathf.PI * baseHz * ratio[p] * t);
            data[start + i] += level * Mathf.Min(1f, i / (Rate * 0.001f)) * s;
        }
    }

    static float[] Metal(float seconds, float baseHz)
    {
        var data = new float[Mathf.CeilToInt(seconds * Rate)];
        Strike(data, 0, seconds, baseHz, 1f);
        return data;
    }

    static float[] Bell()
    {
        var data = new float[Mathf.CeilToInt(0.42f * Rate)];
        float[] hz = { 1850f, 2100f, 1950f };
        for (int k = 0; k < 3; k++) Strike(data, Mathf.RoundToInt(k * 0.07f * Rate), 0.3f, hz[k], 0.7f);
        return data;
    }

    static float[] Trap()
    {
        rng = new System.Random(7);
        var data = new float[Mathf.CeilToInt(0.18f * Rate)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float click = t < 0.012f ? Noise() * (1f - t / 0.012f) : 0f;               // the spring letting go
            float thump = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-28f * t);   // the bar hitting the board
            data[i] = 0.9f * click + 0.8f * thump;
        }
        Strike(data, Mathf.RoundToInt(0.004f * Rate), 0.12f, 2600f, 0.25f);           // a little steel ring
        return data;
    }

    static float[] Hiss()
    {
        rng = new System.Random(11);
        var data = new float[Mathf.CeilToInt(0.55f * Rate)];
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float k = i / (float)data.Length, t = i / (float)Rate;
            float n = Noise();
            lp += 0.35f * (n - lp);                                                    // a soft low-pass: breath, not static
            float rough = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 31f * t);           // the throat's rasp
            float env = Mathf.Min(1f, k / 0.15f) * Mathf.Pow(1f - k, 1.5f);
            data[i] = env * rough * (0.6f * (n - lp) + 0.4f * lp);                      // mostly the hissy highs
        }
        return data;
    }

    static float[] Churu()
    {
        rng = new System.Random(3);
        var data = new float[Mathf.CeilToInt(0.22f * Rate)];
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            lp += 0.08f * (Noise() - lp);                                              // a wet, dull squish
            data[i] = 1.6f * lp * Mathf.Exp(-14f * t);
        }
        var pop = Chirp(0.08f, 380f, 760f);                                            // and a small rising pop
        int at = Mathf.RoundToInt(0.06f * Rate);
        for (int i = 0; i < pop.Length && at + i < data.Length; i++) data[at + i] += 0.6f * pop[i];
        return data;
    }

    static float[] Groom()
    {
        rng = new System.Random(5);
        var data = new float[Mathf.CeilToInt(0.6f * Rate)];
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float stroke = Mathf.Repeat(t, 0.2f) / 0.2f;                                // three strokes
            float env = Mathf.Sin(Mathf.PI * stroke) * Mathf.Sin(Mathf.PI * stroke);
            float n = Noise();
            lp += 0.5f * (n - lp);
            data[i] = env * (n - lp) * 0.8f;                                           // a fine bristly brush
        }
        return data;
    }

    // Scales `data` to TargetRms, then down further if that would put a peak over PeakLimit.
    public static void Normalize(float[] data)
    {
        float rms = Rms(data), peak = Peak(data);
        if (rms <= 0f) return;
        float gain = TargetRms / rms;
        if (peak * gain > PeakLimit) gain = PeakLimit / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
    }

    public static float Rms(float[] data)
    {
        double sum = 0;
        foreach (var s in data) sum += s * s;
        return data.Length == 0 ? 0f : (float)System.Math.Sqrt(sum / data.Length);
    }

    public static float Peak(float[] data)
    {
        float p = 0f;
        foreach (var s in data) p = Mathf.Max(p, Mathf.Abs(s));
        return p;
    }

    static AudioClip Make(string name, float[] data)
    {
        Normalize(data);
        var clip = AudioClip.Create("synth_" + name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
