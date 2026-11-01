using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Cats Smash > Apply Concept Polish
//
// Fixes found in the concept review that live in scenes the builders don't regenerate from
// scratch (so a full rebuild isn't needed - and none of it wipes manual edits):
//   - Cat scale: the title / lobby / loading cats were drawn 2-3x chunkier than the painted
//     backgrounds around them (the stage cat is handled in StageSceneBuilder). Shrunk, feet kept
//     on the ground, lobby zoom framing scaled to match.
//   - The lobby job plates (dark + gold) become the same warm wood as the rest of the UI.
// Every step is idempotent: running it again changes nothing.
public static class ConceptPolish
{
    const string TitleScene = "Assets/Scenes/TitleScene.unity";
    const string LobbyScene = "Assets/Scenes/LobbyScene.unity";
    const string LoadingScene = "Assets/Scenes/LoadingScene.unity";
    const string NameplatePath = "Assets/UI/LobbyNameplate.png";
    const string StartPrompt = "아무 곳이나 클릭하여 시작"; // it said "touch the screen" - this is a mouse and keyboard game

    public const float TitleCatScale = 10f;   // was 15
    public const float LobbyCatScale = 9f;    // was 12
    public const float LoadingCatScale = 10f; // was 12
    static readonly Color Cream = new Color32(0xff, 0xf1, 0xd2, 0xff);

    [MenuItem("Cats Smash/Apply Concept Polish")]
    public static void ApplyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        ApplyAll();
        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
    }

    // Unity.exe -batchmode -quit -projectPath <project> -executeMethod ConceptPolish.ApplyBatch
    public static void ApplyBatch()
    {
        try { ApplyAll(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void ApplyAll()
    {
        WoodNameplate();
        PatchTitle();
        PatchLobby();
        PatchLoading();
        Debug.Log("[ConceptPolish] Applied.");
    }

    // ---------------------------------------------------------------- nameplate
    // 12x8, 1px dark-brown outline with clipped corners, 1px light-orange rim, wood fill:
    // the same colours as the Kenney wood buttons. Imported as a 9-slice (border 3px).
    static void WoodNameplate()
    {
        const int w = 12, h = 8;
        var outline = new Color32(0x5e, 0x3a, 0x1e, 0xff);
        var rim = new Color32(0xd9, 0xa3, 0x5a, 0xff);
        var fill = new Color32(0x9a, 0x62, 0x34, 0xff);
        var clear = new Color32(0, 0, 0, 0);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
                px[y * w + x] = corner ? clear : d == 0 ? outline : d == 1 ? rim : fill;
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        if (!File.Exists(NameplatePath) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(NameplatePath), png))
        {
            File.WriteAllBytes(NameplatePath, png);
            AssetDatabase.ImportAsset(NameplatePath);
        }
    }

    // ---------------------------------------------------------------- scenes
    static void PatchTitle()
    {
        if (!File.Exists(TitleScene)) return;
        var scene = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Single);
        var player = FindByName(scene, "PLAYER");
        if (player != null)
        {
            var sr = player.GetComponent<SpriteRenderer>();
            float old = player.transform.localScale.x;
            if (sr != null && sr.sprite != null && !Mathf.Approximately(old, TitleCatScale))
            {
                // Keep the feet where they were (the sprite is centred or bottom-pivoted: bounds know).
                float feetY = player.transform.position.y + sr.sprite.bounds.min.y * old;
                player.transform.localScale = new Vector3(TitleCatScale, TitleCatScale, 1f);
                var p = player.transform.position;
                p.y = feetY - sr.sprite.bounds.min.y * TitleCatScale;
                player.transform.position = p;
            }
        }
        var startText = FindByName(scene, "TouchToStartText");
        var tmp = startText != null ? startText.GetComponent<TMP_Text>() : null;
        if (tmp != null && tmp.text != StartPrompt) { tmp.text = StartPrompt; EditorUtility.SetDirty(tmp); }
        EnsureTitleQuitButton(scene, tmp);
        Save(scene);
    }

    // A "게임 종료" button in the bottom-right corner of the title screen. It is a UI Button, so clicking it
    // never reaches the full-screen "click anywhere to start" catcher underneath.
    static void EnsureTitleQuitButton(UnityEngine.SceneManagement.Scene scene, TMP_Text fontFrom)
    {
        if (FindByName(scene, "QuitButton") != null) return;
        var canvas = FindByName(scene, "Canvas");
        if (canvas == null) return;
        var skin = UISkinApplier.Ensure();
        var go = new GameObject("QuitButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(QuitButton));
        go.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-30f, 30f);
        rt.sizeDelta = new Vector2(300f, 84f);
        UISkin.Apply(go.GetComponent<UnityEngine.UI.Image>(), skin.buttonWood, UISkin.SmallScale);
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(rt, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var label = labelGo.GetComponent<TextMeshProUGUI>();
        if (fontFrom != null) label.font = fontFrom.font;
        label.text = "게임 종료";
        label.fontSize = 36f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        go.transform.SetAsLastSibling(); // above the click catcher
    }

    static void PatchLobby()
    {
        if (!File.Exists(LobbyScene)) return;
        var scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        float zoomRatio = 1f;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var display in root.GetComponentsInChildren<LobbyCharacterDisplay>(true))
            {
                var visual = display.visual;
                if (visual == null) continue;
                float old = visual.localScale.x;
                if (!Mathf.Approximately(old, LobbyCatScale))
                {
                    zoomRatio = LobbyCatScale / old;
                    visual.localScale = new Vector3(LobbyCatScale, LobbyCatScale, 1f);
                    var sr = visual.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null)
                    {
                        var b = sr.sprite.bounds;
                        visual.localPosition = new Vector3(-b.center.x * LobbyCatScale, -b.min.y * LobbyCatScale, 0f);
                    }
                    // The plate floats a little above the (now lower) head.
                    var idle = display.character != null && display.character.idleFrames.Length > 0 ? display.character.idleFrames[0] : null;
                    if (display.tagRoot != null && idle != null)
                        display.tagRoot.transform.localPosition = new Vector3(0f, idle.rect.height / idle.pixelsPerUnit * LobbyCatScale + 0.45f, 0f);
                    EditorUtility.SetDirty(visual);
                }
                if (display.jobText != null) { display.jobText.color = Cream; EditorUtility.SetDirty(display.jobText); }
            }
            // Zoomed-in framing follows the cat's size.
            foreach (var lc in root.GetComponentsInChildren<LobbyController>(true))
            {
                if (Mathf.Approximately(zoomRatio, 1f)) continue;
                lc.zoomSize *= zoomRatio;
                lc.zoomFocusOffset *= zoomRatio;
                EditorUtility.SetDirty(lc);
            }
        }
        Save(scene);
    }

    static void PatchLoading()
    {
        if (!File.Exists(LoadingScene)) return;
        var scene = EditorSceneManager.OpenScene(LoadingScene, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var screen in root.GetComponentsInChildren<LoadingScreen>(true))
            {
                var visual = screen.visual;
                if (visual == null || Mathf.Approximately(visual.transform.localScale.x, LoadingCatScale)) continue;
                visual.transform.localScale = new Vector3(LoadingCatScale, LoadingCatScale, 1f);
                EditorUtility.SetDirty(visual.transform);
            }
        Save(scene);
    }

    static void Save(UnityEngine.SceneManagement.Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static GameObject FindByName(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}
