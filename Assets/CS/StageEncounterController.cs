using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Shared room progression for horizontal and vertical stages.
// Direction-specific subclasses only draw gates and map their collision axis.
public abstract class StageEncounterController : MonoBehaviour
{
    protected class Gate
    {
        public float edge;
        public GameObject root;
        public SpriteRenderer plate;
        public TextMeshPro label;
        public SpriteRenderer pointer;
        public bool open;
    }

    protected StageMap map;
    protected PlayerController2D player;
    protected readonly List<List<Enemy>> rooms = new List<List<Enemy>>();
    protected readonly List<Gate> gates = new List<Gate>();
    int currentRoom;
    float readyDelay = 0.35f;

    protected void Configure(StageMap stageMap, GameObject enemyGroup, float[] edges)
    {
        map = stageMap;
        player = PlayerStats.Instance != null ? PlayerStats.Instance.Body : null;
        for (int i = 0; i <= edges.Length; i++) rooms.Add(new List<Enemy>());
        foreach (var enemy in enemyGroup.GetComponentsInChildren<Enemy>())
        {
            if (enemy == null || enemy.isBoss || !enemy.gameObject.activeInHierarchy) continue;
            int room = 0;
            float value = Axis(enemy.transform.position);
            while (room < edges.Length && value >= edges[room]) room++;
            rooms[room].Add(enemy);
        }
        foreach (float edge in edges)
        {
            AddBarrier(edge);
            gates.Add(BuildGate(edge));
        }
        for (int room = 1; room < rooms.Count; room++)
            foreach (var enemy in rooms[room]) if (enemy != null) enemy.gameObject.SetActive(false);
        ShowGate(0);
    }

    protected abstract float Axis(Vector3 position);
    protected abstract bool Passed(float edge);
    protected abstract void AddBarrier(float edge);
    protected abstract void RemoveBarrier(float edge);
    protected abstract Gate BuildGate(float edge);
    protected abstract Vector3 GateEffectPosition(float edge);
    protected abstract Color GateEffectColor { get; }

    void ShowGate(int index)
    {
        if (index < 0 || index >= gates.Count) return;
        if (gates[index].label != null) gates[index].label.gameObject.SetActive(false);
        if (gates[index].plate != null) gates[index].plate.enabled = false;
        if (gates[index].pointer != null) gates[index].pointer.gameObject.SetActive(false); // (the count is on the HUD now - ShowNotice)
    }

    void Update()
    {
        if ((readyDelay -= Time.deltaTime) > 0f || currentRoom >= rooms.Count) return;
        int left = 0;
        foreach (var enemy in rooms[currentRoom]) if (enemy != null && !enemy.IsDead) left++;
        if (currentRoom >= gates.Count) { ShowNotice(0); return; }

        var gate = gates[currentRoom];
        // The count is one line at the top centre of the screen while the seal is in view: the plate and arrow beside
        // the seal sat in the middle of the fight.
        ShowNotice(!gate.open && left > 0 && NoticeFullyVisible(gate) ? left : 0);
        if (!gate.open)
        {
            if (left == 0) Open(gate);
            else
            {
                if (gate.pointer != null)
                {
                    float pulse = 0.86f + 0.14f * Mathf.Sin(Time.unscaledTime * 6f);
                    gate.pointer.transform.localScale = Vector3.one * 2f * pulse;
                }
            }
        }
        if (gate.open && Passed(gate.edge))
        {
            currentRoom++;
            ActivateRoom(currentRoom);
            ShowGate(currentRoom);
        }
    }

    bool NoticeFullyVisible(Gate gate)
    {
        var cam = Camera.main;
        if (gate == null || cam == null) return true;
        Vector3 at = cam.WorldToViewportPoint(GateEffectPosition(gate.edge)); // the seal itself (its plate is never drawn now)
        return at.z > 0f && at.x >= 0f && at.x <= 1f && at.y >= 0f && at.y <= 1f;
    }

    void ActivateRoom(int room)
    {
        if (room < 0 || room >= rooms.Count) return;
        StartCoroutine(Arrive(rooms[room]));
        if (player != null && rooms[room].Count > 0)
            DamagePopup.SpawnLabel(player.transform.position + Vector3.up * 1.4f, Loc.T("encounter.new_area"), UiTheme.Gold); // gold, not the gate tint: the tint is red in the red stages and vanished into the trees
    }

