using UnityEngine;

// Background music for one scene: loops while the scene is open and stops by itself when
// the next scene loads (the object isn't kept across scenes). Overall loudness follows the
// settings volume through the AudioListener (GameSettings).
[RequireComponent(typeof(AudioSource))]
public class SceneMusic : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Seconds to fade in from silence at the start.")]
    public float fadeIn = 1.5f;

    AudioSource source;
    float t;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = fadeIn > 0f ? 0f : volume;
        if (clip != null) source.Play();
    }

    void Update()
    {
        if (t < fadeIn) t += Time.unscaledDeltaTime;
        float fade = fadeIn > 0f ? Mathf.Clamp01(t / fadeIn) : 1f;
        source.volume = volume * fade * GameSettings.MusicVolume; // the settings slider applies live
    }
}
