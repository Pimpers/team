using UnityEngine;

// Attach to any SpriteRenderer to loop through a set of sprite frames (e.g. an Idle animation).
[RequireComponent(typeof(SpriteRenderer))]
public class LoopingSpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 8f;

    SpriteRenderer sr;
    int currentFrame;
    float timer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (frames == null || frames.Length == 0) return;
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
