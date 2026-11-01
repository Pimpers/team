using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Menu: Cats Smash > Build Lobby + Stages
//
// 1. Creates the Korean dynamic TMP font (KoreanFont) and the two playable characters
//    (Assets/Characters/MeowKnight.asset, Meowolas.asset).
// 2. Builds LobbyScene.unity (character select) from TitleScene: Lobby.png background,
//    both cats wandering the tavern floor with name tags; clicking one zooms in and opens
//    a naming panel (LobbyController).
// 3. Rebuilds Stage1-3 (StageSceneBuilder) and switches TitleScene's text to Korean.
public static class LobbySceneBuilder
{
    const string LobbyScene = "Assets/Scenes/LobbyScene.unity";
    const string TitleScene = "Assets/Scenes/TitleScene.unity";
    const string LobbyImage = "Assets/Lobby.png";
    const float FloorY = -3.1f;          // tavern floor line in Lobby.png (PPU 100, centered)
    const float CharacterScale = 9f; // was 12: the cats were far chunkier than the painted backgrounds (see ConceptPolish)
    const float LoadingCatScale = 10f; // was 12

    static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
    static readonly Color Parchment = new Color(0.93f, 0.88f, 0.8f);
    static readonly Color Tan = new Color(0.85f, 0.75f, 0.6f);
    static readonly Color Crimson = new Color(0.62f, 0.16f, 0.22f, 1f);

