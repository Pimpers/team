using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu: Cats Smash > Apply UI Skin
//
// Restyles the game's UI with Kenney's "UI Pack - Pixel Adventure" (Assets/UI):
//   - cuts the needed pieces out of the packed tilesheets as 9-sliced sprites
//   - fills Assets/Resources/UISkin.asset (used by UI that is built at runtime)
//   - walks every scene and re-skins the panels / buttons / banners / texts by name
// The scene builders (LobbySceneBuilder, StageSceneBuilder) call ApplyAndSave instead of
// SaveScene, so rebuilt scenes come out skinned too. Running the menu again is harmless.
public static class UISkinApplier
{
    const string LargeSheet = "Assets/UI/Tilesheets/Large tiles/Thick outline/tilemap_packed.png"; // 13 x 7 of 32px
    const string SmallSheet = "Assets/UI/Tilesheets/Small tiles/Thick outline/tilemap_packed.png"; // 23 x 7 of 16px
    const string SkinPath = "Assets/Resources/UISkin.asset";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/TitleScene.unity", "Assets/Scenes/LobbyScene.unity", "Assets/Scenes/LoadingScene.unity",
        "Assets/Scenes/MainScene.unity", "Assets/Scenes/WorldMapScene.unity", "Assets/Scenes/GameScene.unity",
        "Assets/Scenes/Tutorial.unity", "Assets/Scenes/OptionsScene.unity",
        "Assets/Scenes/Stage1.unity", "Assets/Scenes/Stage2.unity", "Assets/Scenes/Stage3.unity",
        "Assets/Scenes/Stage4.unity", "Assets/Scenes/Stage5.unity",
    };

    // name, sheet, first tile index, tiles wide, 9-slice border (px, all four sides or L/R only)
    struct Piece { public string name; public bool large; public int index, wide; public Vector4 border; }

    static readonly Piece[] Pieces =
    {
        new Piece { name = "ui_panel", large = true, index = 0, wide = 1, border = new Vector4(6, 6, 6, 6) },
        new Piece { name = "ui_slate", large = true, index = 3, wide = 1, border = new Vector4(6, 6, 6, 6) },
        new Piece { name = "ui_wood", large = true, index = 1, wide = 1, border = new Vector4(6, 6, 6, 6) },
        new Piece { name = "ui_card", large = true, index = 18, wide = 1, border = new Vector4(8, 8, 8, 8) },
        new Piece { name = "ui_banner", large = true, index = 43, wide = 3, border = new Vector4(32, 0, 32, 0) },
        new Piece { name = "ui_button_red", large = false, index = 3, wide = 1, border = new Vector4(4, 4, 4, 4) },
        new Piece { name = "ui_button_wood", large = false, index = 11, wide = 1, border = new Vector4(5, 5, 5, 5) },
        new Piece { name = "ui_button_grey", large = false, index = 26, wide = 1, border = new Vector4(4, 4, 4, 4) },
        new Piece { name = "ui_button_cream", large = false, index = 49, wide = 1, border = new Vector4(4, 4, 4, 4) },
        new Piece { name = "ui_knob", large = false, index = 48, wide = 1, border = Vector4.zero },
    };

    [MenuItem("Cats Smash/Apply UI Skin")]
    public static void ApplyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string current = SceneManager.GetActiveScene().path;
        ApplyAll();
        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
    }

    // Unity.exe -batchmode -quit -projectPath <project> -executeMethod UISkinApplier.ApplyBatch
    public static void ApplyBatch()
    {
        try { ApplyAll(); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void ApplyAll()
    {
        Ensure();
        foreach (var path in Scenes)
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ApplyAndSave(scene);
        }
        Debug.Log("[UISkinApplier] UI skin applied to all scenes.");
    }

    // ---------------------------------------------------------------- sprites + skin asset
    // ---- The leafy "Free UI Nature Pack" by Qiuro (Assets/UI/Nature, see License.txt there), the game's
    // current skin. Each piece is one PNG cropped from the pack (button_grey / button_cream recoloured from
    // the wood button). They are drawn twice as fine as the Kenney pieces (64 px art where Kenney had 32):
    // imported at 200 px/unit, one art pixel is exactly 2 canvas units at the usual x4 skin scale, so the
    // frames come out about as thick as before. Their vine edges tile instead of stretching (UISkin.Apply).
    const string NatureDir = "Assets/UI/Nature";
    const string ItchUiDir = "Assets/Resources/ItchUI";
    const float NaturePixelsPerUnit = 200f;

    // file, 9-slice border in source px (left, bottom, right, top - Unity's order)
    static readonly (string file, Vector4 border)[] Nature =
    {
        ("nature_panel", new Vector4(12, 12, 12, 12)),        // cream window, vine frame, leaf corners
        ("nature_wood", new Vector4(16, 14, 14, 16)),         // dark window, big leaf top-left, vines below
        ("nature_slate", new Vector4(5, 5, 5, 5)),            // plain dark window, wood rim
        ("nature_plank", new Vector4(16, 3, 13, 9)),          // vine-wrapped plank: title banners
        ("nature_button_wood", new Vector4(5, 5, 5, 5)),     // (the new wood / green / grey buttons, Assets/UI/Cats; UiCatsImport sets the same border)
        ("nature_button_green", new Vector4(5, 5, 5, 5)),    // main action (계속하기, 재도전, 확인...)
        ("nature_button_grey", new Vector4(5, 5, 5, 5)),
        ("nature_button_cream", new Vector4(6, 5, 6, 5)),
        ("nature_knob", Vector4.zero),                        // a leaf for slider handles
    };

    static Sprite NatureSprite(string file)
    {
        string path = $"{NatureDir}/{file}.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new System.Exception($"[UISkinApplier] {path} not found.");
        Vector4 border = Vector4.zero;
        foreach (var n in Nature) if (n.file == file) border = n.border;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = NaturePixelsPerUnit;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.spriteBorder = border;
        ti.SaveAndReimport();
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) throw new System.Exception($"[UISkinApplier] {path} did not import as a sprite.");
        return s;
    }

    static Sprite ItchSprite(string file, Vector4 border, float pixelsPerUnit = 100f)
    {
        string path = $"{ItchUiDir}/{file}.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new System.Exception($"[UISkinApplier] {path} not found.");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = pixelsPerUnit;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.spriteBorder = border;
        ti.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.Exception($"[UISkinApplier] {path} did not import as a sprite.");
        return sprite;
    }

    public static UISkin Ensure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<UISkin>();
            AssetDatabase.CreateAsset(skin, SkinPath);
        }
        skin.panel = NatureSprite("nature_panel");
        ItchSprite("fantasy_panel", new Vector4(10f, 10f, 10f, 20f), 400f);
        ItchSprite("cryo_panel", new Vector4(7f, 7f, 7f, 7f), 200f);
        ItchSprite("cat_portrait", Vector4.zero);
        ItchSprite("keycap", new Vector4(5f, 4f, 5f, 4f));
        foreach (string icon in new[] { "claw", "wind", "fur", "hunt", "treat" })
            ItchSprite("SetIcons/" + icon, Vector4.zero);
        foreach (string piece in new[] { "top", "bottom", "left", "right", "top_left", "top_right", "bottom_left", "bottom_right" })
            ItchSprite("MinimapFrame/" + piece, Vector4.zero, 100f);
        skin.slate = NatureSprite("nature_slate");
        skin.woodPanel = NatureSprite("nature_wood");
        skin.card = skin.panel;
        skin.banner = NatureSprite("nature_button_wood");
        skin.buttonRed = NatureSprite("nature_button_green"); // same pill as the wood buttons, green = main action
        skin.buttonWood = NatureSprite("nature_button_wood");
        skin.buttonGrey = NatureSprite("nature_button_grey");
        skin.buttonCream = NatureSprite("nature_button_cream");
        skin.knob = NatureSprite("nature_knob");
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        return skin;
    }

    // The previous Kenney skin (Assets/UI/Tilesheets), kept so it can be switched back: call this instead.
    public static UISkin EnsureKenney()
    {
        Slice(LargeSheet, 32, 13, true);
        Slice(SmallSheet, 16, 23, false);
        var skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
        skin.panel = Load("ui_panel");
        skin.slate = Load("ui_slate");
        skin.woodPanel = Load("ui_wood");
        skin.card = Load("ui_card");
        skin.banner = Load("ui_banner");
        skin.buttonRed = Load("ui_button_red");
        skin.buttonWood = Load("ui_button_wood");
        skin.buttonGrey = Load("ui_button_grey");
        skin.buttonCream = Load("ui_button_cream");
        skin.knob = Load("ui_knob");
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        return skin;
    }

    static Sprite Load(string name)
    {
        foreach (var piece in Pieces)
            if (piece.name == name)
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(piece.large ? LargeSheet : SmallSheet))
                    if (o is Sprite s && s.name == name) return s;
        throw new System.Exception($"[UISkinApplier] Sprite '{name}' was not created.");
    }

    // Cuts this sheet's pieces as named sprites (point filter, no compression, 100 PPU so one
    // sprite pixel = one canvas unit before Image.pixelsPerUnitMultiplier scales it up).
    static void Slice(string path, int cell, int cols, bool large)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new System.Exception($"[UISkinApplier] {path} not found.");
        bool settingsChanged = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Multiple ||
                               ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed ||
                               ti.mipmapEnabled || ti.spritePixelsPerUnit != 100f;
        if (settingsChanged)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = 100f;
            ti.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();
        var existing = new Dictionary<string, SpriteRect>();
        foreach (var sr in dp.GetSpriteRects()) existing[sr.name] = sr;

        var list = new List<SpriteRect>();
        bool same = true;
        foreach (var p in Pieces)
        {
            if (p.large != large) continue;
            int col = p.index % cols, row = p.index / cols;
            var rect = new Rect(col * cell, tex.height - (row + 1) * cell, cell * p.wide, cell);
            if (!existing.TryGetValue(p.name, out var sr))
            {
                sr = new SpriteRect { name = p.name, spriteID = GUID.Generate() };
                same = false;
            }
            if (sr.rect != rect || sr.border != p.border) same = false;
            sr.rect = rect;
            sr.border = p.border;
            sr.alignment = SpriteAlignment.Center;
            sr.pivot = new Vector2(0.5f, 0.5f);
            list.Add(sr);
        }
        if (same && existing.Count == list.Count) return;

        dp.SetSpriteRects(list.ToArray());
        var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (ids != null)
        {
            var pairs = new List<SpriteNameFileIdPair>();
            foreach (var sr in list) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
            ids.SetNameFileIdPairs(pairs);
        }
        dp.Apply();
        ti.SaveAndReimport();
    }

    // ---------------------------------------------------------------- scenes
    public static void ApplyAndSave(Scene scene)
    {
        Apply(scene);
        AudioAssets.ApplyToScene(scene); // scene music + button click sounds
        var missing = new List<string>();
        SceneTextLinker.Report(SceneTextLinker.Link(scene, missing), missing); // Korean texts -> text table keys
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void Apply(Scene scene)
    {
        var skin = Ensure();
        LayoutStageUi(scene, skin);
        // Scene-specific rules first, then the shared ones (panels / buttons used in many scenes).
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var img = t.GetComponent<Image>();
                string n = t.name, parent = t.parent != null ? t.parent.name : "";

                switch (n)
                {
                    // Title ribbons (lobby "용병을 고르세요", village "항구").
                    case "TitleBanner":
                        if (img != null)
                        {
                            UISkin.ApplyBanner(img);
                            RecolorChildren(t, UISkin.BannerText);
                            var titleText = t.Find("TitleText");
                            if (titleText != null) FitToRibbonFace((RectTransform)titleText, img);
                        }
                        break;
                    case "HintBanner":
                        UISkin.Apply(img, skin.woodPanel, 2f, new Color(1f, 1f, 1f, 0.94f)); // warm wood, not cold slate
                        break;

                    // Popup frames: parchment, the old dark inner plate becomes transparent.
                    case "NamingPanel":
                    case "CardDraftPanel":
                    case "ResultPanel":
                        UISkin.Apply(img, skin.panel, UISkin.LargeScale);
                        InkTexts(t);
                        break;
                    case "Frame" when parent == "BlacksmithPanel" || parent == "OptionsPanel":
                        UISkin.Apply(img, skin.panel, UISkin.LargeScale);
                        InkTexts(t);
                        break;
                    case "Inner" when parent == "NamingPanel" || parent == "Frame":
                        if (img != null) { img.sprite = null; img.color = Color.clear; }
                        break;
                    case "Divider":
                        if (img != null) img.color = new Color(0.55f, 0.36f, 0.2f, 0.55f);
                        break;
                    case "Row0": case "Row1": case "Row2":
                        UISkin.Apply(img, skin.buttonCream, 3f, new Color(0.93f, 0.86f, 0.74f, 1f));
                        break;

                    // Cards in the post-stage draft.
                    case "Card1Button": case "Card2Button": case "Card3Button":
                        UISkin.Apply(img, skin.card, 3f);
                        RecolorChildren(t, UISkin.Ink);
                        break;

                    // Buttons: red = main action, wood = back / close / secondary.
                    case "ConfirmButton":
                    case "BuyButton":
                    case "RetryButton":
                        UISkin.Apply(img, skin.buttonRed, UISkin.SmallScale);
                        RecolorChildren(t, Color.white);
                        // The pixel font's "!" is a wide monospace glyph that left a gap: "출정하기 !".
                        foreach (var label in t.GetComponentsInChildren<TMP_Text>(true))
                            if (label.text == "출정하기!") label.text = "출정하기";
                        // The result button goes back to the village, not straight into a new run.
                        if (n == "RetryButton")
                            foreach (var label in t.GetComponentsInChildren<TMP_Text>(true)) label.text = "마을로 돌아가기";
                        break;
                    case "QuitButton": // the title screen's 게임 종료
                    case "CancelButton":
                    case "CloseButton":
                    case "BackButton":
                    case "AttackButton": case "SpeedButton": case "HPButton": // hidden legacy upgrade buttons
                        UISkin.Apply(img, skin.buttonWood, UISkin.SmallScale);
                        RecolorChildren(t, Color.white);
                        break;

                    // Name plates under the village icons, profile name, world-map hint.
                    case "LabelPlate":
                        UISkin.Apply(img, skin.buttonWood, 3f);
                        break;
                    case "NamePill":
                    case "HintPlate":
                        UISkin.Apply(img, skin.buttonWood, UISkin.SmallScale);
                        break;

                    case "NameInput":
                        UISkin.Apply(img, skin.buttonCream, UISkin.SmallScale);
                        break;
                    case "RoleBadge": // tinted with the class colour at runtime
                        UISkin.Apply(img, skin.buttonCream, 3f, img != null ? img.color : (Color?)null);
                        RecolorChildren(t, Color.white); // white on the class colour, not ink
                        break;

                    // Volume slider: Unity's default slider uses a half-height track, too thin
                    // for the pixel frame - make the track full height and the knob bigger.
                    case "Background" when parent == "VolumeSlider":
                        FullHeight(t);
                        UISkin.Apply(img, skin.buttonGrey, 2f);
                        break;
                    case "Fill Area" when parent == "VolumeSlider":
                        FullHeight(t);
                        break;
                    case "Fill" when parent == "Fill Area":
                        UISkin.Apply(img, skin.buttonRed, 2f);
                        break;
                    case "Handle" when parent == "Handle Slide Area":
                        UISkin.Apply(img, skin.knob, 3f);
                        if (img != null)
                        {
                            img.preserveAspect = true;
                            img.rectTransform.sizeDelta = new Vector2(48f, 12f);
                        }
                        break;
                }
                if (img != null) EditorUtility.SetDirty(img);
            }
        }
    }

    // ---------------------------------------------------------------- stage popups
    // The stage UI came from the old GameScene prototype: a Constant-Pixel-Size canvas with
    // a 560x260 card panel and 20pt labels, which looked tiny and didn't fit the pixel frames.
    // Scale it like the other scenes (1920x1080 reference) and lay the card draft / result
    // panels out at a proper size.
    static void LayoutStageUi(Scene scene, UISkin skin)
    {
        GameObject cardPanel = null, resultPanel = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CardDraftPanel") cardPanel = t.gameObject;
                if (t.name == "ResultPanel") resultPanel = t.gameObject;
            }
        if (cardPanel == null && resultPanel == null) return;

        var canvas = (cardPanel ?? resultPanel).GetComponentInParent<Canvas>(true).rootCanvas;
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        if (cardPanel != null) LayoutCards(cardPanel.transform, skin);
        if (resultPanel != null) LayoutResult(resultPanel.transform, skin);
    }

    static void LayoutCards(Transform panel, UISkin skin)
    {
        Place(panel, Vector2.zero, new Vector2(1180f, 680f));
        TMP_Text sample = null;

        float[] xs = { -370f, 0f, 370f };
        for (int i = 0; i < 3; i++)
        {
            var card = panel.Find($"Card{i + 1}Button");
            if (card == null) continue;
            Place(card, new Vector2(xs[i], -50f), new Vector2(320f, 480f)); // grade art is 2:3 (CardDraftUI dresses it at runtime)
            var label = card.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;
            sample = label;
            // Text inside the card's cream area (the frame is 8px x 3 = 24 units wide).
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(40f, 40f);
            lrt.offsetMax = new Vector2(-40f, -40f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = 26f;
            label.fontSizeMax = 44f;
            label.fontSize = 44f;
            label.color = UISkin.Ink;
            EditorUtility.SetDirty(label);
        }

        // Ribbon title across the top of the panel.
        var title = Banner(panel, "CardTitle", "카드를 한 장 고르세요", new Vector2(0f, 300f), new Vector2(760f, 112f), sample);
        EditorUtility.SetDirty(title);
    }

    static void LayoutResult(Transform panel, UISkin skin)
    {
        // Title ("의뢰 완수!" / "쓰러지고 말았다...") on a ribbon at the top, the reward text
        // filling the middle, the button at the bottom - no empty band in the centre.
        Place(panel, Vector2.zero, new Vector2(920f, 560f));
        TMP_Text titleText = null, goldText = null;
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "TitleText") titleText = text;
            if (text.name == "GoldText") goldText = text;
        }

        var ribbon = Banner(panel, "ResultBanner", "", new Vector2(0f, 205f), new Vector2(740f, 120f), titleText);
        ribbon.transform.Find("Label").gameObject.SetActive(false); // the real title text sits on it
        if (titleText != null)
        {
            titleText.transform.SetParent(ribbon.transform, false);
            var trt = titleText.rectTransform;
            FitToRibbonFace(trt, ribbon);
            titleText.fontSize = UISkin.FontTitle;
            titleText.color = UISkin.BannerText;
            EditorUtility.SetDirty(titleText);
        }
        if (goldText != null)
        {
            Place(goldText.transform, new Vector2(0f, 5f), new Vector2(820f, 200f));
            goldText.fontSize = UISkin.FontTitle;
            goldText.lineSpacing = 20f;
            goldText.alignment = TextAlignmentOptions.Center;
            goldText.textWrappingMode = TextWrappingModes.Normal;
            goldText.color = UISkin.Ink;
            EditorUtility.SetDirty(goldText);
        }
        var retry = panel.Find("RetryButton");
        if (retry != null)
        {
            Place(retry, new Vector2(0f, -180f), new Vector2(380f, 110f));
            var label = retry.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = UISkin.FontButton;
                label.alignment = TextAlignmentOptions.Center;
                label.margin = new Vector4(0f, 0f, 0f, 6f); // sits a hair above the button's shadow row
                EditorUtility.SetDirty(label);
            }
        }
    }

    static void Place(Transform t, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        EditorUtility.SetDirty(rt);
    }

    // A red ribbon with a centred label; reused (not duplicated) when run again.
    static Image Banner(Transform parent, string name, string text, Vector2 pos, Vector2 size, TMP_Text fontFrom)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            t = go.transform;
            t.SetParent(parent, false);
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(t, false);
        }
        Place(t, pos, size);
        var img = t.GetComponent<Image>();
        img.raycastTarget = false;
        UISkin.ApplyBanner(img);

        var label = t.Find("Label").GetComponent<TextMeshProUGUI>();
        var lrt = label.rectTransform;
        FitToRibbonFace(lrt, img);
        label.text = text;
        if (fontFrom != null) label.font = fontFrom.font;
        label.fontSize = UISkin.FontTitle;
        label.color = UISkin.BannerText;
        label.raycastTarget = false;
        EditorUtility.SetDirty(label);
        return img;
    }

    // Centres a label on the ribbon's flat face (see UISkin.FitToRibbon).
    static void FitToRibbonFace(RectTransform label, Image ribbon)
    {
        UISkin.FitToRibbon(label, ribbon);
        var text = label.GetComponent<TMP_Text>();
        if (text != null) EditorUtility.SetDirty(text);
        EditorUtility.SetDirty(label);
    }

    static void FullHeight(Transform t)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
        rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
        rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
        rt.offsetMax = new Vector2(rt.offsetMax.x, 0f);
        EditorUtility.SetDirty(rt);
    }

    static void RecolorChildren(Transform t, Color color)
    {
        foreach (var text in t.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = color;
            EditorUtility.SetDirty(text);
        }
    }

    // Light-on-dark texts inside a parchment panel -> dark ink. Buttons keep white labels
    // (they're recoloured by their own rule), the input keeps its own dark text.
    static void InkTexts(Transform panel)
    {
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Button>(true) != null || text.GetComponentInParent<TMP_InputField>(true) != null) continue;
            string parentName = text.transform.parent != null ? text.transform.parent.name : "";
            if (parentName.EndsWith("Banner") || parentName == "CardTitle") continue; // light text on a ribbon
            string n = text.name;
            Color c = UISkin.Ink;
            if (n == "ClassText" || n == "Title" || n == "TitleText") c = UISkin.InkTitle;
            else if (n == "SpeciesText" || n == "NameHint") c = UISkin.InkSoft;
            else if (n == "Gold") c = UISkin.InkGold;
            text.color = c;
            EditorUtility.SetDirty(text);
        }
    }
}
