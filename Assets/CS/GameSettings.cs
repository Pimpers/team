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
