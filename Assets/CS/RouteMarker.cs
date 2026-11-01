using UnityEngine;

// Small route lamps placed through vertical stages: green lamps mark the broad safe path,
// red lamps mark exposed hunt branches. They make the route choice visible without opening the map.
public class RouteMarker : MonoBehaviour
{
    static Sprite white;
    SpriteRenderer glow;
    Color color;
    float phase;

    public static void Create(Vector2 feet, bool hunt)
    {
        var go = new GameObject(hunt ? "HuntLamp" : "SafeLamp");
        go.transform.position = feet;
        var marker = go.AddComponent<RouteMarker>();
        marker.color = hunt ? new Color(1f, 0.2f, 0.15f) : new Color(0.3f, 1f, 0.42f);
        marker.phase = Random.value * 6f;
        marker.Part("Post", new Vector3(0f, 0.32f, 0f), new Vector3(0.09f, 0.65f, 1f), new Color(0.28f, 0.16f, 0.1f), 2);
        marker.glow = marker.Part("Lamp", new Vector3(0f, 0.72f, 0f), new Vector3(0.28f, 0.28f, 1f), marker.color, 3);
    }

    SpriteRenderer Part(string name, Vector3 local, Vector3 scale, Color tint, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = White;
        renderer.color = tint;
        renderer.sortingOrder = order;
        return renderer;
    }

    static Sprite White
    {
        get
        {
            if (white != null) return white;
            white = RuntimeGeneratedAssets.WhiteSprite();
            return white;
        }
    }

    void Update()
    {
        if (glow == null) return;
        float k = 0.72f + 0.28f * Mathf.Sin(Time.time * 4f + phase);
        glow.color = new Color(color.r * k, color.g * k, color.b * k, 1f);
    }
}
