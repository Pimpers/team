using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Every Button answers a press the same way, whoever built it (scene, builder or code): it sinks while the mouse
// is down on it and pops back on release, so a press is felt even where the click does nothing visible at once.
// One object for the whole game (made on load, kept across scenes) - it looks only on the frame of a press, by
// asking the EventSystem what is under the pointer; nothing is added to the buttons themselves.
// World things that are clicked (the lobby's cats, the map's forest, the village's ship) call World().
public class PressFeedback : MonoBehaviour
{
    const float Down = 0.92f, Pop = 1.08f, PopTime = 0.12f;

    Transform held, popping;
    Vector3 heldScale, popScale;
    float popT;
    static readonly List<RaycastResult> hits = new List<RaycastResult>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("PressFeedback");
        DontDestroyOnLoad(go);
        go.AddComponent<PressFeedback>();
    }

    // A click on something in the world: a ring where it landed and the button click.
    public static void World(Vector3 at)
    {
        Vfx.Play("ring_small", at, 1f);
        Sfx.Play("ui_click");
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) Press(ButtonUnder(mouse.position.ReadValue()));
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame) Release();
        if (GameInput.PadSubmitPressed) // the pad's A on the selected button: down and up at once
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            var b = selected != null ? selected.GetComponent<Button>() : null;
            if (b != null && b.interactable) { Press(b.transform); Release(); }
        }

        if (popping == null) return; // (also when the button was destroyed by its own click)
        popT += Time.unscaledDeltaTime; // menus are up while the game is paused
        float k = Mathf.Clamp01(popT / PopTime);
        popping.localScale = popScale * Mathf.Lerp(Pop, 1f, k);
        if (k >= 1f) popping = null;
    }

    void Press(Transform t)
    {
        if (t == null) return;
        if (popping == t) { t.localScale = popScale; popping = null; } // pressed again mid-pop: from its real size
        held = t;
        heldScale = t.localScale;
        t.localScale = heldScale * Down;
    }

    void Release()
    {
        if (held == null) { held = null; return; }
        popping = held;
        popScale = heldScale;
        popT = 0f;
        held = null;
    }

    static Transform ButtonUnder(Vector2 screen)
    {
        var es = EventSystem.current;
        if (es == null) return null;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
        if (hits.Count == 0) return null;
        var b = hits[0].gameObject.GetComponentInParent<Button>();
        return b != null && b.interactable ? b.transform : null;
    }
}
