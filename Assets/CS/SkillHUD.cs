using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Bottom-centre skill bar for the run skills (SkillSystem): the Q / E / R slots with each active's
// icon, name and a dark cooldown sweep, and above them a row of small chips for the passives and
// enhancements picked so far (dimmed while a passive recharges - the 식빵 shield, the whisker counter).
// Created by RunManager in the stage scenes; it reads everything from PlayerStats / SkillSystem.
public class SkillHUD : MonoBehaviour
{
    // Big enough to read mid-fight. The pixel font only has 12 / 24 / 36 (PixelText.Dot), so a name that had to
    // shrink to fit an 84 px slot dropped straight to an unreadable 12: names stay 24 and wrap onto a second line.
    const float SlotSize = 104f, SlotGap = 96f, ChipSize = 56f, ChipGap = 8f, Bottom = 52f; // low (the bar covers less of the floor); the gap leaves room for a one-line name (냥냥 연속 베기)
    const float SlotY = 30f;
    const float NameSize = 24f, PlateOneLine = 36f, PlateTwoLines = 54f; // two lines of 24 with -14 spacing need no more

    class Slot
    {
        public RectTransform root;
        public Image frame, icon, shade, plate, keycap;
        public TMP_Text key, timer, label;
        public string id;
        public bool wasCooling;
        public float punch;
    }

    readonly Slot[] slots = new Slot[SkillSystem.MaxActiveSlots];
    readonly List<(string id, Image icon, Image shade)> chips = new List<(string, Image, Image)>();
    RectTransform chipRow;
    TMP_Text chipLabel; // "패시브" left of the chips: on its own the row read as a stray icon over the Q slot
    TMP_FontAsset font;
    int shownCount = -1;
    CanvasGroup group;
    Canvas canvas;
    static Sprite white;

    public static SkillHUD Create(Canvas canvas, TMP_FontAsset font)
    {
        var go = new GameObject("SkillHUD", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, Bottom);
        rt.sizeDelta = new Vector2(SlotSize * 3 + SlotGap * 2, SlotSize + ChipSize + 40f);
        var hud = go.AddComponent<SkillHUD>();
        hud.group = go.AddComponent<CanvasGroup>();
        hud.group.blocksRaycasts = false;
        hud.font = font;
        hud.Build(rt);
        return hud;
    }

    static Sprite White()
    {
        if (white != null) return white;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        return white;
    }

