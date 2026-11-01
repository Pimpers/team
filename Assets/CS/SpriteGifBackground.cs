using System.Linq;
using UnityEngine;

// World-space alternative to UIGifBackground: use this when the scene has world-space
// objects (e.g. a Player SpriteRenderer) that must render ON TOP of the background.
// A Screen Space - Overlay Canvas (RawImage) always draws above the entire camera output
// no matter the Hierarchy order, so it will always hide world-space sprites - this
// SpriteRenderer-based version instead uses Sorting Layer/Order like any other sprite,
// so it can correctly sit behind the Player.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteGifBackground : MonoBehaviour
{
    [Tooltip("Folder name under any Assets/Resources containing the frame PNGs, imported as Sprite (2D and UI).")]
    public string resourceFolder = "BackgroundGif";

    [Tooltip("Matches the extracted gif's own frame rate (background.gif averaged ~10fps).")]
    public float framesPerSecond = 10f;

    SpriteRenderer sr;
    Sprite[] frames;
    int currentFrame;
    float timer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        frames = Resources.LoadAll<Sprite>(resourceFolder)
            .OrderBy(s => s.name)
            .ToArray();

        if (frames.Length == 0)
        {
            Debug.LogWarning($"SpriteGifBackground: no sprites found in Resources/{resourceFolder}. Make sure the PNGs there are imported as Texture Type 'Sprite (2D and UI)'.");
            return;
        }
        sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        timer += Time.deltaTime;
        if (timer >= 1f / framesPerSecond)
        {
            timer = 0f;
            currentFrame = (currentFrame + 1) % frames.Length;
            sr.sprite = frames[currentFrame];
        }
    }
}
