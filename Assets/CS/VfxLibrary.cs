using UnityEngine;

// Animated effect sprites by id, played through Vfx.Play(id, ...). Built by the editor tool
// VfxAssets (menu Cats Smash > Apply Effects) from the sheets in Assets/Effects - don't edit by hand.
public class VfxLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string id;
        public Sprite[] frames;
        public float fps = 16f;
        [Tooltip("World size multiplier: 1 = one sprite pixel is one tile pixel (1/16 unit).")]
        public float scale = 1f;
        public int sortingOrder = 7;
        [Tooltip("Pixel-art sheet drawn at the cats' pixel size: played only at x0.5 / x1 / x2 so its pixels stay whole.")]
        public bool pixelSnap;
        [Tooltip("With pixelSnap: any angle allowed (it points along a flight). Otherwise turned in 90 degree steps.")]
        public bool freeRotation;
    }

    public Entry[] entries;
    [HideInInspector] public int version;
}