    static Image MakeImage(string name, Transform parent, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = White();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text MakeText(string name, Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(20, 12, 10, 255);
        return t;
    }

    // A dark wood plate hanging from `pos` (its top edge), for text that has to read over the level.
    static Image MakePlate(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var img = MakeImage(name, parent, size, UISkin.OverlayBack);
        if (UISkin.Instance != null) UISkin.Apply(img, UISkin.Instance.slate, 2f, new Color(1f, 1f, 1f, 0.95f)); // clean rim, 1 unit per art pixel (the plate is only 26 tall)
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        return img;
    }

    static void Stretch(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    // The plate hugs its one-line name: no wider than the name needs, no wider than the slot pitch.
    static void FitPlate(Slot s)
    {
        var rt = s.plate.rectTransform;
        float need = s.label.GetPreferredValues(s.label.text).x + 16f;
        rt.sizeDelta = new Vector2(Mathf.Clamp(need, SlotSize, SlotSize + SlotGap - 6f), PlateOneLine);
    }

    void Build(RectTransform root)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var s = new Slot();
            var go = new GameObject("Slot" + SkillSystem.SlotKeys[i], typeof(RectTransform));
            s.root = (RectTransform)go.transform;
            s.root.SetParent(root, false);
            s.root.anchorMin = s.root.anchorMax = new Vector2(0.5f, 0f);
            s.root.pivot = new Vector2(0.5f, 0f);
            s.root.sizeDelta = new Vector2(SlotSize, SlotSize);
            s.root.anchoredPosition = new Vector2((i - 1) * (SlotSize + SlotGap), SlotY);

            s.frame = MakeImage("Frame", s.root, new Vector2(SlotSize, SlotSize), new Color(0.07f, 0.05f, 0.08f, 0.78f));
            BookUI.Slice(s.frame, "Slot01a", 3f, new Color(1f, 1f, 1f, 0.95f)); // the book pack's inset slot, 3 units per pixel
            s.icon = MakeImage("Icon", s.root, new Vector2(SlotSize * 1.25f, SlotSize * 1.25f), Color.white); // effect frames have a wide empty margin
            s.icon.preserveAspect = true;
            s.shade = MakeImage("Cooldown", s.root, new Vector2(SlotSize - 10f, SlotSize - 10f), new Color(0f, 0f, 0f, 0.62f));
            s.shade.type = Image.Type.Filled;
            s.shade.fillMethod = Image.FillMethod.Radial360;
            s.shade.fillOrigin = (int)Image.Origin360.Top;
            s.shade.fillClockwise = false;
            s.shade.fillAmount = 0f;
            Vector2 keyPosition = new Vector2(-SlotSize * 0.5f + 15f, SlotSize * 0.5f - 15f);
            s.keycap = MakeImage("Keycap", s.root, new Vector2(26f, 26f), new Color(0.08f, 0.055f, 0.1f, 0.96f));
            s.keycap.rectTransform.anchoredPosition = keyPosition;
            s.key = MakeText("Key", s.root, SkillSystem.SlotKeys[i], UISkin.FontSmall, UISkin.OverlayText,
                             keyPosition, new Vector2(24f, 24f), TextAlignmentOptions.Center);
            s.key.outlineWidth = 0.22f;
            s.key.outlineColor = new Color32(24, 16, 20, 255);
            s.timer = MakeText("Timer", s.root, "", 40f, Color.white, Vector2.zero, new Vector2(SlotSize, 48f), TextAlignmentOptions.Center);
            s.timer.outlineWidth = 0.3f; s.timer.outlineColor = new Color32(20, 12, 10, 255); // the countdown over the dark sweep
            // The name sits on a small dark wood plate under the slot (same plate as the slots), so it reads over
            // any ground; up to two lines, kept inside the slot width so neighbouring names never run together.
            s.plate = MakePlate("NamePlate", s.root, new Vector2(0f, -SlotSize * 0.5f - 3f), new Vector2(SlotSize + SlotGap - 6f, PlateTwoLines));
            s.label = MakeText("Name", s.plate.rectTransform, "", NameSize, UISkin.BannerText, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            Stretch(s.label.rectTransform, 4f, 2f);
            s.label.textWrappingMode = TextWrappingModes.NoWrap; // one line: the slots are spaced for the longest name (냥냥 연속 베기); two lines read as two names
            s.label.overflowMode = TextOverflowModes.Overflow;
            s.label.lineSpacing = -14f;
            s.label.enableAutoSizing = false;
            s.label.fontSize = NameSize;
            s.label.outlineWidth = 0f; // on the dark plate it needs none (an outline eats 1-dot strokes)
            slots[i] = s;
        }

        var row = new GameObject("Passives", typeof(RectTransform));
        chipRow = (RectTransform)row.transform;
        chipRow.SetParent(root, false);
        chipRow.anchorMin = chipRow.anchorMax = new Vector2(0.5f, 0f);
        chipRow.pivot = new Vector2(0.5f, 0f);
        chipRow.anchoredPosition = new Vector2(0f, SlotY + SlotSize + 10f);
        chipRow.sizeDelta = new Vector2(600f, ChipSize);
        chipLabel = MakeText("PassivesLabel", chipRow, Loc.T("hud.passives"), UISkin.FontSmall, UISkin.OverlayText, Vector2.zero, new Vector2(120f, ChipSize), TextAlignmentOptions.MidlineRight);
        UiTheme.Backdrop(chipLabel);

    }

    // Middle frame of the effect (effect sheets start and end nearly empty).
    public static Sprite Icon(CardPool.Card card)
    {
        if (card != null && card.Art != null) return card.Art;
        var frames = card != null && !string.IsNullOrEmpty(card.icon) ? Vfx.Frames(card.icon) : null;
        if (frames == null) return null;
        return frames[frames.Length == 1 ? 0 : frames.Length * 2 / 5];
    }

    // TMP breaks Korean between any two letters ("냥냥 연속 베 / 기"); wrap each word in <nobr> so lines only break at spaces.
    static string KeepWords(string name) => "<nobr>" + name.Replace(" ", "</nobr> <nobr>") + "</nobr>";

    void Rebuild(PlayerStats stats)
    {
        shownCount = stats.SkillOrder.Count;
        var actives = stats.ActiveSkills;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            s.id = i < actives.Count ? actives[i] : null;
            var card = s.id != null ? CardPool.Get(s.id) : null;
            s.icon.sprite = Icon(card);
            s.icon.rectTransform.sizeDelta = Vector2.one * SlotSize * (card != null && card.Art != null ? 0.68f : 1.25f);
            s.icon.enabled = s.icon.sprite != null;
            s.label.text = card != null ? KeepWords(card.name) : "";
            s.root.gameObject.SetActive(card != null); // no giant empty Q/E/R boxes before a skill is learned
            if (card != null)
                s.root.anchoredPosition = new Vector2((i - (actives.Count - 1) * 0.5f) * (SlotSize + SlotGap), SlotY);
            s.plate.enabled = card != null;
            FitPlate(s);
            s.frame.color = new Color(1f, 1f, 1f, card != null ? 0.95f : 0.75f);
            Vector2 keyPosition = new Vector2(-SlotSize * 0.5f + 15f, SlotSize * 0.5f - 15f);
            s.keycap.rectTransform.anchoredPosition = keyPosition;
            s.key.rectTransform.anchoredPosition = keyPosition;
            s.key.rectTransform.sizeDelta = new Vector2(24f, 24f);
            s.key.fontSize = UISkin.FontSmall;
            s.key.alpha = 1f;
        }

        foreach (var c in chips) if (c.icon != null) Destroy(c.icon.transform.parent.gameObject);
        chips.Clear();
        var passives = new List<CardPool.Card>();
        foreach (var id in stats.SkillOrder)
        {
            var c = CardPool.Get(id);
            if (c != null && c.kind != CardPool.Kind.Active) passives.Add(c);
        }
        for (int i = 0; i < passives.Count; i++)
        {
            var go = new GameObject("Chip_" + passives[i].id, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(chipRow, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(ChipSize, ChipSize);
            rt.anchoredPosition = new Vector2((i - (passives.Count - 1) * 0.5f) * (ChipSize + ChipGap), 0f);
            var bg = MakeImage("Bg", rt, new Vector2(ChipSize, ChipSize), new Color(0.07f, 0.05f, 0.08f, 0.7f));
            BookUI.Slice(bg, "Slot01b", 2f, new Color(1f, 1f, 1f, 0.9f));
            var icon = MakeImage("Icon", rt, new Vector2(ChipSize * 1.25f, ChipSize * 1.25f), Color.white);
            icon.sprite = Icon(passives[i]);
            if (passives[i].Art != null) icon.rectTransform.sizeDelta = Vector2.one * ChipSize * 0.82f;
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            var shade = MakeImage("Recharge", rt, new Vector2(ChipSize - 6f, ChipSize - 6f), new Color(0f, 0f, 0f, 0.6f));
            shade.type = Image.Type.Filled;
            shade.fillMethod = Image.FillMethod.Vertical;
            shade.fillOrigin = (int)Image.OriginVertical.Top;
            shade.fillAmount = 0f;
            int lv = stats.CardLevel(passives[i].id);
            if (lv >= 2) MakeText("Lv", rt, "+", 22f, new Color(1f, 0.85f, 0.35f), new Vector2(ChipSize * 0.32f, -ChipSize * 0.3f), new Vector2(20f, 20f), TextAlignmentOptions.Center);
            chips.Add((passives[i].id, icon, shade));
        }
        chipLabel.gameObject.SetActive(passives.Count > 0);
        if (passives.Count > 0)
        {
            var lrt = chipLabel.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(1f, 0f);
            lrt.anchoredPosition = new Vector2(-passives.Count * (ChipSize + ChipGap) * 0.5f - 6f, 0f);
        }
    }

    int levelSum = -1;

    void Update()
    {
        var stats = PlayerStats.Instance;
        var sys = SkillSystem.Instance;
        if (stats == null) return;
        int sum = 0;
        foreach (var id in stats.SkillOrder) sum += stats.CardLevel(id);
        if (stats.SkillOrder.Count != shownCount || sum != levelSum) { levelSum = sum; Rebuild(stats); }

        foreach (var s in slots)
        {
            if (s.id == null || sys == null) { s.shade.fillAmount = 0f; s.timer.text = ""; continue; }
            float left = sys.CooldownLeft(s.id), total = Mathf.Max(0.01f, sys.Cooldown(s.id));
            bool cooling = left > 0f;
            s.shade.fillAmount = cooling ? left / total : 0f;
            s.timer.text = cooling ? (left >= 1f ? Mathf.CeilToInt(left).ToString() : left.ToString("0.0")) : "";
            if (s.wasCooling && !cooling) s.punch = 0.18f; // ready again: a little bounce
            s.wasCooling = cooling;
            if (s.punch > 0f) s.punch -= Time.unscaledDeltaTime;
            float k = s.punch > 0f ? 1f + 0.12f * Mathf.Sin(s.punch / 0.18f * Mathf.PI) : 1f;
            s.root.localScale = new Vector3(k, k, 1f);
        }

        foreach (var c in chips)
            c.shade.fillAmount = sys != null ? 1f - sys.PassiveCharge(c.id) : 0f;

        // The bar sits over the bottom of the play area: when the cat or a monster is behind it
        // (a lower floor), fade it so they stay visible.
        if (group != null)
        {
            bool covered = Covers(stats.transform.position) || Covers(stats.transform.position + Vector3.down * 1f);
            if (!covered)
                foreach (var e in Enemy.All)
                    if (e != null && !e.IsDead && Covers(e.transform.position + Vector3.up * 0.4f)) { covered = true; break; }
            // Out of the way of the card draft, the pause menu and the result board (it is drawn over them).
            bool popup = GameClock.Paused || BuildSheet.Showing || (RunManager.Instance != null && RunManager.Instance.RunEnded);
            group.alpha = popup ? 0f : Mathf.MoveTowards(group.alpha, covered ? 0.3f : 1f, Time.unscaledDeltaTime * 4f);
        }
    }

    bool Covers(Vector3 world)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        Vector2 sp = cam.WorldToScreenPoint(world);
        if (canvas == null) canvas = GetComponentInParent<Canvas>(); // (found once: this ran per frame)
        var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, sp, uiCam);
    }
}
