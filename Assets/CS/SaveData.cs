using System;
using System.IO;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int version = GameSave.CurrentVersion;
    public int gold;
    public int attackLevel;
    public int speedLevel;
    public int hpLevel;
    public int rerollLevel; // 행운의 방울: free card rerolls per run (MetaProgress.Upgrade.Reroll)
    public bool seenSetTip; // the first card draft's line on what # tags and sets are
    public string seenTips = ""; // OneTimeTip ids already shown, comma separated
    public int smithVisits;  // the blacksmith introduces himself on the first visit only
    public int wins;         // runs won (boss beaten) - shown on the world map
    public int bestClear = -1; // highest 타락도 level a run was won at (-1 = never won)
    public int selectedCharacter;
    public string[] characterNames = { "", "" };
    public int corruptionUnlocked;
    public int corruptionSelected;
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public float shakeStrength = 1f;
    public bool hitStopEnabled = true;
    public bool colorblind;   // 색약 모드: enemy / own markers in a red-green safe pair (UiTheme)
    public bool largeText;    // 큰 글씨: the HUD plate and banner lines at 36 instead of 24
    public bool fullscreen = true;
    public bool tutorialDone;
    // Keyboard / mouse bindings for GameInput.Action (attack, dash, jump, skill 1-3): a Key enum value, or
    // GameInput.MouseLeft / MouseRight. Empty = the defaults (GameInput.DefaultBindings). Added in version 2.
    public int[] keyBindings = new int[0];
}

// Versioned persistent save with one backup. Existing PlayerPrefs values are imported
// only when no JSON save exists, preserving players from earlier builds.
public static class GameSave
{
    // 1: first JSON save. 2: rerollLevel, seenSetTip, wins, bestClear, smithVisits checked, keyBindings added.
    public const int CurrentVersion = 2;
    static SaveData data;
    static bool loaded;
    static bool? playtest;

