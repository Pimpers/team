using UnityEngine;

// One-pixel dark silhouette around the player, keeping the cat readable over tree trunks.
public class PlayerOutline : MonoBehaviour
{
    static readonly Vector2[] Dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    static readonly Color Ink = new Color(0.12f, 0.09f, 0.14f, 1f);
    const float ArtTexels = 1f; // texels per art pixel (the x8 is the transform scale)
    SpriteRenderer source;
    readonly SpriteRenderer[] copies = new SpriteRenderer[4];

    public static void Attach(SpriteRenderer source)
    {
        if (source == null || source.GetComponentInChildren<PlayerOutline>() != null) return;
        var go = new GameObject("PlayerOutline");
        go.transform.SetParent(source.transform, false);
        var outline = go.AddComponent<PlayerOutline>();
        outline.source = source;
        for (int i = 0; i < outline.copies.Length; i++)
        {
            var copy = new GameObject("Edge" + i).AddComponent<SpriteRenderer>();
            copy.transform.SetParent(go.transform, false);
            outline.copies[i] = copy;
        }
    }

    void LateUpdate()
    {
        if (source == null) { Destroy(gameObject); return; }
        var sprite = source.sprite;
        // one whole art pixel: the copies sat half a pixel out before, a blur at the edge rather than an outline
        float pixel = sprite != null ? ArtTexels / sprite.pixelsPerUnit : 0f;
        for (int i = 0; i < copies.Length; i++)
        {
            var copy = copies[i];
            copy.sprite = sprite;
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.color = Ink;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder - 1;
            copy.enabled = source.enabled;
            copy.transform.localPosition = (Vector3)(Dirs[i] * pixel);
        }
    }
}
