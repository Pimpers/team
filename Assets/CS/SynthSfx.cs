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

    public static readonly string[] Ids = { "voice_blip", "preview_tick", "tip_chime" };

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