    static bool IsPlaytest
    {
        get
        {
            if (playtest.HasValue) return playtest.Value;
            playtest = false;
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg == "-fullRunPlaytest" || arg == "-backgroundPlaytest" || arg == "-gameplayPlaytest") { playtest = true; break; }
            return playtest.Value;
        }
    }

    public static SaveData Data
    {
        get { EnsureLoaded(); return data; }
    }

    static string SavePath => Path.Combine(Application.persistentDataPath, "cats-smash-save.json");
    static string BackupPath => SavePath + ".bak";
    static string TempPath => SavePath + ".tmp";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize() => EnsureLoaded();

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        if (IsPlaytest)
        {
            data = new SaveData();
            UpgradeAndClamp();
            return;
        }
        data = Read(SavePath) ?? Read(BackupPath);
        if (data == null)
        {
            data = MigratePlayerPrefs();
            Save();
        }
        UpgradeAndClamp();
    }

    static SaveData Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return Parse(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Save read failed: {Path.GetFileName(path)} ({e.Message})");
            return null;
        }
    }

    static SaveData MigratePlayerPrefs()
    {
        var value = new SaveData
        {
            gold = PlayerPrefs.GetInt("meta_gold", 0),
            attackLevel = PlayerPrefs.GetInt("meta_attack_level", 0),
            speedLevel = PlayerPrefs.GetInt("meta_speed_level", 0),
            hpLevel = PlayerPrefs.GetInt("meta_hp_level", 0),
            selectedCharacter = PlayerPrefs.GetInt("selected_character", 0),
            corruptionUnlocked = PlayerPrefs.GetInt("corruption_unlocked", 0),
            corruptionSelected = PlayerPrefs.GetInt("corruption_selected", 0),
            masterVolume = PlayerPrefs.GetFloat("master_volume", 1f),
            musicVolume = PlayerPrefs.GetFloat("music_volume", 1f),
            sfxVolume = PlayerPrefs.GetFloat("sfx_volume", 1f),
            shakeStrength = PlayerPrefs.GetFloat("screen_shake", 1f),
            hitStopEnabled = PlayerPrefs.GetInt("hit_stop", 1) == 1,
            fullscreen = PlayerPrefs.HasKey("fullscreen") ? PlayerPrefs.GetInt("fullscreen") == 1 : Screen.fullScreen,
            tutorialDone = PlayerPrefs.GetInt("tutorial_done", 0) == 1
        };
        value.characterNames[0] = PlayerPrefs.GetString("mercenary_name_0", "");
        value.characterNames[1] = PlayerPrefs.GetString("mercenary_name_1", "");
        RefundLegacyExcess(value, ref value.attackLevel, 3, 25);
        RefundLegacyExcess(value, ref value.speedLevel, 3, 20);
        RefundLegacyExcess(value, ref value.hpLevel, 5, 12);
        return value;
    }

    static void RefundLegacyExcess(SaveData value, ref int level, int maximum, int baseCost)
    {
        if (level <= maximum) return;
        for (int current = maximum; current < level; current++) value.gold += baseCost * (current + 1);
        level = maximum;
    }

    // A save file's text to data, upgraded to CurrentVersion and clamped; null when it isn't a save.
    public static SaveData Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"version\"")) return null; // {} would read as a new save
        var value = JsonUtility.FromJson<SaveData>(json);
        if (value == null || value.version <= 0) return null;
        Migrate(value, json);
        Clamp(value);
        return value;
    }

    // Older saves miss fields added since. JsonUtility leaves a missing field at its initializer, but an older build
    // could also have written a field before it meant what it means now, so each step fixes up what that version
    // could not have known. `json` is the raw text, to tell "missing" from "written as 0".
    static void Migrate(SaveData value, string json)
    {
        if (value.version < 2)
        {
            if (!json.Contains("\"wins\"")) value.wins = value.corruptionUnlocked; // each unlocked tier took a win
            if (!json.Contains("\"bestClear\"")) value.bestClear = value.wins > 0 ? Mathf.Max(0, value.corruptionUnlocked - 1) : -1;
            if (value.wins <= 0) { value.wins = 0; value.bestClear = -1; }
            if (!json.Contains("\"rerollLevel\"")) value.rerollLevel = 0;
            if (!json.Contains("\"seenSetTip\"")) value.seenSetTip = value.tutorialDone; // a returning player has seen sets
            if (!json.Contains("\"smithVisits\"")) value.smithVisits = 0;
            value.keyBindings = new int[0];
        }
    }

    static void UpgradeAndClamp() => Clamp(data);

    static void Clamp(SaveData data)
    {
        if (data.characterNames == null || data.characterNames.Length < 2)
        {
            var old = data.characterNames;
            data.characterNames = new[] { old != null && old.Length > 0 ? old[0] : "", "" };
        }
        data.version = CurrentVersion;
        data.gold = Mathf.Max(0, data.gold);
        data.attackLevel = Mathf.Clamp(data.attackLevel, 0, 3);
        data.speedLevel = Mathf.Clamp(data.speedLevel, 0, 3);
        data.hpLevel = Mathf.Clamp(data.hpLevel, 0, 5);
        data.rerollLevel = Mathf.Clamp(data.rerollLevel, 0, 2);
        data.selectedCharacter = Mathf.Clamp(data.selectedCharacter, 0, 1);
        data.corruptionUnlocked = Mathf.Clamp(data.corruptionUnlocked, 0, Corruption.MaxLevel);
        data.corruptionSelected = Mathf.Clamp(data.corruptionSelected, Corruption.Easy, data.corruptionUnlocked);
        data.masterVolume = Mathf.Clamp01(data.masterVolume);
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        data.shakeStrength = Mathf.Clamp01(data.shakeStrength);
        data.wins = Mathf.Max(0, data.wins);
        data.smithVisits = Mathf.Max(0, data.smithVisits);
        data.bestClear = Mathf.Clamp(data.bestClear, -1, Corruption.MaxLevel);
        if (data.keyBindings == null) data.keyBindings = new int[0];
    }

    public static void Save()
    {
        EnsureLoaded();
        if (IsPlaytest) return;
        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath))
            {
                try { File.Replace(TempPath, SavePath, BackupPath); }
                catch
                {
                    File.Copy(SavePath, BackupPath, true);
                    File.Copy(TempPath, SavePath, true);
                    File.Delete(TempPath);
                }
            }
            else File.Move(TempPath, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"Save write failed: {e.Message}");
        }
    }

#if UNITY_EDITOR
    public static void ResetForTests(SaveData value = null)
    {
        data = value ?? new SaveData();
        loaded = true;
        playtest = true; // tests never write the player's real save file
    }
#endif
}
