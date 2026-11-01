using System.Collections.Generic;
using UnityEngine;

// Owns textures, sprites and materials created at runtime, so editor play sessions
// and reload-disabled projects do not keep stale generated objects.
public static class RuntimeGeneratedAssets
{
    static readonly List<Object> owned = new List<Object>();
    static Sprite white;

    public static Texture2D Texture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        owned.Add(texture);
        return texture;
    }

    public static Sprite Sprite(Texture2D texture, Vector2 pivot, float pixelsPerUnit)
    {
        var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit);
        sprite.hideFlags = HideFlags.DontSave;
        owned.Add(sprite);
        return sprite;
    }

    public static Sprite WhiteSprite()
    {
        if (white != null) return white;
        var texture = Texture(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        white = Sprite(texture, new Vector2(0.5f, 0.5f), 1f);
        return white;
    }

    public static T Track<T>(T value) where T : Object
    {
        if (value != null)
        {
            value.hideFlags = HideFlags.DontSave;
            owned.Add(value);
        }
        return value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        foreach (var value in owned)
        {
            if (value == null) continue;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
        owned.Clear();
        white = null;
    }
}
