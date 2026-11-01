using UnityEngine;

// "Hit stop": the game nearly freezes for a few hundredths of a second when a hit lands,
// which is most of what makes a hit feel heavy. Created on demand; overlapping calls keep
// the longest remaining stop. Never touches a real pause (GameClock owns those).
// Rapid hits (a spinning skill ticking every 0.1 s) get shorter and shorter stops instead of
// stuttering the whole game; GameSettings.HitStopEnabled turns it off.
public class HitStop : MonoBehaviour
{
    const float SlowScale = 0.02f;
    const float StreakWindow = 0.3f;   // a stop this soon after the last one counts as a streak
    const float StreakFalloff = 0.6f;  // each streak step shortens the stop: 1, 0.62, 0.45, 0.36...
    const float MinStop = 0.015f;      // anything shorter than this is skipped

    static HitStop instance;
    static float lastStopAt = -10f;
    static int streak;
    float remaining;

    // The speed the game should run at right now if nothing is paused (GameClock resumes to this).
    public static float Scale => instance == null ? 1f : instance.remaining > 0f ? SlowScale : instance.slowRemaining > 0f ? instance.slowScale : 1f;

    public static void Do(float seconds)
    {
        if (!GameSettings.HitStopEnabled || GameClock.Paused) return; // never during a pause

        float now = Time.unscaledTime;
        streak = now - lastStopAt < StreakWindow ? streak + 1 : 0;
        lastStopAt = now;
        seconds /= 1f + StreakFalloff * streak;
        if (seconds < MinStop) return;

        if (instance == null) instance = new GameObject("HitStop").AddComponent<HitStop>();
        instance.remaining = Mathf.Max(instance.remaining, seconds);
        instance.Apply();
    }

    // Slow motion (the boss's last hit): the game runs at `scale` for `seconds` of real time. A hit stop
    // during it still freezes harder; when both run out the game goes back to full speed.
    public static void Slow(float seconds, float scale)
    {
        if (!GameSettings.HitStopEnabled || GameClock.Paused) return;
        if (instance == null) instance = new GameObject("HitStop").AddComponent<HitStop>();
        instance.slowRemaining = Mathf.Max(instance.slowRemaining, seconds);
        instance.slowScale = scale;
        instance.Apply();
    }

    // Drops any stop / slow motion still running (GameClock.ResetAll on a scene change).
    public static void Clear()
    {
        if (instance == null) return;
        instance.remaining = 0f;
        instance.slowRemaining = 0f;
    }

    float slowRemaining, slowScale = 1f;

    // Drives the clock only while nothing holds a pause.
    void Apply()
    {
        if (!GameClock.Paused) Time.timeScale = Scale;
    }

    void Update()
    {
        if (remaining <= 0f && slowRemaining <= 0f) return;
        remaining -= Time.unscaledDeltaTime;
        slowRemaining -= Time.unscaledDeltaTime;
        Apply();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (!GameClock.Paused) Time.timeScale = 1f;
    }
}