    [MenuItem("Cats Smash/Build Lobby + Stages")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Build Lobby + Stages",
                "LobbyScene and Stage1/2/3 will be rebuilt. Manual edits in those scenes will be lost. Continue?",
                "Rebuild", "Cancel"))
            return;
        Run();
        EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
    }

    // Unity.exe -batchmode -quit -projectPath <project> -executeMethod LobbySceneBuilder.BuildBatch
    public static void BuildBatch()
    {
        try
        {
            Run();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void Run()
    {
        KoreanFont.Ensure();
        CharacterAssets.Ensure();
        BuildLobby();
        BuildLoadingScene();
        BuildMainScene();
        BuildWorldMapScene();
        StageSceneBuilder.Build(false);
        LocalizeTitle();
        Debug.Log("[LobbySceneBuilder] Built LobbyScene + characters + Korean UI.");
    }

    static void LocalizeTitle()
    {
        if (!File.Exists(TitleScene)) return;
        Scene scene = EditorSceneManager.OpenScene(TitleScene, OpenSceneMode.Single);
        KoreanFont.ApplyToScene(scene);
        SetSceneMusic(scene, TitleMusic);
        EditorSceneManager.MarkSceneDirty(scene);
        UISkinApplier.ApplyAndSave(scene); // re-skins the UI (Kenney Pixel Adventure) and saves
    }

    // ---------------------------------------------------------------- music
    const string TitleMusic = "Assets/music/alex-morgan-fantasy-adventure-quest-537478.mp3";
    const string LoungeMusic = "Assets/music/kaazoom-the-jolly-tankard-medieval-tavern-song-363816.mp3";

    // Puts a looping "Music" object (SceneMusic) in the scene, replacing any old one.
    // It isn't carried over to other scenes, so the music stops when the next scene loads.
    static void SetSceneMusic(Scene scene, string clipPath)
    {
        var old = Find(scene, "Music");
        if (old != null) Object.DestroyImmediate(old);

        var importer = (AudioImporter)AssetImporter.GetAtPath(clipPath);
        if (importer == null) throw new System.Exception($"{clipPath} not found");
        var settings = importer.defaultSampleSettings;
        if (settings.loadType != AudioClipLoadType.Streaming)
        {
            settings.loadType = AudioClipLoadType.Streaming; // long BGM: stream instead of loading it all
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        var go = new GameObject("Music");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AudioSource>();
        var music = go.AddComponent<SceneMusic>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
    }

    // ---------------------------------------------------------------- lobby scene
    static void BuildLobby()
    {
        PrepareLobbyTexture();
        EnsureNameplateSprite();

        if (File.Exists(LobbyScene)) AssetDatabase.DeleteAsset(LobbyScene);
        if (!AssetDatabase.CopyAsset(TitleScene, LobbyScene))
            throw new System.Exception($"Could not copy {TitleScene} to {LobbyScene}");
        Scene scene = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);

        foreach (var n in new[] { "ClickCatcher", "TouchToStartText", "Background", "GameTitleImage", "PLAYER", "Music" })
        {
            var go = Find(scene, n);
            if (go != null) Object.DestroyImmediate(go);
        }

        var chars = CharacterAssets.Load();
        SetSceneMusic(scene, LoungeMusic);   // tavern song while picking a mercenary
        var font = KoreanFont.Load();

        // Camera framing the whole 1672x941 picture (PPU 100 -> 16.72 x 9.41 units).
        var cam = Find(scene, "Main Camera").GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4.705f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x12, 0x0c, 0x10, 0xff);
        EditorUtility.SetDirty(cam);

        var bg = new GameObject("LobbyBackground");
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LobbyImage);
        bgSr.sortingOrder = -100;

        var knight = MakeCat("MeowKnight", 0, chars[0], new Vector3(-3.5f, FloorY, 0f), font);
        var archer = MakeCat("Meowolas", 1, chars[1], new Vector3(3.5f, FloorY, 0f), font);

        // ---- UI
        var canvasGo = Find(scene, "Canvas");
        foreach (Transform child in canvasGo.transform.Cast<Transform>().ToList())
            Object.DestroyImmediate(child.gameObject);
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        // Overview: title + hint (hidden while zoomed in on a cat)
        var overview = new GameObject("Overview", typeof(RectTransform));
        overview.transform.SetParent(canvas, false);
        var banner = Panel(overview.transform, "TitleBanner", new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, 430f), new Vector2(900f, 120f));
        Text(banner.transform, "TitleText", "용병을 고르세요", 68, Gold, Vector2.zero, new Vector2(880f, 110f), FontStyles.Bold, font);
        var hintBg = Panel(overview.transform, "HintBanner", new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, 345f), new Vector2(760f, 60f));
        Text(hintBg.transform, "HintText", "돌아다니는 고양이를 클릭해 보세요", 32, Parchment, Vector2.zero, new Vector2(740f, 56f), FontStyles.Normal, font);

        // Back to title (always visible)
        var backImg = Panel(canvas, "BackButton", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, new Vector2(200f, 70f));
        var backRt = backImg.rectTransform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
        backRt.anchoredPosition = new Vector2(130f, -60f);
        var backBtn = MakeButton(backImg);
        Text(backImg.transform, "Label", "< 뒤로", 34, Color.white, Vector2.zero, new Vector2(190f, 64f), FontStyles.Bold, font);

        // Character panel (right side; the zoomed camera keeps the cat on the left).
        // Top: job, species, role-group badge and description. Bottom: naming, as before.
        var frame = Panel(canvas, "NamingPanel", new Color(0.55f, 0.4f, 0.2f, 1f), new Vector2(470f, 0f), new Vector2(772f, 792f));
        var panel = Panel(frame.transform, "Inner", new Color(0.07f, 0.05f, 0.07f, 0.94f), Vector2.zero, new Vector2(760f, 780f));
        var classText = Text(panel.transform, "ClassText", chars[0].tagName, 66, Gold, new Vector2(0f, 312f), new Vector2(700f, 90f), FontStyles.Bold, font);
        var speciesText = Text(panel.transform, "SpeciesText", chars[0].displayName, 26, new Color(0.7f, 0.65f, 0.65f), new Vector2(0f, 255f), new Vector2(700f, 40f), FontStyles.Normal, font);
        var roleBadge = Panel(panel.transform, "RoleBadge", chars[0].roleColor, new Vector2(0f, 198f), new Vector2(380f, 56f));
        var roleText = Text(roleBadge.transform, "RoleText", "", 28, Color.white, Vector2.zero, new Vector2(370f, 52f), FontStyles.Bold, font);
        var descText = Text(panel.transform, "DescriptionText", "", 27, Parchment, new Vector2(0f, 108f), new Vector2(680f, 140f), FontStyles.Normal, font);
        descText.lineSpacing = 12f;
        Panel(panel.transform, "Divider", new Color(0.55f, 0.4f, 0.2f, 0.6f), new Vector2(0f, 40f), new Vector2(660f, 3f));

        Text(panel.transform, "NameLabel", "이 고양이의 이름을 지어주세요", 34, Parchment, new Vector2(0f, -10f), new Vector2(700f, 56f), FontStyles.Normal, font);
        var input = MakeInputField(panel.transform, font, new Vector2(0f, -95f), new Vector2(540f, 96f));
        var nameHint = Text(panel.transform, "NameHint", $"최대 {CharacterSelection.MaxNameLength}글자", 24, new Color(0.65f, 0.6f, 0.6f), new Vector2(0f, -168f), new Vector2(700f, 40f), FontStyles.Normal, font);

        var cancelImg = Panel(panel.transform, "CancelButton", new Color(0.25f, 0.22f, 0.25f, 1f), new Vector2(-175f, -285f), new Vector2(290f, 100f));
        var cancelBtn = MakeButton(cancelImg);
        Text(cancelImg.transform, "Label", "다시 고르기", 36, Color.white, Vector2.zero, new Vector2(280f, 90f), FontStyles.Bold, font);
        var confirmImg = Panel(panel.transform, "ConfirmButton", Crimson, new Vector2(175f, -285f), new Vector2(290f, 100f));
        var confirmBtn = MakeButton(confirmImg);
        Text(confirmImg.transform, "Label", "출정하기", 40, Color.white, Vector2.zero, new Vector2(280f, 90f), FontStyles.Bold, font);

        var controllerGo = new GameObject("LobbyController");
        var lc = controllerGo.AddComponent<LobbyController>();
        lc.displays = new[] { knight, archer };
        lc.overviewGroup = overview;
        lc.namingPanel = frame.gameObject;
        lc.classText = classText;
        lc.speciesText = speciesText;
        lc.roleBadge = roleBadge;
        lc.roleText = roleText;
        lc.descriptionText = descText;
        lc.nameInput = input;
        lc.nameHint = nameHint;
        lc.confirmButton = confirmBtn;
        lc.cancelButton = cancelBtn;
        lc.backButton = backBtn;
        frame.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        UISkinApplier.ApplyAndSave(scene); // re-skins the UI (Kenney Pixel Adventure) and saves
    }

    // ---------------------------------------------------------------- loading scene
    const string LoadingScene = "Assets/Scenes/LoadingScene.unity";

    // ---------------------------------------------------------------- main scene (harbor village)
    const string MainScene = "Assets/Scenes/MainScene.unity";
    const string HarborImage = "Assets/Backgrounds/HarborVillage.png";

    // Harbor village picture filling the screen: the PixelLab pixel-art version (264x148 art pixels,
    // _art_source/HarborVillage_pixellab_264x148.png) stored blown up 8x with hard edges (2112x1184).
    // PPU 120 -> 17.6 x 9.87 units (the same size as the old 1056x590 painting at PPU 60), framed by an
    // orthographic camera of size 4.9. On screen it is then slightly *shrunk* (about 0.9x at 1080p) with
    // bilinear filtering, which keeps every art pixel the same width - an earlier 352x196 version blown
    // up by a non-integer ~5.5x came out with uneven pixels. (Old painting: _art_backup/harbor_original.)
    static void BuildMainScene()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(HarborImage);
        if (ti == null) throw new System.Exception($"{HarborImage} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 120f;
        ti.filterMode = FilterMode.Bilinear;
        ti.maxTextureSize = 4096;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();

        if (File.Exists(MainScene)) AssetDatabase.DeleteAsset(MainScene);
        if (!AssetDatabase.CopyAsset(TitleScene, MainScene))
            throw new System.Exception($"Could not copy {TitleScene} to {MainScene}");
        Scene scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);

        foreach (var n in new[] { "ClickCatcher", "TouchToStartText", "Background", "GameTitleImage", "PLAYER", "Music" })
        {
            var go = Find(scene, n);
            if (go != null) Object.DestroyImmediate(go);
        }

        var cam = Find(scene, "Main Camera").GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4.9f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x1c, 0x4a, 0x7a, 0xff); // sea blue, in case of wider screens
        EditorUtility.SetDirty(cam);

        var bg = new GameObject("HarborBackground");
        var sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HarborImage);
        sr.sortingOrder = -100;

        var chars = CharacterAssets.Load();
        var font = KoreanFont.Load();

        // The picked mercenary idling in the village square, left of the campfire.
        // Scale 5 keeps the cat readable next to the villagers in the picture.
        const float catScale = 5f;
        var cat = new GameObject("Mercenary");
        cat.transform.position = new Vector3(1.55f, -0.45f, 0f);
        var catVisual = new GameObject("Visual");
        catVisual.transform.SetParent(cat.transform, false);
        catVisual.transform.localScale = new Vector3(catScale, catScale, 1f);
        var catSr = catVisual.AddComponent<SpriteRenderer>();
        catSr.sprite = chars[0].idleFrames[0];
        catSr.sortingOrder = 5;
        Bounds cb = catSr.sprite.bounds;
        catVisual.transform.localPosition = new Vector3(-cb.center.x * catScale, -cb.min.y * catScale, 0f);

        var tagGo = new GameObject("NameTag");
        tagGo.transform.SetParent(cat.transform, false);
        tagGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        var tag = tagGo.AddComponent<TextMeshPro>();
        tag.font = font;
        tag.fontSize = 2.4f;
        tag.fontStyle = FontStyles.Bold;
        tag.alignment = TextAlignmentOptions.Center;
        tag.color = Color.white;
        tag.outlineWidth = 0.25f;
        tag.outlineColor = new Color32(20, 12, 16, 255);
        tag.textWrappingMode = TextWrappingModes.NoWrap;
        tag.sortingOrder = 20;
        tag.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
        tag.text = "이름";

        // ---- UI
        var canvasGo = Find(scene, "Canvas");
        foreach (Transform child in canvasGo.transform.Cast<Transform>().ToList())
            Object.DestroyImmediate(child.gameObject);
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        var banner = Panel(canvas, "TitleBanner", new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, 455f), new Vector2(760f, 90f));
        Text(banner.transform, "TitleText", "항구", 58, Gold, new Vector2(0f, 0f), new Vector2(740f, 70f), FontStyles.Bold, font);
        // Title card: shows for a moment when you arrive, then fades away.
        banner.gameObject.AddComponent<CanvasGroup>();
        banner.gameObject.AddComponent<FadeBanner>();

        EnsureHammerIcon();
        EnsureProfileSprites();

        // ---- Top-left profile: round portrait (cat face) + dark pill with the name.
        var profile = new GameObject("Profile", typeof(RectTransform));
        profile.transform.SetParent(canvas, false);
        var prt = (RectTransform)profile.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
        prt.anchoredPosition = new Vector2(30f, -24f);
        prt.sizeDelta = new Vector2(470f, 130f);

        var pill = UiImage(profile.transform, "NamePill", PillPath, Color.white, new Vector2(0f, 0f), new Vector2(370f, 88f));
        pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0f, 1f);
        pill.rectTransform.anchoredPosition = new Vector2(285f, -65f);
        pill.type = Image.Type.Sliced;
        // Centred on the part of the pill not covered by the portrait.
        var profileName = Text(pill.transform, "Name", "이름", 44, Color.white, new Vector2(15f, 0f), new Vector2(300f, 80f), FontStyles.Bold, font);
        profileName.alignment = TextAlignmentOptions.Center;
        profileName.enableAutoSizing = true;
        profileName.fontSizeMin = 24f;
        profileName.fontSizeMax = 44f;

        // Portrait: thick dark rim, lighter inner ring, then a circular mask showing the face.
        var rim = UiImage(profile.transform, "PortraitRim", CirclePath, new Color(0.09f, 0.08f, 0.11f, 1f), Vector2.zero, new Vector2(130f, 130f));
        rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = new Vector2(0f, 1f);
        rim.rectTransform.anchoredPosition = new Vector2(65f, -65f);
        UiImage(rim.transform, "Ring", CirclePath, new Color(0.3f, 0.28f, 0.36f, 1f), Vector2.zero, new Vector2(112f, 112f));
        var maskImg = UiImage(rim.transform, "Mask", CirclePath, new Color(0.23f, 0.19f, 0.29f, 1f), Vector2.zero, new Vector2(104f, 104f));
        maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        // The idle frame is 15x17 px; scale it so the head (top ~9 rows) fills the circle.
        const float facePx = 7.6f;
        var face = new GameObject("Face", typeof(RectTransform));
        face.transform.SetParent(maskImg.transform, false);
        var faceImg = face.AddComponent<Image>();
        faceImg.sprite = chars[0].idleFrames[0];
        faceImg.preserveAspect = true;
        faceImg.raycastTarget = false;
        var frt = faceImg.rectTransform;
        frt.sizeDelta = new Vector2(17f * facePx, 17f * facePx);
        frt.anchoredPosition = new Vector2(0f, -4f * facePx);

        // ---- Blacksmith hammer button, right under the profile.
        // Same size as the lounge sign below so the two left-side icons match.
        var hammerImg = UiImage(canvas, "BlacksmithButton", HammerIconPath, Color.white, Vector2.zero, new Vector2(VillageIconSize, VillageIconSize));
        var hrt = hammerImg.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(105f, -260f);
        var hammerBtn = MakeButton(hammerImg);
        var hammerColors = hammerBtn.colors;
        hammerColors.highlightedColor = new Color(1f, 0.9f, 0.6f);
        hammerColors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        hammerBtn.colors = hammerColors;
        IconLabel(hammerImg.transform, "대장간", 28, new Vector2(0f, -98f), new Vector2(130f, 42f), font);

        // ---- Back to the tavern (top-right)
        // ---- Back to the lounge (character select): pixel tavern icon, bottom-left.
        EnsureLoungeIcon();
        var backImg = UiImage(canvas, "LoungeButton", LoungeIconPath, Color.white, Vector2.zero, new Vector2(VillageIconSize, VillageIconSize));
        var backRt = backImg.rectTransform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 0f);
        backRt.anchoredPosition = new Vector2(105f, 160f);
        var backBtn = MakeButton(backImg);
        var backColors = backBtn.colors;
        backColors.highlightedColor = new Color(1f, 0.9f, 0.6f);
        backColors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        backBtn.colors = backColors;
        IconLabel(backImg.transform, "라운지", 28, new Vector2(0f, -98f), new Vector2(130f, 42f), font);

        // ---- Settings: pixel gear icon, top-right (same size as the other village icons).
        EnsureGearIcon();
        var gearImg = UiImage(canvas, "SettingsButton", GearIconPath, Color.white, Vector2.zero, new Vector2(VillageIconSize, VillageIconSize));
        var gearRt = gearImg.rectTransform;
        gearRt.anchorMin = gearRt.anchorMax = new Vector2(1f, 1f);
        gearRt.anchoredPosition = new Vector2(-105f, -105f);
        var gearBtn = MakeButton(gearImg);
        var gearColors = gearBtn.colors;
        gearColors.highlightedColor = new Color(1f, 0.9f, 0.6f);
        gearColors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        gearBtn.colors = gearColors;
        IconLabel(gearImg.transform, "설정", 28, new Vector2(0f, -98f), new Vector2(110f, 42f), font);

        // ---- Set sail: the user's pixel treasure-map icon (bottom-right) with a label.
        PrepareMapIcon();
        var departImg = UiImage(canvas, "DepartButton", MapIconPath, Color.white, Vector2.zero, new Vector2(200f, 200f));
        departImg.preserveAspect = true;
        var departRt = departImg.rectTransform;
        departRt.anchorMin = departRt.anchorMax = new Vector2(1f, 0f);
        departRt.anchoredPosition = new Vector2(-150f, 170f);
        var departBtn = MakeButton(departImg);
        var departColors = departBtn.colors;
        departColors.highlightedColor = new Color(1f, 0.92f, 0.7f);
        departColors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        departBtn.colors = departColors;
        IconLabel(departImg.transform, "출항하기", 34, new Vector2(0f, -112f), new Vector2(180f, 50f), font);

        var controllerGo = new GameObject("MainSceneController");
        var mc = controllerGo.AddComponent<MainSceneController>();
        mc.characters = chars;
        mc.catVisual = catSr;
        mc.nameTag = tag;
        mc.portrait = faceImg;
        mc.profileName = profileName;
        mc.departButton = departBtn;
        mc.tavernButton = backBtn;
        mc.blacksmithButton = hammerBtn;
        // The big sailing ship at the pier (left side of the picture), in world units.
        mc.shipArea = new Rect(-7.0f, -2.0f, 3.3f, 2.9f);
        // Setting sail opens the CAT ISLAND world map first (pick a region there).
        mc.stageSceneName = "WorldMapScene";
        // Built last so the shop window draws over everything else.
        mc.blacksmith = BuildBlacksmith(canvas, font);
        mc.settingsButton = gearBtn;
        mc.options = BuildOptions(canvas, font);

        EditorSceneManager.MarkSceneDirty(scene);
        UISkinApplier.ApplyAndSave(scene); // re-skins the UI (Kenney Pixel Adventure) and saves
    }

    // ---------------------------------------------------------------- world map
    const string WorldMapScene = "Assets/Scenes/WorldMapScene.unity";
    const string WorldMapImage = "Assets/Backgrounds/WorldMap.png";
    const string RingPath = "Assets/UI/Ring.png";

    // CAT ISLAND map: the PixelLab Pro Flash pixel-art version (_art_source/WorldMap_pixellab_proflash_470x212.png)
    // stored 8x with hard edges (old painting: _art_backup/worldmap_original). Earlier note: (a pixelized version looked
    // broken). PPU 84.635 -> 19.2 x 8.85 units, bilinear. Only the Ancient
    // Forest is open: pulsing gold ring + "CHAPTER 1" tag, click -> Stage1 (WorldMapController).
    static void BuildWorldMapScene()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(WorldMapImage);
        if (ti == null) throw new System.Exception($"{WorldMapImage} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 3760f / 19.2f; // PixelLab pixel-art map (470x212) stored 8x: 19.2 x 8.66 units
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 4096;
        ti.SaveAndReimport();
        EnsureRingSprite();
        EnsureProfileSprites();

        if (File.Exists(WorldMapScene)) AssetDatabase.DeleteAsset(WorldMapScene);
        if (!AssetDatabase.CopyAsset(TitleScene, WorldMapScene))
            throw new System.Exception($"Could not copy {TitleScene} to {WorldMapScene}");
        Scene scene = EditorSceneManager.OpenScene(WorldMapScene, OpenSceneMode.Single);
        foreach (var n in new[] { "ClickCatcher", "TouchToStartText", "Background", "GameTitleImage", "PLAYER", "Music" })
        {
            var go = Find(scene, n);
            if (go != null) Object.DestroyImmediate(go);
        }
        var font = KoreanFont.Load();

        // 1625x749 at PPU 84.635 -> 19.2 x 8.85 units.
        var cam = Find(scene, "Main Camera").GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.4f; // fits the full width on 16:9 (WorldMapController refits per screen)
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x2b, 0x1d, 0x12, 0xff);
        EditorUtility.SetDirty(cam);

        var bg = new GameObject("WorldMapBackground");
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WorldMapImage);
        bgSr.sortingOrder = -100;

        // Ancient Forest: the dense grove above the "고대 숲" label (original map px 480,470,
        // radii 125x78 px) -> world (PPU 84.635, picture centered).
        const float mapPpu = 1625f / 19.2f;
        Vector2 center = new Vector2((480f - 812.5f) / mapPpu, (374.5f - 470f) / mapPpu);
        Vector2 radius = new Vector2(125f / mapPpu, 78f / mapPpu);

        var glowGo = new GameObject("ForestGlow");
        glowGo.transform.position = center;
        glowGo.transform.localScale = new Vector3(radius.x * 2f, radius.y * 2f, 1f);
        var glow = glowGo.AddComponent<SpriteRenderer>();
        glow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        glow.color = new Color(1f, 0.85f, 0.45f, 0.15f);
        glow.sortingOrder = -50;
        glowGo.SetActive(false); // the tinted disc read as a blurry smudge over the pixel-art map; the ring is enough

        var ringGo = new GameObject("ForestRing");
        ringGo.transform.position = center;
        ringGo.transform.localScale = new Vector3(radius.x * 2f, radius.y * 2f, 1f);
        var ring = ringGo.AddComponent<SpriteRenderer>();
        ring.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        ring.color = new Color(1f, 0.82f, 0.4f, 1f);
        ring.sortingOrder = -40;

        // "CHAPTER 1" tag bobbing over the forest.
        var markerRoot = new GameObject("ForestMarker");
        // Nudged right: the wider tag would cover the end of the map title on its left.
        markerRoot.transform.position = new Vector3(center.x + 0.45f, center.y + radius.y + 0.45f, 0f);
        var marker = new GameObject("Tag");
        marker.transform.SetParent(markerRoot.transform, false);
        var plate = marker.AddComponent<SpriteRenderer>();
        plate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(NameplatePath);
        plate.drawMode = SpriteDrawMode.Sliced;
        plate.size = new Vector2(4.2f, 0.6f);
        plate.sortingOrder = 19;
        var tagTextGo = new GameObject("Text");
        tagTextGo.transform.SetParent(marker.transform, false);
        var tagText = tagTextGo.AddComponent<TextMeshPro>();
        tagText.font = font;
        tagText.fontSize = 2.8f;
        tagText.fontStyle = FontStyles.Bold;
        tagText.alignment = TextAlignmentOptions.Center;
        tagText.color = Gold;
        tagText.textWrappingMode = TextWrappingModes.NoWrap;
        tagText.sortingOrder = 20;
        tagText.rectTransform.sizeDelta = new Vector2(5.4f, 0.6f);
        tagText.text = "CHAPTER 1 · 고대 숲"; // the map art has only English names; this matches the hint text

        // ---- UI
        var canvasGo = Find(scene, "Canvas");
        foreach (Transform child in canvasGo.transform.Cast<Transform>().ToList())
            Object.DestroyImmediate(child.gameObject);
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        var backImg = UiImage(canvas, "BackButton", PillPath, Color.white, Vector2.zero, new Vector2(270f, 70f));
        backImg.type = Image.Type.Sliced;
        var backRt = backImg.rectTransform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
        backRt.anchoredPosition = new Vector2(165f, -55f);
        var backBtn = MakeButton(backImg);
        Text(backImg.transform, "Label", "< 항구", 36, Color.white, Vector2.zero, new Vector2(260f, 64f), FontStyles.Bold, font); // 36: on the pixel grid, like the lobby's "< 뒤로"

        var hint = UiImage(canvas, "HintPlate", PillPath, Color.white, Vector2.zero, new Vector2(620f, 70f));
        hint.type = Image.Type.Sliced;
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 60f);
        Text(hint.transform, "Label", "고대 숲을 눌러 출정하세요", 32, Gold, Vector2.zero, new Vector2(600f, 64f), FontStyles.Bold, font);

        var message = Text(canvas, "Message", "아직 갈 수 없는 지역입니다", 34, new Color(1f, 0.55f, 0.5f), Vector2.zero, new Vector2(900f, 60f), FontStyles.Bold, font);
        var mrt = message.rectTransform;
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0f);
        mrt.anchoredPosition = new Vector2(0f, 140f);
        message.outlineWidth = 0.25f;
        message.outlineColor = new Color32(20, 12, 16, 255);

        var controllerGo = new GameObject("WorldMapController");
        var wc = controllerGo.AddComponent<WorldMapController>();
        wc.forestCenter = center;
        wc.forestRadius = radius;
        wc.ring = ring;
        wc.glow = glow;
        wc.marker = marker.transform;
        wc.messageText = message;
        wc.backButton = backBtn;

        EditorSceneManager.MarkSceneDirty(scene);
        UISkinApplier.ApplyAndSave(scene); // re-skins the UI (Kenney Pixel Adventure) and saves
    }

    // 256x256 anti-aliased ring (8px thick) outlining the open region on the (smooth,
    // non-pixel) world map, so it scales into a clean ellipse.
    static void EnsureRingSprite()
    {
        const int C = 256;
        const float thickness = 8f;
        var tex = new Texture2D(C, C, TextureFormat.RGBA32, false);
        for (int y = 0; y < C; y++)
            for (int x = 0; x < C; x++)
            {
                float dx = x + 0.5f - C / 2f, dy = y + 0.5f - C / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01(C / 2f - d), inner = Mathf.Clamp01(d - (C / 2f - thickness));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Min(outer, inner)));
            }
        File.WriteAllBytes(RingPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(RingPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(RingPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 256f;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
    }

    // Dark screen, the picked cat rolling in the middle (LoadingScreen), "Loading ..." below.
    static void BuildLoadingScene()
    {
        if (File.Exists(LoadingScene)) AssetDatabase.DeleteAsset(LoadingScene);
        if (!AssetDatabase.CopyAsset(TitleScene, LoadingScene))
            throw new System.Exception($"Could not copy {TitleScene} to {LoadingScene}");
        Scene scene = EditorSceneManager.OpenScene(LoadingScene, OpenSceneMode.Single);

        foreach (var n in new[] { "ClickCatcher", "TouchToStartText", "Background", "GameTitleImage", "PLAYER", "Music" })
        {
            var go = Find(scene, n);
            if (go != null) Object.DestroyImmediate(go);
        }

        var chars = CharacterAssets.Load();
        var font = KoreanFont.Load();

        var cam = Find(scene, "Main Camera").GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x12, 0x0c, 0x10, 0xff);
        EditorUtility.SetDirty(cam);

        // The cat stands on y = -0.6 (this object = its feet), with a soft shadow under it.
        var root = new GameObject("RollingCat");
        root.transform.position = new Vector3(0f, -0.6f, 0f);
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(root.transform, false);
        shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        var shadowSr = shadow.AddComponent<SpriteRenderer>();
        shadowSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(NameplatePath);
        shadowSr.drawMode = SpriteDrawMode.Sliced;
        shadowSr.size = new Vector2(2.2f, 0.25f);
        shadowSr.color = new Color(0f, 0f, 0f, 0.5f);
        shadowSr.sortingOrder = -1;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(LoadingCatScale, LoadingCatScale, 1f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = chars[0].dodgeFrames[0];
        Bounds b = sr.sprite.bounds;
        visual.transform.localPosition = new Vector3(-b.center.x * LoadingCatScale, -b.min.y * LoadingCatScale, 0f);

        var canvasGo = Find(scene, "Canvas");
        foreach (Transform child in canvasGo.transform.Cast<Transform>().ToList())
            Object.DestroyImmediate(child.gameObject);
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var text = Text(canvasGo.transform, "LoadingText", "Loading .", 64, Gold, new Vector2(0f, -170f), new Vector2(800f, 100f), FontStyles.Bold, font);
        // Fixed left edge so the moving dots don't make the word slide around.
        text.alignment = TextAlignmentOptions.Left;
        text.rectTransform.anchoredPosition = new Vector2(262f, -170f);

        var screen = root.AddComponent<LoadingScreen>();
        screen.characters = chars;
        screen.visual = sr;
        screen.loadingText = text;

        EditorSceneManager.MarkSceneDirty(scene);
        UISkinApplier.ApplyAndSave(scene); // re-skins the UI (Kenney Pixel Adventure) and saves
    }

    const string HammerIconPath = "Assets/UI/HammerIcon.png";

    // Display size of the village icons (the user's 128x128 hammer-on-anvil, tavern sign and
    // gear, ~112px of art each), so they all read the same size on screen.
    const float VillageIconSize = 160f;

    // The user's pixel-art hammer and anvil (128x128, transparent background). Only the
    // import settings are applied here - the picture itself is never redrawn.
    static void EnsureHammerIcon()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(HammerIconPath);
        if (ti == null) throw new System.Exception($"{HammerIconPath} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    const string GearIconPath = "Assets/UI/GearIcon.png";

    // The user's pixel-art mossy gear (128x128, transparent background). Only the import
    // settings are applied here - the picture itself is never redrawn.
    static void EnsureGearIcon()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(GearIconPath);
        if (ti == null) throw new System.Exception($"{GearIconPath} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    // Settings window: dim blocker + panel with a master volume slider and a close button.
    static OptionsUI BuildOptions(Transform canvas, TMP_FontAsset font)
    {
        var blocker = Panel(canvas, "OptionsPanel", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.zero);
        var brt = blocker.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        blocker.raycastTarget = true;

        var frame = Panel(blocker.transform, "Frame", new Color(0.55f, 0.4f, 0.2f, 1f), Vector2.zero, new Vector2(712f, 432f));
        var panel = Panel(frame.transform, "Inner", new Color(0.07f, 0.05f, 0.07f, 1f), Vector2.zero, new Vector2(700f, 420f));
        Text(panel.transform, "Title", "설정", 60, Gold, new Vector2(0f, 150f), new Vector2(600f, 80f), FontStyles.Bold, font);
        Panel(panel.transform, "Divider", new Color(0.55f, 0.4f, 0.2f, 0.6f), new Vector2(0f, 98f), new Vector2(600f, 3f));

        var label = Text(panel.transform, "VolumeLabel", "전체 볼륨", 36, Parchment, new Vector2(-170f, 30f), new Vector2(240f, 50f), FontStyles.Bold, font);
        label.alignment = TextAlignmentOptions.Left;
        var value = Text(panel.transform, "VolumeValue", "100%", 36, Color.white, new Vector2(240f, 30f), new Vector2(140f, 50f), FontStyles.Bold, font);
        value.alignment = TextAlignmentOptions.Right;

        // Slider built from Unity's default control, then restyled (dark track, gold fill).
        var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderGo.name = "VolumeSlider";
        sliderGo.transform.SetParent(panel.transform, false);
        var srt = (RectTransform)sliderGo.transform;
        srt.anchoredPosition = new Vector2(0f, -40f);
        srt.sizeDelta = new Vector2(560f, 36f);
        var slider = sliderGo.GetComponent<Slider>();
        foreach (var img in sliderGo.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "Background") img.color = new Color(0.25f, 0.22f, 0.28f, 1f);
            else if (img.name == "Fill") img.color = Gold;
            else if (img.name == "Handle")
            {
                img.color = Color.white;
                img.rectTransform.sizeDelta = new Vector2(28f, 0f);
            }
        }
        slider.value = 1f;

        var closeImg = Panel(panel.transform, "CloseButton", new Color(0.25f, 0.22f, 0.25f, 1f), new Vector2(0f, -150f), new Vector2(240f, 80f));
        var closeBtn = MakeButton(closeImg);
        Text(closeImg.transform, "Label", "닫기", 36, Color.white, Vector2.zero, new Vector2(230f, 76f), FontStyles.Bold, font);

        var ui = blocker.gameObject.AddComponent<OptionsUI>();
        ui.panel = blocker.gameObject;
        ui.volumeSlider = slider;
        ui.volumeValue = value;
        ui.closeButton = closeBtn;
        return ui;
    }

    const string LoungeIconPath = "Assets/UI/LoungeIcon.png";

    // The user's pixel-art tavern sign (128x128, transparent background). Only the import
    // settings are applied here - the picture itself is never redrawn.
    static void EnsureLoungeIcon()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(LoungeIconPath);
        if (ti == null) throw new System.Exception($"{LoungeIconPath} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    // User-supplied treasure map, background removed and downscaled to 118x118 pixel art.
    const string MapIconPath = "Assets/UI/MapIcon.png";

    static void PrepareMapIcon()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(MapIconPath);
        if (ti == null) throw new System.Exception($"{MapIconPath} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    const string CirclePath = "Assets/UI/Circle.png";
    const string PillPath = "Assets/UI/NamePill.png";

    // White circle (tinted per use, also used as the portrait mask) and a rounded "pill"
    // plate (near-black with a grey outline, 9-sliced so it stretches to any width).
    static void EnsureProfileSprites()
    {
        const int C = 64;
        var circle = new Texture2D(C, C, TextureFormat.RGBA32, false);
        for (int y = 0; y < C; y++)
            for (int x = 0; x < C; x++)
            {
                float dx = x + 0.5f - C / 2f, dy = y + 0.5f - C / 2f;
                circle.SetPixel(x, y, dx * dx + dy * dy <= (C / 2f) * (C / 2f) ? Color.white : Color.clear);
            }
        File.WriteAllBytes(CirclePath, circle.EncodeToPNG());

        const int W = 32, H = 24, R = 12;
        var pill = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var border = new Color32(0x4a, 0x46, 0x56, 0xff);
        var fill = new Color32(0x14, 0x12, 0x18, 0xff);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // distance outside the rounded rectangle's straight middle section
                float cx = Mathf.Clamp(x + 0.5f, R, W - R), cy = H / 2f;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                pill.SetPixel(x, y, d > R ? (Color)Color.clear : d > R - 2f ? (Color)border : (Color)fill);
            }
        File.WriteAllBytes(PillPath, pill.EncodeToPNG());

        foreach (var (path, ppu, sliced) in new[] { (CirclePath, 64f, false), (PillPath, 32f, true) })
        {
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            if (sliced)
            {
                ti.spriteBorder = new Vector4(R, 0f, R, 0f);
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(s);
            }
            ti.SaveAndReimport();
        }
    }

    // Gold label on a small dark pill so it stays readable over the busy village picture.
    static TextMeshProUGUI IconLabel(Transform parent, string text, float size, Vector2 pos, Vector2 box, TMP_FontAsset font)
    {
        var plate = UiImage(parent, "LabelPlate", PillPath, Color.white, pos, box);
        plate.type = Image.Type.Sliced;
        plate.pixelsPerUnitMultiplier = 2f; // smaller rounded caps for a small plate
        return Text(plate.transform, "Label", text, size, Gold, Vector2.zero, box, FontStyles.Bold, font);
    }

    static Image UiImage(Transform parent, string name, string spritePath, Color color, Vector2 pos, Vector2 size)
    {
        var img = Panel(parent, name, color, pos, size);
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        return img;
    }

    // Blacksmith upgrade window: a full-screen dim blocker (so the village can't be clicked
    // behind it) holding a panel with the gold, three upgrade rows and a close button.
    static BlacksmithUI BuildBlacksmith(Transform canvas, TMP_FontAsset font)
    {
        var blocker = Panel(canvas, "BlacksmithPanel", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.zero);
        var brt = blocker.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        blocker.raycastTarget = true;

        var frame = Panel(blocker.transform, "Frame", new Color(0.55f, 0.4f, 0.2f, 1f), Vector2.zero, new Vector2(812f, 712f));
        var panel = Panel(frame.transform, "Inner", new Color(0.07f, 0.05f, 0.07f, 0.95f), Vector2.zero, new Vector2(800f, 700f));
        Text(panel.transform, "Title", "대장간", 64, Gold, new Vector2(0f, 285f), new Vector2(700f, 80f), FontStyles.Bold, font);
        var message = Text(panel.transform, "Message", "어서 오게, 용병. 무엇을 강화하겠나?", 28, Parchment, new Vector2(0f, 222f), new Vector2(740f, 44f), FontStyles.Italic, font);
        var goldText = Text(panel.transform, "Gold", "보유 골드 : 0", 36, new Color(1f, 0.85f, 0.35f), new Vector2(0f, 165f), new Vector2(700f, 50f), FontStyles.Bold, font);
        Panel(panel.transform, "Divider", new Color(0.55f, 0.4f, 0.2f, 0.6f), new Vector2(0f, 128f), new Vector2(700f, 3f));

        var ui = blocker.gameObject.AddComponent<BlacksmithUI>();
        ui.panel = blocker.gameObject;
        ui.goldText = goldText;
        ui.messageText = message;
        for (int i = 0; i < 3; i++)
        {
            float y = 60f - i * 120f;
            var row = Panel(panel.transform, $"Row{i}", new Color(1f, 1f, 1f, 0.05f), new Vector2(0f, y), new Vector2(720f, 104f));
            var label = Text(row.transform, "Label", "", 36, Color.white, new Vector2(-110f, 0f), new Vector2(460f, 96f), FontStyles.Normal, font);
            label.alignment = TextAlignmentOptions.Left;
            var btnImg = Panel(row.transform, "BuyButton", Crimson, new Vector2(240f, 0f), new Vector2(210f, 80f));
            var btn = MakeButton(btnImg);
            var btnLabel = Text(btnImg.transform, "Label", "강화", 32, Color.white, Vector2.zero, new Vector2(200f, 76f), FontStyles.Bold, font);
            ui.rowLabels[i] = label;
            ui.buyButtons[i] = btn;
            ui.buyLabels[i] = btnLabel;
        }

        var closeImg = Panel(panel.transform, "CloseButton", new Color(0.25f, 0.22f, 0.25f, 1f), new Vector2(0f, -290f), new Vector2(260f, 84f));
        ui.closeButton = MakeButton(closeImg);
        Text(closeImg.transform, "Label", "나가기", 36, Color.white, Vector2.zero, new Vector2(250f, 80f), FontStyles.Bold, font);
        return ui;
    }

    static void PrepareLobbyTexture()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(LobbyImage);
        if (ti == null) throw new System.Exception($"{LobbyImage} not found");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
    }

    static LobbyCharacterDisplay MakeCat(string name, int index, CharacterDefinition def, Vector3 feet, TMP_FontAsset font)
    {
        var root = new GameObject(name);
        root.transform.position = feet;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(CharacterScale, CharacterScale, 1f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = def.idleFrames[0];
        // Pre-position like LobbyCharacterDisplay does at runtime, so the Scene view looks right too.
        Bounds b = def.idleFrames[0].bounds;
        visual.transform.localPosition = new Vector3(-b.center.x * CharacterScale, -b.min.y * CharacterScale, 0f);

        // Job plate above the head: a small 9-sliced pixel plate with the job centered in it.
        // Its width is fitted to the text at runtime (LobbyCharacterDisplay.RefreshNameTag).
        var tagGo = new GameObject("JobTag");
        tagGo.transform.SetParent(root.transform, false);
        tagGo.transform.localPosition = new Vector3(0f, (def.idleFrames[0].rect.height / def.idleFrames[0].pixelsPerUnit) * CharacterScale + 0.45f, 0f);

        var plate = tagGo.AddComponent<SpriteRenderer>();
        plate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(NameplatePath);
        plate.drawMode = SpriteDrawMode.Sliced;
        plate.size = new Vector2(1.6f, 0.6f);
        plate.sortingOrder = 19;

        var textGo = new GameObject("JobText");
        textGo.transform.SetParent(tagGo.transform, false);
        var job = textGo.AddComponent<TextMeshPro>();
        job.font = font;
        job.fontSize = 3f;
        job.fontStyle = FontStyles.Bold;
        job.alignment = TextAlignmentOptions.Center;
        job.color = Gold;
        job.sortingOrder = 20;
        job.textWrappingMode = TextWrappingModes.NoWrap;
        job.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
        job.text = def.tagName;

        var display = root.AddComponent<LobbyCharacterDisplay>();
        display.character = def;
        display.characterIndex = index;
        display.visual = visual.transform;
        display.tagRoot = tagGo;
        display.jobPlate = plate;
        display.jobText = job;
        return display;
    }

    const string NameplatePath = "Assets/UI/LobbyNameplate.png";

    // 12x8 dark plate with a 1px gold border and clipped corners, imported as a
    // 9-slice sprite (border 3px) so it stretches to fit any job name without blurring.
    static void EnsureNameplateSprite()
    {
        if (!File.Exists(NameplatePath))
        {
            const int w = 12, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var gold = new Color32(0xc9, 0xa2, 0x4a, 0xff);
            var fill = new Color32(0x1a, 0x12, 0x18, 0xe6);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    tex.SetPixel(x, y, corner ? clear : edge ? gold : fill);
                }
            File.WriteAllBytes(NameplatePath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(NameplatePath);
        }

        var ti = (TextureImporter)AssetImporter.GetAtPath(NameplatePath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 16f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.spriteBorder = new Vector4(3f, 3f, 3f, 3f);
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // required for Sliced draw mode
        ti.SetTextureSettings(settings);
        ti.SaveAndReimport();
    }

    static Button MakeButton(Image img)
    {
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    static TMP_InputField MakeInputField(Transform parent, TMP_FontAsset font, Vector2 pos, Vector2 size)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "NameInput";
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.95f, 0.92f, 0.85f, 1f);

        var input = go.GetComponent<TMP_InputField>();
        input.fontAsset = font;
        input.pointSize = 44f;
        input.characterLimit = CharacterSelection.MaxNameLength;
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
        {
            t.font = font;
            t.fontSize = 44f;
            t.alignment = TextAlignmentOptions.Center;
        }
        input.textComponent.color = new Color(0.12f, 0.08f, 0.1f);
        if (input.placeholder is TMP_Text ph)
        {
            ph.text = "이름 입력";
            ph.color = new Color(0.45f, 0.4f, 0.4f, 0.8f);
        }
        return input;
    }

    static Image Panel(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 box, FontStyles style, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}

// Korean pixel UI font. Galmuri11 contains Hangul, Latin and digits in one family,
// so Korean and numbers keep the same height and stroke weight across every screen.
public static class KoreanFont
{
    const string MainFontPath = "Assets/font/Galmuri/Galmuri11.ttf";
    public const string MainAssetPath = "Assets/font/Galmuri/Galmuri11 Dynamic SDF.asset";

    // Static English labels that live in the scenes (code-set texts are translated in code).
    static readonly Dictionary<string, string> Translations = new Dictionary<string, string>
    {
        { "Retry", "다시 도전" },
        { "Touch to Start", "아무 곳이나 클릭하여 시작" },
    };

    public static void Ensure()
    {
        var main = CreateDynamic(MainFontPath, MainAssetPath, "Galmuri11 Dynamic SDF");
        CleanPixelFont(main);
        AddGlobalFallback(main);
        AssetDatabase.SaveAssets();
    }

    // Both fonts are pixel fonts: TMP's fake bold (dilating the SDF) melted their strokes
    // into blobs and fake italic sheared them into jagged steps, so both are turned off
    // (bold/italic text simply renders regular), and edges are sharpened a little.
    static void CleanPixelFont(TMP_FontAsset font)
    {
        font.boldStyle = 0f;
        font.italicStyle = 0;
        var mat = font.material;
        if (mat != null)
        {
            if (mat.HasProperty(ShaderUtilities.ID_WeightBold)) mat.SetFloat(ShaderUtilities.ID_WeightBold, 0f);
            if (mat.HasProperty("_Sharpness")) mat.SetFloat("_Sharpness", 0.5f);
            EditorUtility.SetDirty(mat);
        }
        EditorUtility.SetDirty(font);
    }

    static TMP_FontAsset CreateDynamic(string sourcePath, string assetPath, string name)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;
        var src = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (src == null) throw new System.Exception($"{sourcePath} not found");
        var fa = TMP_FontAsset.CreateFontAsset(src, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = name;
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.atlasTextures[0].name = fa.name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // The font every built text uses: Departure Mono, with Korean as its fallback.
    public static TMP_FontAsset Load()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainAssetPath);
    }

    static void AddGlobalFallback(TMP_FontAsset fa)
    {
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings == null) return;
        var so = new SerializedObject(settings);
        var list = so.FindProperty("m_fallbackFontAssets");
        if (list == null) return;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == fa) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fa;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    // Puts every TMP text in the scene on the Korean font and translates known labels.
    public static void ApplyToScene(Scene scene)
    {
        var fa = Load();
        if (fa == null) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = fa;
                if (t.text != null && Translations.TryGetValue(t.text.Trim(), out var ko)) t.text = ko;
                EditorUtility.SetDirty(t);
            }
    }
}

// Creates the CharacterDefinition assets from the sprite sheets. Frame choices were
// checked against the actual sheet images: Meow Knight uses Attack_3 (the other attack
// sheets interleave small FX sprites), Meowolas uses Attack_1 minus frame 3 (a wide frame
// with the flying arrow baked in - the arrow is spawned as a real projectile instead).
public static class CharacterAssets
{
    const string Dir = "Assets/Characters";
    public const string KnightPath = Dir + "/MeowKnight.asset";
    public const string ArcherPath = Dir + "/Meowolas.asset";
    const string KnightFramesDir = "Assets/Meow Knight/Frames";
    const string ArcherFramesDir = "Assets/Meowolas/Frames";

    // Only the character assets (frames, combo) - no scenes are rebuilt, so manual scene
    // edits are kept. Scenes reference the assets, so changes show up immediately.
    [MenuItem("Cats Smash/Rebuild Character Assets")]
    public static void RebuildMenu() => Ensure();

    public static void RebuildBatch()
    {
        Ensure();
        AssetDatabase.SaveAssets();
    }

    static AttackMove Move(string name, Sprite[] frames, float fps, int hitFrame, int recovery, float damage, float lunge, bool allAround = false)
    {
        // Reach from the art: the furthest the blade/effect gets in front of the body
        // on the hit frame (the frames are mirrored when facing left).
        float reach = AsepriteFrames.ForwardReach(frames[hitFrame]);
        return new AttackMove
        {
            name = name,
            frames = frames,
            framesPerSecond = fps,
            hitFrame = hitFrame,
            recoveryFrames = recovery,
            damageMultiplier = damage,
            reach = reach,
            hitsAllAround = allAround,
            lunge = lunge,
        };
    }

    public static void Ensure()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Characters");

        var knight = LoadOrCreate(KnightPath);
        knight.displayName = "Meow Knight";
        knight.jobName = "검사";
        knight.tagName = "하양이";
        knight.role = "근접 · 검으로 베기";
        knight.roleGroup = "근거리 전사";
        knight.roleColor = new Color(0.62f, 0.16f, 0.22f, 1f);
        knight.description = "검 한 자루로 붉은 타락에 맞서는 고양이 기사.\n적에게 바짝 붙어 묵직한 검격으로 베어낸다.";
        // Frames come straight from the .aseprite canvas (shared pivot, sword FX included).
        var k = AsepriteFrames.Build("Assets/Meow Knight/Meow_Knight.aseprite", KnightFramesDir, "Knight");
        knight.canvasAligned = true;
        knight.idleFrames = k["Idle"];
        knight.runFrames = k["Run"];
        knight.damageFrames = k["Take_Damage"];
        knight.jumpFrames = k["Jump"];
        knight.deathFrames = k["Death"];
        knight.dodgeFrames = k["Dodge"];
        // Ground combo, chosen so each move starts where the previous one ends:
        // overhead chop -> rising slash (both start/end with the blade level) -> wind-up thrust.
        knight.combo = new[]
        {
            Move("내려베기", k["Attack_2"], 14f, hitFrame: 1, recovery: 0, damage: 1f, lunge: 0.04f),
            Move("올려베기", k["Attack_3"], 14f, hitFrame: 1, recovery: 0, damage: 1f, lunge: 0.04f),
            Move("찌르기", k["Attack_1"], 16f, hitFrame: 4, recovery: 3, damage: 1.5f, lunge: 0.1f),
        };
        // Attack_4 is the plunge: frames 0-2 = falling with the sword pointed down,
        // 3-7 = the impact dome and the shockwave rolling out to both sides.
        var plunge = k["Attack_4"];
        knight.jumpAttackFallFrames = plunge.Take(3).ToArray();
        knight.jumpAttack = Move("내려찍기", plunge.Skip(3).ToArray(), 13f, hitFrame: 0, recovery: 0, damage: 2f, lunge: 0f, allAround: true);
        knight.bonusMaxHP = 1f; // melee: has to take hits to deal them
        // Reach from the widest shockwave frame, not the dome.
        knight.jumpAttack.reach = AsepriteFrames.ForwardReach(knight.jumpAttack.frames[1]);
        knight.comboWindow = 0.35f;
        // The lounge plays the whole combo once when the cat is clicked.
        knight.attackFrames = knight.combo.SelectMany(m => m.frames).ToArray();
        knight.dodgeFramesPerSecond = 12f;
        knight.framesPerSecond = 8f;
        knight.attackFramesPerSecond = 14f;
        knight.damageFramesPerSecond = 10f;
        knight.jumpFramesPerSecond = 16f;
        knight.deathFramesPerSecond = 8f;
        knight.ranged = false;
        knight.projectileSprite = null;
        EditorUtility.SetDirty(knight);

        var archer = LoadOrCreate(ArcherPath);
        archer.displayName = "Meowolas";
        archer.jobName = "궁수";
        archer.tagName = "치즈";
        archer.role = "원거리 · 활로 화살 쏘기";
        archer.roleGroup = "원거리 사수";
        archer.roleColor = new Color(0.18f, 0.42f, 0.38f, 1f);
        archer.description = "숲의 바람을 읽는 고양이 궁수.\n거리를 벌리고 마우스로 조준해 화살을 날린다.";
        // Same .aseprite pipeline as the knight. The flying arrow is painted into the frames
        // after the release (Arrow layer, plus the Effect/Fade_Effect glow of the electro
        // arrow); those layers are left out there because the game shoots a real arrow.
        var a = AsepriteFrames.Build("Assets/Meowolas/MEOWOLAS.aseprite", ArcherFramesDir, "Archer", skipLayer: (tag, i, layer) =>
            (tag == "Attack_1" && i >= 3 && layer == "Arrow") ||
            (tag == "Attack_2" && i >= 6 && (layer == "Arrow" || layer == "Effect" || layer == "Fade_Effect")));
        archer.canvasAligned = true;
        archer.idleFrames = a["Idle"];
        archer.runFrames = a["Run"];
        archer.damageFrames = a["Take_damage"];
        archer.jumpFrames = a["Jump"];
        archer.deathFrames = a["Death"];
        archer.dodgeFrames = a["Dodge"];
        // Ground combo: two quick shots, then the charged electro arrow (pierces).
        // Arrows leave the bow on the release frame, from the nocking point.
        var bow = new Vector2(0.08f, 0.005f);
        var shot = Move("사격", a["Attack_1"], 14f, hitFrame: 3, recovery: 1, damage: 0.85f, lunge: 0f);
        shot.projectileFrames = a["Arrow"];
        shot.projectileSpawn = bow;
        var shot2 = Move("사격", a["Attack_1"], 14f, hitFrame: 3, recovery: 1, damage: 0.85f, lunge: 0f);
        shot2.projectileFrames = a["Arrow"];
        shot2.projectileSpawn = bow;
        var electro = Move("전기 화살", a["Attack_2"], 14f, hitFrame: 6, recovery: 1, damage: 1.7f, lunge: 0f);
        electro.projectileFrames = a["Electro Arrow"];
        electro.projectileFps = 14f;
        electro.projectileSpeedMultiplier = 1.25f;
        electro.projectilePierces = true;
        electro.projectileSpawn = bow;
        archer.combo = new[] { shot, shot2, electro };
        archer.comboWindow = 0.4f;
        // Attack_4 = jump attack: frames 0-2 (glowing orb) while diving, 3-10 on landing -
        // arrows burst up, then the shockwave rolls out to both sides (frame 5 = index 2).
        var archerPlunge = a["Attack_4"];
        archer.jumpAttackFallFrames = archerPlunge.Take(3).ToArray();
        archer.jumpAttack = Move("화살 폭풍", archerPlunge.Skip(3).ToArray(), 13f, hitFrame: 2, recovery: 0, damage: 1.5f, lunge: 0f, allAround: true);
        // The lounge plays a normal shot and the electro arrow.
        archer.attackFrames = a["Attack_1"].Concat(a["Attack_2"]).ToArray();
        archer.dodgeFramesPerSecond = 12f;
        archer.framesPerSecond = 8f;
        archer.attackFramesPerSecond = 14f;
        archer.damageFramesPerSecond = 10f;
        archer.jumpFramesPerSecond = 16f;
        archer.deathFramesPerSecond = 8f;
        archer.ranged = true;
        archer.projectileSprite = a["Arrow"][0];
        archer.projectileSpeed = 16f;
        archer.projectileRange = 8f; // just past a monster's 7-tile sight: the first shot is free, the rest isn't
        archer.bonusMaxHP = 0f;
        archer.releaseDelay = 3f / 14f; // only used without a combo
        EditorUtility.SetDirty(archer);

        ComputeAlignment(knight);
        ComputeAlignment(archer);
        AssetDatabase.SaveAssets();
    }

    // ---- per-frame body alignment ----
    // For every frame, try every horizontal shift against Idle frame 0 (both bottom-aligned,
    // so the feet stay on the ground) and keep the shift where the most body pixels land on
    // same-coloured body pixels. Swords, bows and tails don't match the idle body, so they
    // don't drag the character around any more.
    static readonly Dictionary<string, Texture2D> readableCache = new Dictionary<string, Texture2D>();

    static void ComputeAlignment(CharacterDefinition def)
    {
        readableCache.Clear();
        var reference = def.idleFrames[0];
        if (def.canvasAligned)
        {
            // Shared pivot already lines everything up; only the body size is needed.
            def.alignSprites = new Sprite[0];
            def.alignOffsetsX = new float[0];
            def.alignReferenceSize = new Vector2(reference.rect.width, reference.rect.height) / reference.pixelsPerUnit;
            def.alignVersion = 2;
            EditorUtility.SetDirty(def);
            return;
        }
        Pixels(reference, out var refPx, out int rw, out int rh);
        float ppu = reference.pixelsPerUnit;

        var sprites = new List<Sprite>();
        var offsets = new List<float>();
        foreach (var set in new[] { def.idleFrames, def.runFrames, def.attackFrames, def.damageFrames, def.jumpFrames, def.deathFrames })
        {
            if (set == null) continue;
            foreach (var s in set)
            {
                if (s == null || sprites.Contains(s)) continue;
                Pixels(s, out var px, out int w, out int h);
                sprites.Add(s);
                offsets.Add(BestShift(refPx, rw, rh, px, w, h) / ppu);
            }
        }

        def.alignSprites = sprites.ToArray();
        def.alignOffsetsX = offsets.ToArray();
        def.alignReferenceSize = new Vector2(rw / ppu, rh / ppu);
        def.alignVersion = 1;
        EditorUtility.SetDirty(def);
    }

    static int BestShift(Color32[] refPx, int rw, int rh, Color32[] px, int w, int h)
    {
        int best = 0;
        int bestScore = -1;
        int rows = Mathf.Min(h, rh);
        for (int dx = -w; dx <= rw; dx++)
        {
            int score = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < w; x++)
                {
                    int rx = x + dx;
                    if (rx < 0 || rx >= rw) continue;
                    var a = px[y * w + x];
                    if (a.a < 128) continue;
                    var b = refPx[y * rw + rx];
                    if (b.a < 128) continue;
                    int diff = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    if (diff < 40) score++;
                }
            // Ties go to the smaller shift.
            if (score > bestScore || (score == bestScore && Mathf.Abs(dx) < Mathf.Abs(best)))
            {
                bestScore = score;
                best = dx;
            }
        }
        return best;
    }

    // The sheets aren't import-readable, so read the PNG file directly. Rows are bottom-up,
    // matching Sprite.rect.
    static void Pixels(Sprite s, out Color32[] px, out int w, out int h)
    {
        string path = AssetDatabase.GetAssetPath(s.texture);
        if (!readableCache.TryGetValue(path, out var tex))
        {
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            readableCache[path] = tex;
        }
        Rect r = s.rect;
        w = (int)r.width;
        h = (int)r.height;
        px = new Color32[w * h];
        var all = tex.GetPixels32();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = all[((int)r.y + y) * tex.width + (int)r.x + x];
    }

    // Reload after any OpenScene(Single) - see StageSceneBuilder.LoadTiles for why.
    public static CharacterDefinition[] Load()
    {
        return new[]
        {
            AssetDatabase.LoadAssetAtPath<CharacterDefinition>(KnightPath),
            AssetDatabase.LoadAssetAtPath<CharacterDefinition>(ArcherPath),
        };
    }

    static CharacterDefinition LoadOrCreate(string path)
    {
        var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
        if (def != null) return def;
        def = ScriptableObject.CreateInstance<CharacterDefinition>();
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    // All sub-sprites of a sheet in frame order (name suffix _0, _1, ...), optionally
    // picking only some indices.
    static Sprite[] Frames(string sheetPath, params int[] pick)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
            .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int n) ? n : 0)
            .ToArray();
        if (all.Length == 0) throw new System.Exception($"No sprites found in {sheetPath}");
        if (pick == null || pick.Length == 0) return all;
        return pick.Select(i => all[i]).ToArray();
    }
}
