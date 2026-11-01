using UnityEngine;

// Replaces the mouse pointer with a pixel sword in every scene (Resources/Cursor/SwordCursor.png).
// Created automatically on game start; nothing has to be placed in a scene.
public class SwordCursor : MonoBehaviour
{
    // Half-size keeps the pointer clear of enemies and damage numbers.
    const int Downsample = 2;
    static readonly Vector2 Hotspot = new Vector2(2f, 2f);

    Texture2D cursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<SwordCursor>() != null) return;
        var go = new GameObject("SwordCursor");
        DontDestroyOnLoad(go);
        go.AddComponent<SwordCursor>();
    }

    void Awake()
    {
        var source = Resources.Load<Texture2D>("Cursor/SwordCursor");
        if (source != null)
        {
            int width = Mathf.Max(1, source.width / Downsample);
            int height = Mathf.Max(1, source.height / Downsample);
            cursor = RuntimeGeneratedAssets.Texture(width, height);
            cursor.name = "SwordCursorSmall";
            var src = source.GetPixels32();
            var dst = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    dst[y * width + x] = src[(y * Downsample) * source.width + x * Downsample];
            cursor.SetPixels32(dst);
            cursor.Apply(false, false);
        }
        Apply();
    }

    void Apply()
    {
        // Software mode: Unity draws the texture at its real size. A hardware cursor can be
        // shrunk by Windows to the system cursor size (often 32px).
        if (cursor != null) Cursor.SetCursor(cursor, Hotspot, CursorMode.ForceSoftware);
    }

    void OnApplicationFocus(bool focused)
    {
        // Windows can drop a custom cursor after alt-tab; set it again on return.
        if (focused) Apply();
    }
}
