using System.Collections.Generic;
using UnityEngine;

// A one-pixel outline around an elite monster, drawn behind it: four flat-colour silhouettes of its current sprite,
// one pixel up / down / left / right. It reads on every monster (a gold tint barely shows on the dark spider) and
// is pixel art all the way: whole pixels, solid colour, stepping between gold and ember red instead of fading.
public class EliteOutline : MonoBehaviour
{
    static readonly Color Gold = UiTheme.Gold, Ember = new Color(1f, 0.38f, 0.18f);
    static readonly Vector2[] Dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    // source sprite -> the same sprite as a white silhouette (so a colour can tint it flat)
    static readonly Dictionary<Sprite, Sprite> flat = new Dictionary<Sprite, Sprite>();

    SpriteRenderer src;
    readonly SpriteRenderer[] rings = new SpriteRenderer[4];

    public static GameObject Attach(SpriteRenderer source)
    {
        var go = new GameObject("EliteOutline");
        go.transform.SetParent(source.transform, false);
        go.AddComponent<EliteOutline>().Init(source);
        return go;
    }

    void Init(SpriteRenderer source)
    {
        src = source;
        for (int i = 0; i < rings.Length; i++)
        {
            var r = new GameObject("Ring" + i).AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            rings[i] = r;
        }
    }

    void LateUpdate()
    {
        if (src == null) { Destroy(gameObject); return; }
        var sprite = src.sprite;
        var silhouette = sprite != null ? Flat(sprite) : null;
        // Gold and ember red swap 3 times a second.
        var color = Mathf.FloorToInt(Time.time * 3f) % 2 == 0 ? Gold : Ember;
        float px = sprite != null ? 1f / sprite.pixelsPerUnit : 0f;
        for (int i = 0; i < rings.Length; i++)
        {
            var r = rings[i];
            r.sprite = silhouette;
            r.flipX = src.flipX;
            r.color = color;
            r.sortingLayerID = src.sortingLayerID;
            r.sortingOrder = src.sortingOrder - 1;
            r.enabled = src.enabled;
            r.transform.localPosition = (Vector3)(Dirs[i] * px);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => flat.Clear();

    // White copy of the sprite: the texture is read back through a render texture, so it works even when the
    // texture is not marked readable.
    static Sprite Flat(Sprite s)
    {
        if (flat.TryGetValue(s, out var cached)) return cached;
        var rect = s.rect; // the full cell (textureRect is the trimmed part: its pivot would be off)
        int w = (int)rect.width, h = (int)rect.height;
        var rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(s.texture, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = RuntimeGeneratedAssets.Texture(w, h);
        tex.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(px[i].a >= 110 ? 255 : 0));
        tex.SetPixels32(px);
        tex.Apply();
        var result = RuntimeGeneratedAssets.Sprite(tex, new Vector2(s.pivot.x / w, s.pivot.y / h), s.pixelsPerUnit);
        flat[s] = result;
        return result;
    }
}
