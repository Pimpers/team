using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Attach to a full-screen RawImage placed as the FIRST child of the Canvas
// (so it renders behind the Play/Options buttons).
// GIFs aren't natively playable in Unity, so the frames were extracted to
// Assets/Resources/BackgroundGif/frame_0000.png ... frame_0479.png and are
// loaded and cycled here at runtime.
[RequireComponent(typeof(RawImage))]
public class UIGifBackground : MonoBehaviour
{
    [Tooltip("Folder name under any Assets/Resources containing the frame PNGs.")]
    public string resourceFolder = "BackgroundGif";

    [Tooltip("Matches the extracted gif's own frame rate (background.gif averaged ~10fps).")]
    public float framesPerSecond = 10f;

    RawImage rawImage;
    Texture2D[] frames;
    int currentFrame;
    float timer;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        frames = Resources.LoadAll<Texture2D>(resourceFolder)
            .OrderBy(t => t.name)
            .ToArray();

        if (frames.Length == 0)
        {
            Debug.LogWarning($"UIGifBackground: no textures found in Resources/{resourceFolder}");
            return;
        }
        rawImage.texture = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        timer += Time.deltaTime;
        if (timer >= 1f / framesPerSecond)
        {
            timer = 0f;
            currentFrame = (currentFrame + 1) % frames.Length;
            rawImage.texture = frames[currentFrame];
        }
    }
}
