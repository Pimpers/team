using UnityEngine;

// Sound-effect clips by id, played through Sfx.Play(id). Built by the editor tool
// AudioAssets (menu Cats Smash > Apply Audio) from Assets/Audio/Sfx - don't edit by hand,
// change the per-id volume table in AudioAssets instead.
public class SfxLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch spread (0.06 = +-6%) so repeated sounds don't feel robotic.")]
        public float pitchJitter = 0.06f;
        [Tooltip("Base pitch (1 = as recorded). The realistic foley is nudged up so the hits feel lighter and cuter.")]
        public float pitch = 1f;
    }

    public Entry[] entries;
}
