using UnityEngine;
using UnityEngine.UI;

// Put on a Button to make it close the game (see GameSettings.Quit).
[RequireComponent(typeof(Button))]
public class QuitButton : MonoBehaviour
{
    void Awake() => GetComponent<Button>().onClick.AddListener(GameSettings.Quit);
}
