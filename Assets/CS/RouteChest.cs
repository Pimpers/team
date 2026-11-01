using System.Collections.Generic;
using UnityEngine;

// High-route reward using the same CC0 chest art as the stage reward.
public class RouteChest : MonoBehaviour
{
    public static readonly List<RouteChest> All = new List<RouteChest>();
    const int Gold = 12;
    static Sprite sprite;
    float homeY;
    float phase;
    bool taken;

    public static void Create(Vector2 floor)
    {
        var go = new GameObject("HuntRouteChest");
        go.transform.position = new Vector3(floor.x, floor.y + 0.08f, 0f);
        go.transform.localScale = Vector3.one * 0.62f;
        var chest = go.AddComponent<RouteChest>();
        chest.homeY = go.transform.position.y;
        chest.phase = Random.value * 6f;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite;
        renderer.sortingOrder = 3;
    }

    static Sprite Sprite => sprite != null
        ? sprite
        : (sprite = Resources.Load<Sprite>("WorldProps/TreasureChest/idle"));

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        transform.position = new Vector3(transform.position.x, homeY + Mathf.Sin(Time.time * 3f + phase) * 0.08f, 0f);
        var player = PlayerStats.Instance;
        if (taken || player == null || Vector2.Distance(player.transform.position, transform.position) > 1.15f) return;
        taken = true;
        // The hunt route's reward you feel on the spot (its rare-card bonus only pays every second hunt): enough
        // gold to notice and a half heart, announced with a banner rather than only a small popup.
        player.AddGold(Gold);
        player.Heal(0.5f);
        DamagePopup.SpawnLabel(transform.position + Vector3.up * 0.8f, Loc.F("route.treasure", Gold), UiTheme.Gold);
        var run = RunManager.Instance;
        if (run != null && run.hudText != null)
            StageBanner.Show(run.hudText.canvas.rootCanvas, run.hudText.font, Loc.T("route.treasure.title"), Loc.F("route.treasure.reward", Gold), 0.9f, compact: true);
        Sfx.Play("coin", 1f, 1.25f);
        WorldPropFx.PlayItemFeedback(transform.position + Vector3.up * 0.7f, 0.9f);
        Destroy(gameObject);
    }
}
