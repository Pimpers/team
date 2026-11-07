using UnityEngine;

// Attach to the Main Camera of a stage. Draws the sky / mountains / hills layers behind
// the tilemaps, each scaled to fill the view height and repeated sideways. A layer with
// follow = 1 stays fixed on screen (infinitely far); lower values drift past more slowly
// than the ground does. Runs after CameraFollow so it uses this frame's camera position.
[DefaultExecutionOrder(100)]
public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Sprite sprite;
        [Range(0f, 1f)] public float follow = 0.9f;
        public int sortingOrder = -100;
        [Range(0.4f, 1.2f)] public float heightFraction = 1f;
        public bool alignBottom;
        [Tooltip("Colour multiplied onto the layer - the stage mood (sunset, dusk, crimson).")]
        public Color tint = Color.white;
    }

    public Layer[] layers;
    const int Copies = 5; // enough to cover ultra-wide views at any scroll offset

    Camera cam;
    Transform[] roots;
    float[] widths;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null || layers == null) return;

        roots = new Transform[layers.Length];
        widths = new float[layers.Length];
        float viewHeight = cam.orthographicSize * 2f;
        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            if (layer.sprite == null) continue;

            var root = new GameObject($"Parallax_{layer.sprite.name}").transform;
            root.SetParent(transform, false);
            float layerHeight = viewHeight * Mathf.Clamp(layer.heightFraction, 0.4f, 1.2f);
            float scale = layerHeight / layer.sprite.bounds.size.y;
            float y = layer.alignBottom ? -viewHeight * 0.5f + layerHeight * 0.5f : 0f;
            root.localPosition = new Vector3(0f, y, 20f);
            root.localScale = new Vector3(scale, scale, 1f);
            widths[i] = layer.sprite.bounds.size.x * scale;

            for (int k = 0; k < Copies; k++)
            {
                var go = new GameObject("Tile");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3((k - Copies / 2) * layer.sprite.bounds.size.x, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = layer.sprite;
                sr.sortingOrder = layer.sortingOrder;
                sr.color = layer.tint * FarMute;
            }
            roots[i] = root;
        }
        foreach (var t in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>()) t.color *= TileMute;
        LateUpdate();
    }

    // Fight readability, by tint only (the PNGs and materials stay): the far layers darker, with red cut most so the
    // warm forest also loses colour; the tilemaps (ground, platforms) a touch darker and less purple / lime.
    static readonly Color FarMute = new Color(0.58f, 0.66f, 0.74f, 1f);
    static readonly Color TileMute = new Color(0.9f, 0.86f, 0.92f, 1f);

    void LateUpdate()
    {
        if (roots == null) return;
        float camX = transform.position.x;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null) continue;
            // The layer moves through the world at `follow` x the camera's speed, so relative
            // to the camera it slides back by (1 - follow); wrap that within one image width.
            float w = widths[i];
            float offset = -Mathf.Repeat(camX * (1f - layers[i].follow), w);
            if (offset < -w * 0.5f) offset += w;
            var p = roots[i].localPosition;
            roots[i].localPosition = new Vector3(offset, 0f, p.z);
        }
    }
}
