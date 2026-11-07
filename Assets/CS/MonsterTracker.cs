using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Once only a few monsters are left (the stage needs all of them), arrows at the screen
// edge point at the ones that are off-screen, so the last rat isn't a hunt across a
// 150-tile map. Created at runtime by RunManager in the stage scenes.
public class MonsterTracker : MonoBehaviour
{
    public int showAtOrBelow = 3;
    const float EdgeMargin = 90f;   // canvas units from the screen edge (the whole arrow stays inside)
    const float ArrowScale = 3f;    // whole art pixels: at x7 (77 units) it was as big as the cat's head
    // The minimap's corner (top right, StageMinimap: at most 420 x 180 + rim and margin): arrows stay below it.
    const float MapClearW = 470f, MapClearH = 230f;
    const int ArrowPx = 11;

    readonly List<RectTransform> arrows = new List<RectTransform>();
    readonly List<Enemy> targets = new List<Enemy>();
    readonly List<Enemy> alive = new List<Enemy>();
    RectTransform canvasRect;
    Camera cam;
    Sprite arrowSprite;

    public static MonsterTracker Create(Canvas canvas)
    {
        var go = new GameObject("MonsterTracker", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.SetSiblingIndex(0); // under the HUD and popups
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<MonsterTracker>();
        t.canvasRect = (RectTransform)canvas.transform;
        return t;
    }

    void Start()
    {
        cam = Camera.main;
        arrowSprite = MakeArrowSprite();
    }

    void LateUpdate()
    {
        alive.Clear();
        foreach (var e in Enemy.All)
            if (e != null && !e.IsDead && !e.isBoss) alive.Add(e);

        targets.Clear();
        bool vertical = StageMap.Instance != null && StageMap.Instance.verticalLayout;
        if (vertical && PlayerStats.Instance != null)
        {
            float playerY = PlayerStats.Instance.transform.position.y;
            foreach (var e in alive)
                if (e.transform.position.y < playerY - 3f) targets.Add(e);
            targets.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y)); // nearest missed floor first
            if (targets.Count > 2) targets.RemoveRange(2, targets.Count - 2);
        }
        if (targets.Count == 0 && alive.Count <= showAtOrBelow) targets.AddRange(alive);
        bool show = targets.Count > 0 && cam != null;

        int used = 0;
        if (show)
        {
            Vector2 size = canvasRect.rect.size;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var e in targets)
            {
                Vector3 vp = cam.WorldToViewportPoint(e.Renderer != null ? e.Renderer.bounds.center : e.transform.position);
                if (vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) continue; // on screen: no arrow

                Vector2 fromCenter = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                Vector2 half = size * 0.5f - Vector2.one * EdgeMargin;
                Vector2 dir = new Vector2(fromCenter.x * size.x, fromCenter.y * size.y);
                // Push the point out to the screen-edge rectangle along its direction.
                float k = Mathf.Min(Mathf.Abs(half.x / (dir.x == 0f ? 0.0001f : dir.x)), Mathf.Abs(half.y / (dir.y == 0f ? 0.0001f : dir.y)));
                Vector2 pos = dir * k;
                if (pos.x > half.x - MapClearW && pos.y > half.y - MapClearH) pos.y = half.y - MapClearH; // not over the minimap

                var arrow = GetArrow(used++);
                arrow.anchoredPosition = pos;
                arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                arrow.localScale = Vector3.one * (e.IsElite ? 1.25f : 1f) * pulse;
            }
        }
        for (int i = used; i < arrows.Count; i++) arrows[i].gameObject.SetActive(false);
    }

    RectTransform GetArrow(int i)
    {
        while (arrows.Count <= i)
        {
            var go = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ArrowPx * ArrowScale, ArrowPx * ArrowScale);
            var img = go.GetComponent<Image>();
            img.sprite = arrowSprite;
            img.raycastTarget = false;
            arrows.Add(rt);
        }
        arrows[i].gameObject.SetActive(true);
        return arrows[i];
    }

    // A small pixel arrow pointing right (red with a dark outline), drawn once.
    static Sprite MakeArrowSprite()
    {
        int n = ArrowPx;
        var tex = RuntimeGeneratedAssets.Texture(n, n);
        var clear = new Color(0, 0, 0, 0);
        var fill = new Color(1f, 0.32f, 0.28f);
        var edge = new Color(0.16f, 0.06f, 0.06f);
        int mid = n / 2;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Triangle: width shrinks toward the tip on the right.
                int halfH = (n - 1 - x) / 2;
                bool inside = Mathf.Abs(y - mid) <= halfH;
                bool inner = Mathf.Abs(y - mid) <= halfH - 1 && x >= 1 && x < n - 2;
                px[y * n + x] = inner ? fill : inside ? edge : clear;
            }
        tex.SetPixels(px);
        tex.Apply();
        return RuntimeGeneratedAssets.Sprite(tex, new Vector2(0.5f, 0.5f), 100f);
    }
}
