using UnityEngine;

// SunnyLand forest sign with a coloured route arrow.
public class RouteSign : MonoBehaviour
{
    SpriteRenderer arrow;
    Color colour;
    float phase;

    public static void Create(Vector2 floor, bool hunt)
    {
        var root = new GameObject(hunt ? "HuntRouteSign" : "SafeRouteSign");
        root.transform.position = floor + Vector2.right * 3f;
        var sign = root.AddComponent<RouteSign>();
        sign.colour = hunt ? new Color(1f, 0.3f, 0.2f) : new Color(0.4f, 1f, 0.5f);
        sign.phase = Random.value * 6f;

        var signSprite = Resources.Load<Sprite>("WorldProps/SunnyLand/sign");
        if (signSprite != null) sign.SpritePart("ForestSign", signSprite, Vector3.zero, Vector3.one, Color.white, 2);

        var arrowSprite = Resources.Load<Sprite>("KenneyRpgUI/arrowBeige_right");
        if (arrowSprite != null)
        {
            sign.SpritePart("ArrowShadow", arrowSprite, new Vector3(0.04f, 0.91f, 0f), Vector3.one * 2.7f, new Color(0.08f, 0.04f, 0.03f, 0.9f), 3);
            sign.arrow = sign.SpritePart("Arrow", arrowSprite, new Vector3(0f, 0.95f, 0f), Vector3.one * 2.45f, sign.colour, 4);
        }
    }

    SpriteRenderer SpritePart(string name, Sprite sprite, Vector3 local, Vector3 scale, Color tint, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = order;
        return renderer;
    }

    void Update()
    {
        if (arrow == null) return;
        float glow = 0.82f + Mathf.Sin(Time.time * 3f + phase) * 0.18f;
        arrow.color = new Color(colour.r * glow, colour.g * glow, colour.b * glow, 1f);
    }
}
