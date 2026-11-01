using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// LoadingScene: the cat picked in the lobby rolls (Dodge animation) in the middle of the
// screen above "Loading ..." while SceneLoader.TargetScene loads in the background. Stays
// up for at least minDisplayTime so the roll is actually visible on fast loads.
public class LoadingScreen : MonoBehaviour
{
    [Tooltip("Same order as the lobby: 0 = Meow Knight, 1 = Meowolas.")]
    public CharacterDefinition[] characters;
    public SpriteRenderer visual;
    public TMP_Text loadingText;
    public float minDisplayTime = 1.5f;
    public float dotInterval = 0.35f;
    public string fallbackScene = "TitleScene";

    CharacterDefinition def;
    Sprite[] frames;
    float fps;
    int frame;
    float frameTimer;
    float dotTimer;
    int dots = 1;

    void Start()
    {
        int index = Mathf.Clamp(CharacterSelection.SelectedIndex, 0, characters.Length - 1);
        def = characters[index];
        frames = def.dodgeFrames != null && def.dodgeFrames.Length > 0 ? def.dodgeFrames : def.idleFrames;
        fps = def.dodgeFramesPerSecond;
        ShowFrame(frames[0]);
        UpdateText();
        StartCoroutine(LoadTarget());
    }

    IEnumerator LoadTarget()
    {
        string target = string.IsNullOrEmpty(SceneLoader.TargetScene) ? fallbackScene : SceneLoader.TargetScene;
        float started = Time.unscaledTime;
        var op = SceneManager.LoadSceneAsync(target);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f || Time.unscaledTime - started < minDisplayTime)
            yield return null;
        // Switch behind a black screen: activating the scene freezes a frame or two (garbage collection).
        ScreenFader.FadeOut(() => op.allowSceneActivation = true);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        frameTimer += dt;
        if (frameTimer >= 1f / fps)
        {
            frameTimer -= 1f / fps;
            frame = (frame + 1) % frames.Length;
            ShowFrame(frames[frame]);
        }

        dotTimer += dt;
        if (dotTimer >= dotInterval)
        {
            dotTimer -= dotInterval;
            dots = dots % 3 + 1;
            UpdateText();
        }
    }

    void UpdateText()
    {
        if (loadingText != null) loadingText.text = "Loading " + new string('.', dots);
    }

    // Feet on this object's position. Body-aligned like everywhere else (re-centring each
    // frame by its own bounds made the roll wobble, since the frames share one pivot now);
    // bounds are only the fallback for characters without alignment data.
    void ShowFrame(Sprite f)
    {
        visual.sprite = f;
        float s = visual.transform.localScale.x;
        if (def != null && def.TryGetVisualOffset(f, visual.flipX, s, out Vector2 aligned))
        {
            visual.transform.localPosition = new Vector3(aligned.x, aligned.y + def.alignReferenceSize.y * 0.5f * s, 0f);
            return;
        }
        Bounds b = f.bounds;
        visual.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, 0f);
    }
}
