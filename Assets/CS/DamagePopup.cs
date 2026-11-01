using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Floating damage number over a hit enemy: pops in, rises, then fades out. Strong hits
// (finishers, charged shots) are bigger and gold. Uses the HUD's font so it matches the UI.
public class DamagePopup : MonoBehaviour
{
    const float Life = 0.7f;
    const float Rise = 0.9f;

    TextMeshPro text, shadow;
    Vector3 start;
    float t;
    bool numeric;
    int value;
    bool critical;

    // Numbers spawned in the last moment, so a fresh one can nudge away from them instead of landing on top.
    static readonly List<DamagePopup> live = new List<DamagePopup>();
    const float SpreadX = 0.55f; // how far apart two numbers must sit before they stop overlapping

    public static void Spawn(Vector3 position, int amount, bool strong, bool crit = false)
    {
        position = BossSafePosition(position);
        var merged = FindMerge(position);
        if (merged != null)
        {
            merged.MergeDamage(amount, strong, crit);
            return;
        }
        if (crit)
        {
            // Colour and the exclamation mark carry the critical message without another label.
            Vector3 p = position + new Vector3(Random.Range(-0.2f, 0.2f), 0.2f, 0f);
            var popup = Create(p, amount + "!", UiTheme.Critical, 8.5f);
            popup.numeric = true; popup.value = amount; popup.critical = true;
            return;
        }
        Color color = strong ? UiTheme.Gold : Color.white;
        var number = Create(position + new Vector3(Random.Range(-0.25f, 0.25f), 0.2f, 0f), amount.ToString(), color, strong ? 7.5f : 6.2f);
        number.numeric = true; number.value = amount;
    }

    // Any short label (e.g. the "!" an enemy shows when it spots the player).
    public static void SpawnLabel(Vector3 position, string label, Color color, float sizeMultiplier = 1f)
    {
        int chars = string.IsNullOrEmpty(label) ? 0 : label.Replace("\n", "").Length;
        float size = label != null && label.Contains("\n") ? 3.4f : chars >= 4 ? 3.7f : 6.2f;
        size *= Mathf.Clamp(sizeMultiplier, 0.5f, 1.5f);
        Create(StackLabel(position + new Vector3(0f, 0.45f, 0f)), label, color, size);
    }

    // Numbers were sized for the old 2.5-unit cat; the characters are ~0.55x now.
    const float SizeScale = 0.65f;

    // Labels use higher lanes than numbers, so nearby enemy warnings stay readable.
    static Vector3 StackLabel(Vector3 position)
    {
        int lane = 0;
        foreach (var popup in live)
            if (popup != null && !popup.numeric && popup.t < 0.45f &&
                Mathf.Abs(popup.start.x - position.x) < 2f && Mathf.Abs(popup.start.y - position.y) < 2f)
                lane++;
        position.y += Mathf.Min(2, lane) * 0.58f;
        return position;
    }

    // Boss numbers sit beside the body instead of covering its face and wind-up poses.
    static Vector3 BossSafePosition(Vector3 position)
    {
        foreach (var enemy in Enemy.All)
        {
            if (enemy == null || enemy.IsDead || !enemy.isBoss) continue;
            Vector2 centre = enemy.Renderer != null ? enemy.Renderer.bounds.center : enemy.transform.position;
            if (Vector2.Distance(position, centre) > 4f) continue;
            float playerX = PlayerStats.Instance != null ? PlayerStats.Instance.transform.position.x : enemy.BodyX - 1f;
            float side = playerX < enemy.BodyX ? 1f : -1f;
            return new Vector3(enemy.BodyX + side * (enemy.BodyHalfWidth + 0.65f), enemy.BodyTopY - 0.25f, position.z);
        }
        return position;
    }

    static DamagePopup Create(Vector3 position, string label, Color color, float size)
    {
        size *= SizeScale;
        var go = new GameObject("DamagePopup");
        var popup = go.AddComponent<DamagePopup>();
        popup.start = Spread(position); // shove off any number already floating near this spot
        go.transform.position = popup.start;
        popup.shadow = MakeText(go.transform, label, size, new Color(0.08f, 0.05f, 0.08f, 1f), 59, new Vector3(0.07f, -0.07f, 0f));
        popup.text = MakeText(go.transform, label, size, color, 60, Vector3.zero);
        live.Add(popup);
        return popup;
    }

    static DamagePopup FindMerge(Vector3 position)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var popup = live[i];
            if (popup == null || !popup.numeric || popup.t > 0.18f) continue;
            if (Vector2.Distance(popup.start, position) < 0.8f) return popup;
        }
        return null;
    }

    void MergeDamage(int amount, bool strong, bool crit)
    {
        value += amount;
        critical |= crit;
        string label = value + (critical ? "!" : "");
        text.text = shadow.text = label;
        text.color = critical ? UiTheme.Critical : strong ? UiTheme.Gold : Color.white;
        t = Mathf.Min(t, 0.06f);
    }

    // Nudge the spawn point sideways while anything is still floating within SpreadX of it, so a burst of
    // numbers (multi-hit, several enemies at once) fans out left and right instead of stacking on one spot.
    static Vector3 Spread(Vector3 p)
    {
        for (int tries = 0; tries < 6; tries++)
        {
            bool clash = false;
            foreach (var o in live)
            {
                if (o == null) continue;
                if (Mathf.Abs(o.transform.position.x - p.x) < SpreadX && Mathf.Abs(o.transform.position.y - p.y) < 0.4f)
                {
                    // push to the emptier side of the one we hit
                    p.x += (p.x >= o.transform.position.x ? 1f : -1f) * SpreadX;
                    if (tries >= 2) p.y += 0.42f; // both sides taken: step up a row instead of bouncing
                    clash = true;
                    break;
                }
            }
            if (!clash) break;
        }
        return p;
    }

    static TextMeshPro MakeText(Transform parent, string label, float size, Color color, int order, Vector3 offset)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        var tmp = go.AddComponent<TextMeshPro>();
        var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
        if (hud != null) tmp.font = hud.font;
        tmp.text = label;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
        tmp.sortingOrder = order;
        if (order == 60) tmp.fontSharedMaterial = Outlined(tmp.fontSharedMaterial); // white/yellow numbers vanished against a bright sky
        return tmp;
    }

    void Update()
    {
        t += Time.deltaTime; // scaled: freezes with the hit stop, which reads well
        float k = Mathf.Clamp01(t / Life);
        transform.position = start + Vector3.up * Rise * (1f - (1f - k) * (1f - k));
        transform.localScale = Vector3.one * (1f + 0.5f * (1f - Mathf.Clamp01(t / 0.12f)));
        float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
        SetAlpha(text, alpha);
        SetAlpha(shadow, alpha);
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy() => live.Remove(this);

    // One shared outlined copy of the font material (setting outlineWidth per popup would make a material per number).
    static Material outlined;
    static Material Outlined(Material source)
    {
        if (outlined != null) return outlined;
        outlined = RuntimeGeneratedAssets.Track(new Material(source) { name = source.name + " (popup outline)" });
        outlined.EnableKeyword("OUTLINE_ON");
        outlined.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        outlined.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(30, 18, 12, 255));
        return outlined;
    }

    static void SetAlpha(TextMeshPro tmp, float a)
    {
        var c = tmp.color;
        c.a = a;
        tmp.color = c;
    }
}
