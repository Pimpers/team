using UnityEngine;
using UnityEngine.SceneManagement;

// Living backgrounds: pixelfx loops (Resources/Ambient/<name>/<name>_NN.png, rendered from the pixelfx library) laid
// over the world map and the harbor village - fireflies on the open forest, a smoking volcano, a storm over the
// fortress, snow on the frost peaks, a whirlpool, drifting clouds; the village's campfire, chimney smoke, birds
// and ripples. Placed at fractions of the background picture (u from the left, v from the top), so they stay on
// their spot of the art at any screen size.
public class AmbientFx : MonoBehaviour
{
    Sprite[] frames;
    SpriteRenderer sr;
    float fps, t;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "WorldMapScene") WorldMap();
        else if (scene.name == "MainScene") Village();
    }

    static SpriteRenderer Background(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<SpriteRenderer>() : null;
    }

    static void WorldMap()
    {
        var bg = Background("WorldMapBackground");
        if (bg == null) return;
        // (Full-screen weather - clouds, storm, snow, fireflies - read as smudges or boxes at map scale; the
        // open forest already has its pulsing gold ring.)
        Place(bg, "volcano", 0.79f, 0.27f, 0.03f, 11, 1f);       // a flame on the volcano's crater
        Place(bg, "whirlpool", 0.52f, 0.78f, 0.05f, 11, 0.9f);  // a whirlpool out at sea
    }

    static void Village()
    {
        var bg = Background("HarborBackground");
        if (bg == null) return;
        Place(bg, "campfire", 0.633f, 0.5f, 0.035f, 4, 1f);     // the campfire in the square
        Place(bg, "chimney", 0.51f, 0.19f, 0.09f, 4, 0.75f);    // smoke off the roofs
        Place(bg, "birds", 0.3f, 0.12f, 0.03f, 6, 1f);          // gulls over the harbor
        Place(bg, "ripples", 0.18f, 0.77f, 0.06f, 4, 0.8f);     // water round the boats
        Place(bg, "ripples", 0.43f, 0.9f, 0.06f, 4, 0.8f);
    }

    // `width`: the loop's width as a fraction of the background's.
    public static AmbientFx Place(SpriteRenderer bg, string name, float u, float v, float width, int order, float alpha, float fps = 12f)
    {
        var sprites = Resources.LoadAll<Sprite>("Ambient/" + name);
        if (sprites == null || sprites.Length == 0) return null;
        System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
        var b = bg.bounds;
        var go = new GameObject("Ambient_" + name);
        go.transform.position = new Vector3(b.min.x + u * b.size.x, b.max.y - v * b.size.y, 0f);
        float spriteWidth = sprites[0].bounds.size.x;
        go.transform.localScale = Vector3.one * (spriteWidth > 0f ? width * b.size.x / spriteWidth : 1f);
        var fx = go.AddComponent<AmbientFx>();
        fx.frames = sprites;
        fx.fps = fps;
        fx.t = Random.value * sprites.Length / fps; // two of a kind don't move in step
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = sprites[0];
        fx.sr.sortingOrder = order;
        fx.sr.color = new Color(1f, 1f, 1f, alpha);
        return fx;
    }

    void Update()
    {
        t += Time.deltaTime;
        sr.sprite = frames[(int)(t * fps) % frames.Length];
    }
}
