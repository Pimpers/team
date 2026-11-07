using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The fight camera's Pixel Perfect Camera (StagePixelPerfect): on a stage, at 1920 x 1080, it zooms x5 (one cat
// pixel = 10 screen px), it costs next to nothing per frame, and the camera still shakes - unless the player
// turned shake off in the settings.
public class PixelPerfectPlayModeTests
{
    [TearDown]
    public void Reset() { GameSettings.ShakeStrength = 1f; GameClock.ResetAll(); }

    [UnityTest]
    public IEnumerator Stage_CameraIsPixelPerfect_CheapAndShakeStillObeysTheSetting()
    {
        SceneManager.LoadScene("Stage2");
        yield return new WaitForSecondsRealtime(1.5f);
        var cam = Camera.main;
        var ppc = cam.GetComponent<PixelPerfectCamera>();
        Assert.That(ppc, Is.Not.Null, "no Pixel Perfect Camera on the stage camera");
        Assert.That(ppc.assetsPPU, Is.EqualTo(StagePixelPerfect.AssetsPPU));

        // At 1920 x 1080 the reference is 384 x 216 -> x5.
        var rt = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = rt;
        yield return null; yield return null;
        Assert.That(ppc.pixelRatio, Is.EqualTo(5), "1920 x 1080 should be a whole x5");

        // Frame cost: 60 renders with the component on, then off.
        double on = Time60(cam), off;
        ppc.enabled = false;
        off = Time60(cam);
        ppc.enabled = true;
        UnityEngine.Debug.Log($"[PixelPerfect] render 1920x1080: on {on:F2} ms, off {off:F2} ms per frame");
        Assert.That(on, Is.LessThan(off * 1.5 + 1.0), "the Pixel Perfect Camera made a frame much dearer");
        cam.targetTexture = null;
        Object.Destroy(rt);

        // Shake still moves the camera; with shake off in the settings it does not.
        GameSettings.ShakeStrength = 1f;
        var still = cam.transform.position;
        CameraFollow.Shake(0.3f, 0.5f);
        float moved = 0f;
        for (int i = 0; i < 10; i++) { yield return null; moved = Mathf.Max(moved, Vector2.Distance(cam.transform.position, still)); }
        Assert.That(moved, Is.GreaterThan(0.01f), "the camera did not shake");
        yield return new WaitForSecondsRealtime(0.8f);
        GameSettings.ShakeStrength = 0f;
        still = cam.transform.position;
        CameraFollow.Shake(0.3f, 0.5f);
        moved = 0f;
        for (int i = 0; i < 10; i++) { yield return null; moved = Mathf.Max(moved, Vector2.Distance(cam.transform.position, still)); }
        Assert.That(moved, Is.LessThan(0.05f), "shake off in the settings, yet the camera shook");
    }

    static double Time60(Camera cam)
    {
        cam.Render(); // warm-up
        var w = Stopwatch.StartNew();
        for (int i = 0; i < 60; i++) cam.Render();
        w.Stop();
        return w.Elapsed.TotalMilliseconds / 60.0;
    }
}
