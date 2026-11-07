using UnityEngine;
using UnityEngine.Rendering.Universal;

// The fight camera at a whole number of screen pixels per art pixel (URP's Pixel Perfect Camera, added at runtime by
// CameraFollow so the stage scenes stay untouched). The cats and the effects are 12.5 px per unit; the component
// takes a whole PPU, so it runs at 25 (half a cat pixel). The view is 216 of those pixels high (8.64 units; it was
// 8.8 at camera size 4.4) and as wide as the screen: zoom = screen height / 216 rounded, so 1920 x 1080, 1440 x
// 1080 (4:3), 2560 x 1080 (21:9) and 1680 x 1050 (16:10, 210 high) are all x5 - one cat pixel = 10 screen px; the
// view height moves a few per cent with the screen, never black bars. A fixed 384-wide reference zoomed 4:3
// out to x3 (the sky over the map showed).
// Command line for comparing captures: -ppcOff (no component), -ppcUpscale (render the reference and blow it up).
[RequireComponent(typeof(PixelPerfectCamera))]
public class StagePixelPerfect : MonoBehaviour
{
    public const int AssetsPPU = 25, RefHeight = 216;
    PixelPerfectCamera ppc;
    Camera cam;
    int lastW, lastH;

    public static void Apply(Camera cam)
    {
        if (cam == null || !cam.orthographic || cam.GetComponent<PixelPerfectCamera>() != null) return;
        var args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-ppcOff") >= 0) return;
        var ppc = cam.gameObject.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = AssetsPPU;
        ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
        // Off by default: rendering the 25 px grid and blowing it up drew the 16 px tiles and monsters with uneven pixels.
        ppc.gridSnapping = System.Array.IndexOf(args, "-ppcUpscale") >= 0
            ? PixelPerfectCamera.GridSnapping.UpscaleRenderTexture
            : PixelPerfectCamera.GridSnapping.None;
        var fit = cam.gameObject.AddComponent<StagePixelPerfect>();
        fit.ppc = ppc;
        fit.cam = cam;
        fit.Refit();
    }

    // The reference follows the screen (or a capture's render texture): 216 high, as wide as the screen at that zoom.
    void Refit()
    {
        int w = cam.pixelWidth, h = cam.pixelHeight;
        if (w == lastW && h == lastH) return;
        lastW = w; lastH = h;
        int zoom = Mathf.Max(1, Mathf.RoundToInt(h / (float)RefHeight)); // nearest zoom: 1050 high is x5 on 210, not x4
        ppc.refResolutionY = Mathf.Max(1, h / zoom);
        ppc.refResolutionX = Mathf.Max(1, w / zoom);
    }

    void OnPreCull() { if (ppc != null && cam != null) Refit(); }
    void Update() { if (ppc != null && cam != null) Refit(); }
}