    // The next room's monsters are announced before they stand there: a "!" over each spot, and only then the monster.
    // They used to appear the moment the seal was passed, sometimes on top of the cat. (Still switched off they count
    // as alive, so the room's seal can't open meanwhile.)
    System.Collections.IEnumerator Arrive(List<Enemy> foes)
    {
        foreach (var enemy in foes)
        {
            if (enemy == null) continue;
            Vector3 at = enemy.transform.position;
            DamagePopup.SpawnLabel(at + Vector3.up * 1.6f, "!", new Color(1f, 0.55f, 0.35f), 1.4f);
            Vfx.Play("blink", at + Vector3.up * 0.6f, 0.5f);
        }
        yield return new WaitForSeconds(0.9f);
        foreach (var enemy in foes)
        {
            if (enemy == null) continue;
            enemy.gameObject.SetActive(true);
            Vfx.Play("blink", enemy.transform.position + Vector3.up * 0.6f, 0.5f);
        }
    }

    TextMeshProUGUI notice;

    int noticeLeft = -1; // the count the line shows now: its text is written only when this changes (not every frame)

    // The room's count on the HUD, top centre; 0 hides it.
    void ShowNotice(int left)
    {
        if (notice == null)
        {
            if (left <= 0) return;
            var hud = RunManager.Instance != null ? RunManager.Instance.hudText : null;
            if (hud == null || hud.canvas == null) return;
            notice = new GameObject("EncounterNotice", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var rt = notice.rectTransform;
            rt.SetParent(hud.canvas.rootCanvas.transform, false);
            rt.SetAsFirstSibling(); // under the popups
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            rt.sizeDelta = new Vector2(520f, 40f);
            notice.font = hud.font;
            notice.fontSize = GameSettings.ReadingSize;
            notice.color = UISkin.OverlayText;
            notice.alignment = TextAlignmentOptions.Center;
            notice.textWrappingMode = TextWrappingModes.NoWrap;
            notice.raycastTarget = false;
            UiTheme.Backdrop(notice);
        }
        if (left == noticeLeft) return;
        noticeLeft = left;
        notice.gameObject.SetActive(left > 0);
        if (left > 0) notice.text = Loc.F("encounter.enemies_left", left);
    }

    void Open(Gate gate)
    {
        gate.open = true;
        RemoveBarrier(gate.edge);
        Vfx.Play("rupture", GateEffectPosition(gate.edge), 1f, tint: GateEffectColor);
        if (gate.root != null) Destroy(gate.root);
        Sfx.Play("stage_clear", 0.52f, 1.33f);
        OnRoomOpened();
    }

    protected virtual void OnRoomOpened()
    {
        if (player != null)
            DamagePopup.SpawnLabel(player.transform.position + Vector3.up * 1.4f, Loc.T("encounter.area_clear"), UiTheme.Gold);
    }

    // A seal drawn as a wall of real climbing plants (Danaida, Resources/WorldProps/Vines):
    // pieces laid end to end across the whole map, alternately mirrored so the repeat is hidden.
    // `alongX` lays the wall sideways (vertical stages). Returns false when the art is missing.
    protected static bool BuildVineWall(Transform parent, float length, bool alongX, string[] names, int order)
    {
        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (var n in names)
        {
            var s = Resources.Load<Sprite>("WorldProps/Vines/" + n);
            if (s != null) sprites.Add(s);
        }
        if (sprites.Count == 0) return false;

        float piece = sprites[0].bounds.size.y;   // 64 px = 4 units
        float step = piece * 0.85f;               // a little overlap: no gaps between stems
        int count = Mathf.CeilToInt(length / step) + 1;
        float start = -length * 0.5f + piece * 0.5f - piece * 0.1f;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Vine" + i);
            go.transform.SetParent(parent, false);
            float t = start + i * step;
            go.transform.localPosition = alongX ? new Vector3(t, 0f, 0f) : new Vector3(0f, t, 0f);
            if (alongX) go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[i % sprites.Count];
            sr.flipX = i % 2 == 1;
            sr.flipY = (i / 2) % 2 == 1;
            sr.sortingOrder = order;
        }
        return true;
    }

    protected virtual void OnDestroy()
    {
        if (map == null) return;
        foreach (var gate in gates) if (!gate.open) RemoveBarrier(gate.edge);
    }
}
