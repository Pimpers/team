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
        if (gates[index].pointer != null) gates[index].pointer.gameObject.SetActive(true);
    }

    void Update()
    {
        if ((readyDelay -= Time.deltaTime) > 0f || currentRoom >= rooms.Count) return;
        int left = 0;
        foreach (var enemy in rooms[currentRoom]) if (enemy != null && !enemy.IsDead) left++;
        if (currentRoom >= gates.Count) return;

        var gate = gates[currentRoom];
        bool noticeVisible = NoticeFullyVisible(gate);
        if (gate.plate != null) gate.plate.enabled = noticeVisible;
        if (gate.label != null) gate.label.gameObject.SetActive(noticeVisible);
        if (!gate.open)
        {
            if (left == 0) Open(gate);
            else
            {
                if (gate.label != null) gate.label.text = Loc.F("encounter.enemies_left", left);
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
        if (gate == null || gate.plate == null) return true;
        var cam = Camera.main;
        if (cam == null) return true;
        Bounds bounds = gate.plate.bounds;
        Vector3 min = cam.WorldToViewportPoint(bounds.min);
        Vector3 max = cam.WorldToViewportPoint(bounds.max);
        return min.z > 0f && min.x >= 0.02f && max.x <= 0.98f &&
               min.y >= 0.04f && max.y <= 0.96f;
    }

    void ActivateRoom(int room)
    {
        if (room < 0 || room >= rooms.Count) return;
        foreach (var enemy in rooms[room])
        {
            if (enemy == null) continue;
            enemy.gameObject.SetActive(true);
            Vfx.Play("blink", enemy.transform.position + Vector3.up * 0.6f, 0.5f);
        }
        if (player != null && rooms[room].Count > 0)
            DamagePopup.SpawnLabel(player.transform.position + Vector3.up * 1.4f, Loc.T("encounter.new_area"), UiTheme.Gold); // gold, not the gate tint: the tint is red in the red stages and vanished into the trees
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
