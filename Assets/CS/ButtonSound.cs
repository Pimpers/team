using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Click sound for a UI button. Added to every Button in the scenes by the editor tool
// AudioAssets, and by code to buttons built at runtime (pause menu).
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour, IPointerClickHandler
{
    public string sound = "ui_click";

    public void OnPointerClick(PointerEventData eventData)
    {
        var button = GetComponent<Button>();
        if (button != null && button.interactable) Sfx.Play(sound);
    }
}
