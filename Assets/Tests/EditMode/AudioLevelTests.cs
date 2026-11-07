using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// The code-made sounds (SynthSfx) sit at the level of the recorded ones: each recorded effect's loudness as played
// (clip RMS x its library volume) is measured, and every synth sound's must be within 3 dB of their mean, with no
// sample over -1 dBFS. The numbers are logged, so a change to a recording shows how far the mix moved.
public class AudioLevelTests
{
    static float Db(float v) => 20f * Mathf.Log10(Mathf.Max(v, 1e-6f));

    [Test]
    public void SynthSounds_SitWithin3dB_OfTheRecordedEffects()
    {
        var library = Resources.Load<SfxLibrary>("SfxLibrary");
        Assert.That(library, Is.Not.Null);
        var played = new List<float>();
        var clipRms = new List<float>();
        foreach (var e in library.entries)
        {
            if (e == null || e.clips == null) continue;
            foreach (var c in e.clips)
            {
                if (c == null || !c.LoadAudioData()) continue;
                var data = new float[c.samples * c.channels];
                if (!c.GetData(data, 0)) continue;
                float rms = SynthSfx.Rms(data);
                if (rms < 1e-4f) continue;
                clipRms.Add(rms);
                played.Add(rms * e.volume);
            }
        }
        Assert.That(played.Count, Is.GreaterThan(10), "too few recorded clips could be read");
        float meanPlayedDb = 0f, meanClipDb = 0f;
        foreach (var v in played) meanPlayedDb += Db(v);
        foreach (var v in clipRms) meanClipDb += Db(v);
        meanPlayedDb /= played.Count;
        meanClipDb /= clipRms.Count;
        Debug.Log($"[AudioLevels] recorded: {played.Count} clips, clip RMS mean {meanClipDb:0.0} dBFS, as played (x volume) {meanPlayedDb:0.0} dBFS");

        var off = new List<string>();
        foreach (var id in SynthSfx.Ids)
        {
            var (clip, volume, _) = SynthSfx.Build(id);
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float rms = SynthSfx.Rms(data), peak = SynthSfx.Peak(data), playedDb = Db(rms * volume);
            Debug.Log($"[AudioLevels] {id}: RMS {Db(rms):0.0} dBFS, peak {Db(peak):0.0} dBFS, volume {volume}, as played {playedDb:0.0} dBFS ({playedDb - meanPlayedDb:+0.0;-0.0} dB from the mean)");
            // the shape over time: RMS of ten equal slices, as a bar per slice (0-9), e.g. a hit is "96421...."
            var shape = new System.Text.StringBuilder();
            int slice = Mathf.Max(1, data.Length / 10);
            for (int s = 0; s < 10; s++)
            {
                var part = new float[Mathf.Min(slice, data.Length - s * slice)];
                if (part.Length <= 0) { shape.Append('.'); continue; }
                System.Array.Copy(data, s * slice, part, 0, part.Length);
                int level = Mathf.Clamp(Mathf.RoundToInt(SynthSfx.Rms(part) / SynthSfx.PeakLimit * 30f), 0, 9);
                shape.Append(level == 0 ? '.' : (char)('0' + level));
            }
            Debug.Log($"[AudioLevels] {id}: {data.Length / (float)clip.frequency:0.00} s, shape {shape}");
            if (Mathf.Abs(playedDb - meanPlayedDb) > 3f) off.Add($"{id}: {playedDb - meanPlayedDb:+0.0;-0.0} dB");
            if (peak > SynthSfx.PeakLimit + 1e-4f) off.Add($"{id}: peak {Db(peak):0.0} dBFS over -1");
            Object.DestroyImmediate(clip);
        }
        Assert.That(off, Is.Empty, string.Join("\n", off));
    }
}
