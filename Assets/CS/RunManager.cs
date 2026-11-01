using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Drives the chapter loop. Two modes:
// - Stage scenes (stageNumber 1-5, Stage1..Stage5.unity): each scene is one stage
//   with its own tilemap. Clearing the monsters opens the card draft, and picking a card
//   carries the run's state over (PlayerStats.SaveForNextStage) and loads nextSceneName.
//   The last stage (totalStages) holds only the boss; beating it ends the run.
// - Legacy (stageNumber 0, the old GameScene demo): all 3 stages sit side by side in one
//   scene and the player walks right from one into the next.
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Tooltip("1-3 for the Stage scenes. 0 = legacy single-scene demo (GameScene).")]
    public int stageNumber = 0;
    [Tooltip("Stage scenes only: scene loaded after this stage's card is picked.")]
    public string nextSceneName = "";
    [Tooltip("Stages in the chapter (the last one is the boss). Shown in the HUD.")]
    public int totalStages = 5;

    [Tooltip("Index 0 = Stage 1 monsters parent, 1 = Stage 2 monsters parent, 2 = Stage 3 boss parent. In a Stage scene, only this stage's group.")]
    public GameObject[] stageGroups;

    public CardDraftUI cardDraftUI;
    public ResultUI resultUI;
    public TMP_Text hudText;

    int currentStage;
    int monstersRemaining;
    bool runEnded;

    bool IsStageScene => stageNumber > 0;
    public bool RunEnded => runEnded;

    [Tooltip("Seconds between the boss / player dying and the result screen, so the death plays out.")]
    public float resultDelay = 1.2f;
    [Tooltip("Hearts restored when a stage's last monster falls.")]
    public float stageClearHeal = 2f;
    [Tooltip("Tutorial scene: no gold, no card draft - TutorialController drives the flow.")]
    public bool tutorial;

    // Closing the window (or the quit button) in the middle of a run still banks the gold collected
    // so far - it used to be lost, because gold was only deposited when a run ended.
    void OnApplicationQuit()
    {
        if (!IsStageScene || tutorial || runEnded || PlayerStats.Instance == null) return;
        runEnded = true;
        MetaProgress.AddGold(PlayerStats.Instance.Gold);
    }

    void Awake()
    {
        Instance = this;
        // The card draft pauses time; make sure a fresh scene never starts paused.
        GameClock.ResetAll();
        StyleHud();
    }

    // The HUD sits straight on the level (bright sky, trees), so plain small white text was
    // nearly invisible: make it bigger and put a translucent dark plate behind it.
    RectTransform hudBacking;
    const float HudPadX = 20f, HudPadY = 14f;

    void StyleHud()
    {
        if (hudText == null) return;
        hudText.fontSize = UISkin.FontBody;
        hudText.outlineWidth = 0.18f; // a dark edge: the cream letters stay crisp on the wood grain
        hudText.outlineColor = new Color32(24, 14, 10, 255);
        hudText.raycastTarget = false; // the HUD must never swallow a click meant for the game
        hudText.color = UISkin.OverlayText; // cream on the wood plate
        hudText.lineSpacing = 6f;
        hudText.textWrappingMode = TextWrappingModes.NoWrap;
        hudText.overflowMode = TextOverflowModes.Overflow;
        hudText.alignment = TextAlignmentOptions.TopLeft; // the plate is fitted from the top-left
        var rt = hudText.rectTransform;
        rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 420f), Mathf.Max(rt.sizeDelta.y, 140f));

        var go = new GameObject("HudBacking", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        hudBacking = (RectTransform)go.transform;
        hudBacking.SetParent(rt.parent, false);
        hudBacking.SetSiblingIndex(rt.GetSiblingIndex()); // drawn just behind the text
        hudBacking.anchorMin = rt.anchorMin;
        hudBacking.anchorMax = rt.anchorMax;
        hudBacking.pivot = new Vector2(0f, 1f);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = UISkin.OverlayBack;
        if (UISkin.Instance != null) UISkin.Apply(img, UISkin.Instance.slate, 4f, Color.white); // the clean wood-rimmed plate: the vine one's repeated border broke into stubs // opaque: the see-through plate let the trees show through the text
        img.raycastTarget = false;
    }

    // Fits the plate to the text's current size (called whenever the text changes).
    void FitHudBacking()
    {
        if (hudBacking == null) return;
        var rt = hudText.rectTransform;
        hudText.ForceMeshUpdate();
        Vector2 size = hudText.GetRenderedValues(false);
        // Top-left corner of the text box, whatever the text's own pivot is.
        Vector2 topLeft = rt.anchoredPosition + new Vector2(-rt.pivot.x * rt.rect.width, (1f - rt.pivot.y) * rt.rect.height);
        hudBacking.anchoredPosition = topLeft + new Vector2(-HudPadX, HudPadY);
        hudBacking.sizeDelta = new Vector2(size.x + HudPadX * 2f, size.y + HudPadY * 2f);
    }

    SetHUD setHud;

    void Start()
    {
        currentStage = IsStageScene ? stageNumber : 1;
        if (IsStageScene && !tutorial) RunStats.Stage = Mathf.Max(RunStats.Stage, stageNumber);
        if (IsStageScene && !tutorial && hudText != null && hudText.canvas != null)
            StageTitle.Show(hudText.canvas, hudText.font, stageNumber); // "스테이지 1-2" at the top centre, then gone
        monstersRemaining = CountMonsters(stageGroups[0]);
        AddMinimap();
        if (IsStageScene && !tutorial && RouteProgress.TryTakeNextRoute(out bool nextRouteHunt))
            StartCoroutine(ApplySavedRoute(nextRouteHunt));
        if (IsStageScene && !tutorial && StageMap.Instance != null && StageMap.Instance.verticalLayout)
            StartCoroutine(SetupVerticalEncounters());
        if (IsStageScene && !tutorial && (stageNumber == 1 || stageNumber == 3) && StageMap.Instance != null)
            StartCoroutine(SetupHorizontalEncounters());
        // Side-scrolling scenes: dash charges as a row of bars right under the hearts; the text plate moves
        // down to make room (hearts end 66 px from the top, the bars take 76-112, the plate starts at 122).
        if (StageMap.Instance != null && hudText != null && hudText.canvas != null)
        {
            hudText.rectTransform.anchoredPosition = new Vector2(20f + HudPadX, -136f); // plate left edge in line with the hearts and bars
            DashBar.Create(hudText.rectTransform.parent, new Vector2(20f, -76f), hudText.font);
        }
        if (IsStageScene && hudText != null && hudText.canvas != null)
        {
            PauseMenu.Create(hudText.canvas.rootCanvas, hudText.font);
            if (!tutorial && hudBacking != null)
            {
                setHud = SetHUD.Create((RectTransform)hudText.canvas.rootCanvas.transform, hudText.font); // the build's sets, bottom-left
                setHud.transform.SetAsFirstSibling(); // under the popups (card draft, result board)
            }
            if (!tutorial && StageMap.Instance != null) StageExit.Create(stageNumber); // the log out (and in) - see StageExit
            if (!tutorial && StageMap.Instance != null && stageNumber >= 2 && stageNumber < totalStages) StartCoroutine(SetupTraps()); // none in the boss arena
            MonsterTracker.Create(hudText.canvas.rootCanvas);
            // Q / E / R bar for the run skills picked from the drafts.
            if (!tutorial && PlayerStats.Instance != null && PlayerStats.Instance.Job != CardPool.Job.Any)
                SkillHUD.Create(hudText.canvas.rootCanvas, hudText.font);
            if (!tutorial) BuildSheet.Create(hudText.canvas.rootCanvas, hudText.font); // Tab: stats and cards, mid-run
        }
        RefreshHud();
    }

    // Stage scenes: minimap in the top-right corner (hearts and HUD text stay top-left).
    void AddMinimap()
    {
        if (StageMap.Instance == null || hudText == null) return;
        var canvas = hudText.canvas != null ? hudText.canvas.rootCanvas : null;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (canvas == null || playerObj == null) return;
        StageMinimap.Create(canvas, StageMap.Instance, playerObj.transform, hudText.font);
    }

    // The result board is a clean final screen, not another layer over the combat HUD.
    void SetGameplayHudVisible(bool visible)
    {
        if (hudText != null) hudText.gameObject.SetActive(visible);
        if (hudBacking != null) hudBacking.gameObject.SetActive(visible);
        if (setHud != null) setHud.gameObject.SetActive(visible);
        SetHudObjects<HeartHUD>(visible);
        SetHudObjects<DashBar>(visible);
        SetHudObjects<SkillHUD>(visible);
        SetHudObjects<MonsterTracker>(visible);
        if (StageMinimap.Instance != null) StageMinimap.Instance.gameObject.SetActive(visible);
    }

    static void SetHudObjects<T>(bool visible) where T : MonoBehaviour
    {
        foreach (var item in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
            item.gameObject.SetActive(visible);
    }

    static int CountMonsters(GameObject group)
    {
        int count = 0;
        foreach (var e in group.GetComponentsInChildren<Enemy>())
            if (!e.isBoss) count++;
        return count;
    }

    // Gold is the only HUD value that changes on its own; rebuilding the text (and the
    // plate behind it) every frame was wasted work, so only redraw when it changes.
    int shownGold = -1;

    void Update()
    {
        // Play time for the result screen: running, not paused (card draft / pause menu / result screen).
        if (IsStageScene && !tutorial && !runEnded && !GameClock.Paused) RunStats.PlayTime += Time.unscaledDeltaTime;
        if (PlayerStats.Instance != null && PlayerStats.Instance.Gold != shownGold) RefreshHud();
    }

    void RefreshHud()
    {
        if (hudText == null || PlayerStats.Instance == null) return;
        shownGold = PlayerStats.Instance.Gold;
        int total = IsStageScene ? totalStages : 3;
        bool bossStage = currentStage >= total;
        // A small dim label and a big bright value per line, so each number is found at a glance. The stage itself
        // is announced at the top centre on entering (StageTitle); the plate keeps what changes while playing.
        // Labels share the value's 32 px (in-between sizes smear the pixel font): the text table gives them a dimmer colour.
        string lines = "";
        if (tutorial) lines = Loc.T("hud.tutorial");
        if (IsStageScene && Corruption.Level > 0) lines += Loc.F("hud.corruption", Corruption.Level);
        if (IsStageScene && stageNumber == 1 && RunStats.CardDrafts == 0 && !string.IsNullOrEmpty(PlayerStats.Instance.StarterCardName))
            lines += Loc.F("hud.starter", PlayerStats.Instance.StarterCardName);
        // Big maps: how many are still out there (the minimap shows where); red when it's the last few.
        int left = Mathf.Max(0, monstersRemaining);
        if (!bossStage) lines += Loc.F("hud.enemies_left", UiTheme.Html(left <= 3 ? "hurt" : "white"), left);
        // HP is shown as hearts by HeartHUD.
        hudText.text = $"{lines}<color={UiTheme.Html("gold")}>{Currency.Amount(PlayerStats.Instance.Gold)}</color>";
        FitHudBacking();
    }

    public void RefreshHudNow() => RefreshHud();

    System.Collections.IEnumerator SetupVerticalEncounters()
    {
        // Route placement runs one frame after Start; build encounter seals after it settles.
        yield return new WaitForSeconds(0.25f);
        if (StageMap.Instance != null && stageGroups != null && stageGroups.Length > 0)
            VerticalEncounterController.Create(StageMap.Instance, stageGroups[0]);
    }

    System.Collections.IEnumerator SetupTraps()
    {
        // After the exit is placed, so traps keep away from it.
        yield return new WaitForSeconds(0.3f);
        TrapHazard.SpawnForStage(stageNumber);
    }

    System.Collections.IEnumerator SetupHorizontalEncounters()
    {
        // StageRandomizer has already spread the first stage's monsters by this point.
        yield return new WaitForSeconds(0.25f);
        if (StageMap.Instance != null && stageGroups != null && stageGroups.Length > 0)
            HorizontalEncounterController.Create(StageMap.Instance, stageGroups[0]);
    }

    System.Collections.IEnumerator ApplySavedRoute(bool hunt)
    {
        // Let StageRandomizer finish its Start work before moving the selected route's foes.
        yield return null;
        ApplyRoute(hunt, false);
        while (StageTitle.Showing || LeafWipe.Busy || ScreenFader.Switching) yield return null;
        if (hudText != null && hudText.canvas != null)
            StageBanner.Show(hudText.canvas.rootCanvas, hudText.font, hunt ? Loc.T("route.hunt") : Loc.T("route.safe"),
                             hunt ? Loc.T("route.hunt.hint") : Loc.T("route.safe.hint"), 1.2f);
    }

    float routeGoldMultiplier = 1f;
    float routeGoldRemainder;

    // Called by the route panel before combat starts. The quiet route has 15% fewer foes.
    // The hunt route keeps every foe, turns one more into an elite, and pays 25% more gold.
    public void ApplyRoute(bool huntRoute, bool announce = true)
    {
        if (stageGroups == null || stageGroups.Length == 0 || stageGroups[0] == null) return;
        var foes = new System.Collections.Generic.List<Enemy>();
        foreach (var enemy in stageGroups[0].GetComponentsInChildren<Enemy>())
            if (enemy != null && !enemy.isBoss && !enemy.IsDead) foes.Add(enemy);

        bool milestone = false;
        if (huntRoute) milestone = RouteProgress.ChooseHunt();
        else RouteProgress.ChooseSafe();
        if (huntRoute)
        {
            routeGoldMultiplier = 1.25f;
            var candidates = foes.FindAll(e => !e.IsElite);
            if (candidates.Count > 0) candidates[RunRng.Range(0, candidates.Count)].MakeElite();
            string reward = milestone ? Loc.T("route.hunt.chosen_rare") : Loc.T("route.hunt.chosen");
            if (announce) StageBanner.Show(hudText.canvas.rootCanvas, hudText.font, Loc.T("route.hunt"), reward, 1.15f);
        }
        else
        {
            int skip = Mathf.Min(Mathf.Max(1, Mathf.FloorToInt(foes.Count * 0.15f)), Mathf.Max(0, foes.Count - 1));
            for (int i = 0; i < skip; i++) foes[foes.Count - 1 - i].gameObject.SetActive(false);
            if (announce) StageBanner.Show(hudText.canvas.rootCanvas, hudText.font, Loc.T("route.safe"), Loc.F("route.safe.chosen", skip), 1.15f);
        }
        // A stage route also changes platforms and where the fights happen; it is not only a reward menu.
        var activeFoes = foes.FindAll(e => e != null && e.gameObject.activeInHierarchy);
        StageRouteLayout.Apply(StageMap.Instance, activeFoes, huntRoute);
        monstersRemaining = CountMonsters(stageGroups[0]);
        RefreshHud();
    }

    public void OnMonsterDefeated(int goldReward)
    {
        if (runEnded) return;
        if (tutorial) { monstersRemaining--; RefreshHud(); return; }
        int bonus = PlayerStats.Instance.OnKill(); // lifesteal / greed cards
        int routeGold = goldReward;
        if (routeGoldMultiplier > 1f)
        {
            routeGoldRemainder += goldReward * (routeGoldMultiplier - 1f);
            int extra = Mathf.FloorToInt(routeGoldRemainder);
            routeGoldRemainder -= extra;
            routeGold += extra;
        }
        PlayerStats.Instance.AddGold(routeGold + bonus);
        Sfx.Play("coin");

        monstersRemaining--;
        if (monstersRemaining <= 0) StartCoroutine(StageCleared());
        else RefreshHud();
    }

    // All monsters must be gone first. Then a nearby chest appears; opening it deals three cards.
    System.Collections.IEnumerator StageCleared()
    {
        Sfx.Play("stage_clear");
        HitStop.Slow(0.45f, 0.35f); // the last kill hangs in the air for a moment
        float clearHeal = stageClearHeal + PlayerStats.Instance.ClearHealBonus; // 털가죽 세트
        PlayerStats.Instance.Heal(clearHeal);
        if (hudText != null && hudText.canvas != null)
            StageBanner.Show(hudText.canvas.rootCanvas, hudText.font, Loc.T("stage.clear"), Loc.F("stage.clear_heal", clearHeal), 1.1f);
        yield return new WaitForSecondsRealtime(RunStats.CardDrafts == 0 ? 1.6f : 0.9f);
        while (PauseMenu.IsOpen || CardDraftUI.IsPaused) yield return null; // don't open the draft under the pause menu / another draft
        if (runEnded) yield break;
        if (!CardRewardChest.Create(() => cardDraftUI.Show(OnCardChosen)))
            cardDraftUI.Show(OnCardChosen);
    }

    void OnCardChosen()
    {
        if (IsStageScene)
        {
            // Stage 1-3: card first, then choose the route that shapes the next normal stage.
            if (!tutorial && stageNumber < totalStages - 1 && hudText != null && hudText.canvas != null)
            {
                StageRouteChoiceUI.Show(this, hudText.canvas.rootCanvas, hudText.font, ChooseNextRoute);
                return;
            }
            LoadNextStage();
            return;
        }

        currentStage++;
        if (currentStage - 1 < stageGroups.Length)
            monstersRemaining = CountMonsters(stageGroups[currentStage - 1]);
        RefreshHud();
    }

    void ChooseNextRoute(bool hunt)
    {
        RouteProgress.SetNextRoute(hunt);
        LoadNextStage();
    }

    void LoadNextStage()
    {
        PlayerStats.Instance.SaveForNextStage();
        // Out through the forest: fireflies lead to a hollow log; crawling in loads the next stage. Without a
        // stage map to put the log on, straight to the next scene as before.
        if (StageExit.Instance == null || !StageExit.Instance.Open(nextSceneName)) SceneLoader.Load(nextSceneName);
    }

    public void OnBossDefeated(int goldReward)
    {
        if (runEnded) return;
        PlayerStats.Instance.AddGold(goldReward + PlayerStats.Instance.OnKill());
        Sfx.Play("coin");
        EndRun(true);
    }

    public void OnPlayerDied()
    {
        if (runEnded) return;
        EndRun(false);
    }

    void EndRun(bool win)
    {
        runEnded = true;
        // This run's gold goes into the saved wallet, spent at the village blacksmith.
        MetaProgress.AddGold(PlayerStats.Instance.Gold);
        int unlocked = win ? Corruption.RegisterClear(Corruption.Level) : -1;
        if (win)
        {
            // Records for the world map (a reason to go again besides the next 타락도 tier).
            GameSave.Data.wins++;
            GameSave.Data.bestClear = Mathf.Max(GameSave.Data.bestClear, Corruption.Level);
            GameSave.Save();
        }
        StartCoroutine(ShowResult(win, PlayerStats.Instance.Gold, unlocked));
    }

    // Let the death animation / boss collapse play first, then freeze the level behind the
    // result screen (its buttons run on unscaled time; SceneLoader unpauses on leaving).
    System.Collections.IEnumerator ShowResult(bool win, int gold, int unlockedCorruption)
    {
        yield return new WaitForSecondsRealtime(resultDelay);
        if (cardDraftUI != null && cardDraftUI.panel != null) cardDraftUI.panel.SetActive(false);
        SetGameplayHudVisible(false);
        resultUI.Show(win, gold, unlockedCorruption);
        Sfx.Play(win ? "victory" : "defeat");
        GameClock.Pause("result");
    }

    // Pause menu "give up": counts as a defeat - the gold so far is deposited - and goes
    // straight back to the village.
    public void GiveUp()
    {
        if (runEnded) return;
        runEnded = true;
        MetaProgress.AddGold(PlayerStats.Instance.Gold);
        SceneLoader.Load(resultUI != null ? resultUI.retrySceneName : "MainScene");
    }
}
