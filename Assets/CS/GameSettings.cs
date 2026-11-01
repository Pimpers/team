using UnityEngine;
using UnityEngine.InputSystem;

// Player options persisted in the versioned GameSave file.
public static class GameSettings
{
    public static float MasterVolume
    {
        get => GameSave.Data.masterVolume;
        set
        {
            GameSave.Data.masterVolume = Mathf.Clamp01(value);
            GameSave.Save();
            AudioListener.volume = GameSave.Data.masterVolume;
        }
    }

    public static float MusicVolume
    {
        get => GameSave.Data.musicVolume;
        set { GameSave.Data.musicVolume = Mathf.Clamp01(value); GameSave.Save(); }
    }

    public static float SfxVolume
    {
        get => GameSave.Data.sfxVolume;
        set { GameSave.Data.sfxVolume = Mathf.Clamp01(value); GameSave.Save(); }
    }

    public static float ShakeStrength
    {
        get => GameSave.Data.shakeStrength;
        set { GameSave.Data.shakeStrength = Mathf.Clamp01(value); GameSave.Save(); }
    }

    public static bool HitStopEnabled
    {
        get => GameSave.Data.hitStopEnabled;
        set { GameSave.Data.hitStopEnabled = value; GameSave.Save(); }
    }

    public static bool Fullscreen
    {
        get => GameSave.Data.fullscreen;
        set
        {
            GameSave.Data.fullscreen = value;
            GameSave.Save();
            Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }

    // ---- What the settings screens show, in one place: the village's OptionsUI and the title's TitleSettings both
    // build their rows from these lists, so a setting is added (or relabelled) once, here.
    public struct SliderSetting { public string labelKey; public System.Func<float> get; public System.Action<float> set; public System.Action preview; }
    public struct ToggleSetting { public string onKey, offKey; public System.Func<bool> get; public System.Action<bool> set; }

    public static readonly SliderSetting[] Sliders =
    {
        new SliderSetting { labelKey = "options.volume", get = () => MasterVolume, set = v => MasterVolume = v },
        new SliderSetting { labelKey = "options.music", get = () => MusicVolume, set = v => MusicVolume = v },
        new SliderSetting { labelKey = "options.sfx", get = () => SfxVolume, set = v => SfxVolume = v },
        new SliderSetting { labelKey = "options.shake", get = () => ShakeStrength, set = v => ShakeStrength = v,
                            preview = () => CameraFollow.Shake(0.25f, 0.25f) }, // a taste of the new strength
    };
    public static readonly ToggleSetting[] Toggles =
    {
        new ToggleSetting { onKey = "options.fullscreen.on", offKey = "options.fullscreen.off", get = () => Fullscreen, set = v => Fullscreen = v },
        new ToggleSetting { onKey = "options.hitstop.on", offKey = "options.hitstop.off", get = () => HitStopEnabled, set = v => HitStopEnabled = v },
    };

    // The rebindable keys (KeyBindPanel, opened from both settings screens' "키 설정" button).
    public struct BindingSetting { public string labelKey; public GameInput.Action action; }
    public static readonly BindingSetting[] Bindings =
    {
        new BindingSetting { labelKey = "keys.attack", action = GameInput.Action.Attack },
        new BindingSetting { labelKey = "keys.dash", action = GameInput.Action.Dash },
        new BindingSetting { labelKey = "keys.jump", action = GameInput.Action.Jump },
        new BindingSetting { labelKey = "keys.skill1", action = GameInput.Action.Skill1 },
        new BindingSetting { labelKey = "keys.skill2", action = GameInput.Action.Skill2 },
        new BindingSetting { labelKey = "keys.skill3", action = GameInput.Action.Skill3 },
    };

    public static string ToggleText(ToggleSetting t) => Loc.T(t.get() ? t.onKey : t.offKey);

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySaved()
    {
        AudioListener.volume = MasterVolume;
        Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        var go = new GameObject("GameSettingsHotkeys");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Hotkeys>();
    }

    class Hotkeys : MonoBehaviour
    {
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f11Key.wasPressedThisFrame) Fullscreen = !Fullscreen;
        }
    }
}
