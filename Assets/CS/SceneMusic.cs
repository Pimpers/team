using UnityEngine;

// Background music for one scene: loops while the scene is open. Overall loudness follows the settings volume
// through the AudioListener (GameSettings).
// Scene changes cross-fade: when the scene unloads, its music hands over to a short-lived "tail" kept across the
// load, which fades out over FadeOut while the next scene's music fades in. If the next scene plays the same clip
// (stage to stage), it carries on from the tail's position at full volume instead - no dip, no restart.
[RequireComponent(typeof(AudioSource))]
public class SceneMusic : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Seconds to fade in from silence at the start.")]
    public float fadeIn = 1.5f;

    const float FadeOut = 1.2f;
    static AudioSource tail; // the last scene's music, fading out across the load
    static bool quitting;

    AudioSource source;
    float t;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { tail = null; quitting = false; }

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = fadeIn > 0f ? 0f : volume;
        if (clip == null) return;
        if (tail != null && tail.clip == clip)
        {
            // Same track as the scene before: pick up where it was, already at full volume.
            source.time = tail.time;
            t = fadeIn;
            Destroy(tail.gameObject);
            tail = null;
        }
        source.Play();
    }

    void Update()
    {
        if (t < fadeIn) t += Time.unscaledDeltaTime;
        float fade = fadeIn > 0f ? Mathf.Clamp01(t / fadeIn) : 1f;
        source.volume = volume * fade * GameSettings.MusicVolume; // the settings slider applies live
    }

    void OnApplicationQuit() => quitting = true;

    // The scene is unloading: keep the music going on a tail that fades out under the next scene's fade-in.
    void OnDestroy()
    {
        if (quitting || source == null || !source.isPlaying || !Application.isPlaying) return;
        if (tail != null) Destroy(tail.gameObject);
        var go = new GameObject("MusicTail");
        DontDestroyOnLoad(go);
        tail = go.AddComponent<AudioSource>();
        tail.clip = source.clip;
        tail.loop = true;
        tail.spatialBlend = 0f;
        tail.volume = source.volume;
        tail.time = source.time;
        tail.Play();
        go.AddComponent<TailFade>().from = source.volume;
    }

    class TailFade : MonoBehaviour
    {
        public float from;
        float left = FadeOut;
        AudioSource src;

        void Awake() => src = GetComponent<AudioSource>();

        void Update()
        {
            left -= Time.unscaledDeltaTime;
            if (src != null) src.volume = from * Mathf.Clamp01(left / FadeOut);
            if (left <= 0f) Destroy(gameObject);
        }
    }
}
