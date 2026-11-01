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
    public int selectedCharacter;
    public string[] characterNames = { "", "" };
    public int corruptionUnlocked;
    public int corruptionSelected;
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public float shakeStrength = 1f;
    public bool hitStopEnabled = true;
    public bool fullscreen = true;
    public bool tutorialDone;
}

// Versioned persistent save with one backup. Existing PlayerPrefs values are imported
// only when no JSON save exists, preserving players from earlier builds.
public static class GameSave
{
    public const int CurrentVersion = 1;
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
            var value = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            return value != null && value.version > 0 ? value : null;
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

    static void UpgradeAndClamp()
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
        data.corruptionSelected = Mathf.Clamp(data.corruptionSelected, 0, data.corruptionUnlocked);
        data.masterVolume = Mathf.Clamp01(data.masterVolume);
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        data.shakeStrength = Mathf.Clamp01(data.shakeStrength);
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
    }
#endif
}
