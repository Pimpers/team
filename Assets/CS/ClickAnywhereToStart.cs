using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Attach to a full-screen (invisible) UI Image with Raycast Target enabled,
// so clicking/tapping anywhere on the Title screen loads the game scene.
public class ClickAnywhereToStart : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Must exactly match a scene name added in File > Build Settings.")]
    public string gameSceneName = "GameScene";

    public void OnPointerClick(PointerEventData eventData)
    {
        Sfx.Play("ui_click"); // the Sfx player survives the scene change
        SceneLoader.Load(gameSceneName);
    }
}
