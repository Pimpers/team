using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to any GameObject in the Title scene (e.g. the Canvas).
// Wire the Play button's OnClick() to PlayGame(), and the Options button's OnClick() to OpenOptions().
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Must exactly match a scene name added in File > Build Settings.")]
    public string gameSceneName = "GameScene";

    [Tooltip("Must exactly match a scene name added in File > Build Settings.")]
    public string optionsSceneName = "OptionsScene";

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenOptions()
    {
        SceneManager.LoadScene(optionsSceneName);
    }
}
