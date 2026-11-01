using UnityEngine;

// Attach to Main Camera. Skul-style platformer camera in Stage scenes (with a StageMap):
// - X sticks to the player almost rigidly, leading a little in the facing direction.
// - Y does NOT ride along with jumps: it settles on the height the player stands on and
//   only re-centres after landing somewhere higher/lower, or right away if the player
//   leaves the vertical dead zone (big fall, tall jump).
// The view is clamped so it never shows past the map's edges. Without a StageMap (the old
// GameScene demo) it follows X only at a fixed Y.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float fixedY = 0f;

    [Header("Horizontal")]
    [Tooltip("Lower = tighter. Small but non-zero so turning around isn't a hard snap.")]
    public float xSmoothTime = 0.05f;
    [Tooltip("How far ahead of the player the view looks in the facing direction.")]
    public float lookAhead = 1.2f;
    [Tooltip("How quickly the look-ahead swings to the other side after turning.")]
    public float lookAheadSmoothTime = 0.35f;

    [Header("Vertical")]
    [Tooltip("Camera sits this far above the player's body, so more of what's ahead/above shows.")]
    public float yOffset = 1.2f;
    public float ySmoothTime = 0.25f;
    [Tooltip("While airborne, follow only once the player is this far above / below the camera's anchor height.")]
    public float deadZoneUp = 2.2f;
    public float deadZoneDown = 1.5f;
    [Tooltip("While falling, the view drops this far below its usual framing so the landing spot is on screen before the player gets there.")]
    public float fallLookDown = 2.6f;

    Camera cam;
    PlayerController2D player;

    // ---- Screen shake: Shake(strength in world units, seconds). Up to 8 shakes run at once and add up
    // (as the root of the sum of squares, capped), so a small hit never cuts a big boss slam short.
    // Smooth Perlin noise rather than per-frame random jitter, so it rumbles instead of buzzing. It runs
    // on real time (it keeps going through a hit-stop) and stops while the game is paused.
    const int MaxShakes = 8;
    const float MaxShake = 0.6f, ShakeFrequency = 22f;
    static readonly float[] shakeAmp = new float[MaxShakes], shakeLeft = new float[MaxShakes], shakeLength = new float[MaxShakes];
    static float noiseTime;

    public static void Shake(float strength, float duration)
    {
        strength *= GameSettings.ShakeStrength;
        if (strength <= 0f || duration <= 0f) return;
        // Use a free slot, or replace the weakest one left.
        int slot = 0; float weakest = float.MaxValue;
        for (int i = 0; i < MaxShakes; i++)
        {
            float left = shakeLeft[i] > 0f ? shakeAmp[i] * shakeLeft[i] / shakeLength[i] : 0f;
            if (left < weakest) { weakest = left; slot = i; }
        }
        shakeAmp[slot] = strength;
        shakeLeft[slot] = shakeLength[slot] = duration;
    }

    // ---- Kick: a quick shove of the view in a direction (the way a hit flies, down for a slam), springing
    // straight back. Adds up across hits, capped so a flurry of hits can't push the view far.
    const float MaxKick = 0.3f, KickReturn = 0.09f;
    static Vector2 kick, kickVel;

    public static void Kick(Vector2 direction, float amount)
    {
        amount *= GameSettings.ShakeStrength;
        if (amount <= 0f || direction.sqrMagnitude < 0.0001f) return;
        kick = Vector2.ClampMagnitude(kick + direction.normalized * amount, MaxKick);
    }

    static Vector2 ShakeOffset(float dt)
    {
        float sum = 0f;
        for (int i = 0; i < MaxShakes; i++)
        {
            if (shakeLeft[i] <= 0f) continue;
            shakeLeft[i] -= dt;
            float a = shakeAmp[i] * Mathf.Clamp01(shakeLeft[i] / shakeLength[i]); // fades out
            sum += a * a;
        }
        kick = Vector2.SmoothDamp(kick, Vector2.zero, ref kickVel, KickReturn, Mathf.Infinity, dt);
        if (sum <= 0f) return kick;
        float amp = Mathf.Min(MaxShake, Mathf.Sqrt(sum));
        noiseTime += dt * ShakeFrequency;
        var n = new Vector2(Mathf.PerlinNoise(noiseTime, 0.37f) - 0.5f, Mathf.PerlinNoise(7.31f, noiseTime) - 0.5f) * 2f;
        return n * amp + kick;
    }
    // ---- Focus: the view swings over to a point for a moment (the boss's entrance roar, which started off screen)
    // and back to the player. Game time; eased in and out.
    static Vector2 focusPoint;
    static float focusLeft, focusTotal;

    public static void Focus(Vector2 point, float seconds)
    {
        focusPoint = point;
        focusLeft = focusTotal = Mathf.Max(0.1f, seconds);
    }

    // 0..1: how far the view is pulled to the focus point right now.
    static float FocusWeight()
    {
        if (focusLeft <= 0f) return 0f;
        float t = focusTotal - focusLeft;
        float w = Mathf.Min(Mathf.Clamp01(t / 0.45f), Mathf.Clamp01(focusLeft / 0.55f));
        return w * w * (3f - 2f * w);
    }

    float x, xVel;
    float lead, leadVel;
    float anchorY;  // player height the camera is framing
    float y, yVel;
    float fallLead, fallLeadVel, prevPy;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (target == null) return;
        player = target.GetComponent<PlayerController2D>();
        anchorY = prevPy = target.position.y;
        lead = Facing() * lookAhead;
        x = target.position.x + lead;
        y = anchorY + yOffset;
        transform.position = Clamp(new Vector3(x, y, transform.position.z));
        x = transform.position.x;
        y = transform.position.y;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (StageMap.Instance == null)
        {
            transform.position = new Vector3(target.position.x, fixedY, transform.position.z);
            return;
        }

        float dt = Time.deltaTime;
        if (focusLeft > 0f) focusLeft -= dt; // game time: a pause holds it
        float fw = FocusWeight();
        lead = Mathf.SmoothDamp(lead, Facing() * lookAhead, ref leadVel, lookAheadSmoothTime, Mathf.Infinity, dt);
        x = Mathf.SmoothDamp(x, Mathf.Lerp(target.position.x + lead, focusPoint.x, fw), ref xVel, fw > 0f ? 0.12f : xSmoothTime, Mathf.Infinity, dt);

        float py = target.position.y;
        bool airborne = player != null && player.IsAirborne;
        if (!airborne) anchorY = py;                         // landed / walking: frame this height
        else if (py > anchorY + deadZoneUp) anchorY = py - deadZoneUp;
        else if (py < anchorY - deadZoneDown) anchorY = py + deadZoneDown;
        // Falling far: catch up quickly so the player never drops out of view.
        float ySmooth = airborne && py < anchorY ? ySmoothTime * 0.4f : ySmoothTime;
        // Only a real drop below the take-off height, not the way down from an ordinary jump.
        bool falling = airborne && py < anchorY - 0.3f && dt > 0f && (py - prevPy) / dt < -3f;
        prevPy = py;
        fallLead = Mathf.SmoothDamp(fallLead, falling ? fallLookDown : 0f, ref fallLeadVel, 0.3f, Mathf.Infinity, dt);
        y = Mathf.SmoothDamp(y, Mathf.Lerp(anchorY + yOffset - fallLead, focusPoint.y, fw), ref yVel, ySmooth, Mathf.Infinity, dt);

        Vector3 p = Clamp(new Vector3(x, y, transform.position.z));
        // Stop the smoothing from pushing against a clamped edge (no lag when walking back).
        if (!Mathf.Approximately(p.x, x)) { x = p.x; xVel = 0f; }
        if (!Mathf.Approximately(p.y, y)) { y = p.y; yVel = 0f; }
        // Shake and kick (real time, so they carry on through a hit-stop; frozen while paused).
        if (!GameClock.Paused) p += (Vector3)ShakeOffset(Time.unscaledDeltaTime);
        transform.position = p;
    }

    float Facing() => player != null ? player.FacingSign : 1f;

    Vector3 Clamp(Vector3 d)
    {
        var map = StageMap.Instance;
        if (map == null || cam == null || !cam.orthographic) return d;
        Bounds b = map.WorldBounds;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        d.x = b.size.x > halfW * 2f ? Mathf.Clamp(d.x, b.min.x + halfW, b.max.x - halfW) : b.center.x;
        d.y = b.size.y > halfH * 2f ? Mathf.Clamp(d.y, b.min.y + halfH, b.max.y - halfH) : b.center.y;
        return d;
    }
}
