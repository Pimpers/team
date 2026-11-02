using UnityEngine;

// Sounds made in code (no audio file): small, soft tones for the places where a borrowed effect sounded wrong -
// the blacksmith's voice was a card flip played high, the draft preview a UI click. Built once on first use with
// AudioClip.Create and registered with Sfx under their own ids, so they play, pool and follow the volume
// slider like every other effect:
//   "voice_blip" - a short rounded chirp that rises a little (a cat's chatter), one per few letters of dialogue
//   "preview_tick" - a very short, quiet tick for a card's preview line appearing
//   "tip_chime" - two soft rising notes for a one-time tip
public static class SynthSfx
{
    const int Rate = 22050;

    public static void Register(System.Collections.Generic.Dictionary<string, SfxLibrary.Entry> byId)
    {
        Add(byId, "voice_blip", Chirp(0.07f, 520f, 700f), 0.35f, 0.08f);
        Add(byId, "preview_tick", Chirp(0.025f, 1400f, 1300f), 0.25f, 0.03f);
        Add(byId, "tip_chime", Notes(new[] { 660f, 880f }, 0.11f), 0.35f, 0f);
    }

    static void Add(System.Collections.Generic.Dictionary<string, SfxLibrary.Entry> byId, string id, AudioClip clip, float volume, float jitter)
    {
        if (byId.ContainsKey(id)) return; // a real file under the same id wins
        byId[id] = new SfxLibrary.Entry { id = id, clips = new[] { clip }, volume = volume, pitchJitter = jitter, pitch = 1f };
    }

    // A tone gliding from `from` to `to` Hz: a sine with a little of its octave (rounder than a pure sine), a fast
    // attack and an exponential fall so it never clicks.
    static AudioClip Chirp(float seconds, float from, float to)
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
        return Make("synth_chirp", data);
    }

    static AudioClip Notes(float[] freqs, float each)
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
        return Make("synth_notes", data);
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
