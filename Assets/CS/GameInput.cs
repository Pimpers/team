using UnityEngine;
using UnityEngine.InputSystem;

// The game's controls in one place, keyboard + mouse and a gamepad side by side (Input System devices read directly,
// as the rest of the project did):
//   move        A/D (W/S)  or arrows    left stick / d-pad
//   jump        Space / W / Up          A (south)
//   drop down   S / Down                d-pad down / stick down
//   attack      left click              X (west)
//   dash        right click             B (east) or right trigger
//   skills      Q / E / R               LB / RB / Y (north)
//   pause       ESC                     Start
// Aim follows the mouse; with a gamepad it is the right stick, or straight ahead when the stick is idle.
public static class GameInput
{
    const float Dead = 0.4f;

    // True once the gamepad was the last thing touched (and back to false when the mouse moves or clicks):
    // decides whether aiming follows the mouse or the stick.
    public static bool UsingGamepad { get; private set; }
    static int checkedFrame = -1;

    static Gamepad Pad
    {
        get
        {
            var pad = Gamepad.current;
            if (Time.frameCount != checkedFrame)
            {
                checkedFrame = Time.frameCount;
                var mouse = Mouse.current;
                if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                    UsingGamepad = false;
                else if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > Dead * Dead || pad.rightStick.ReadValue().sqrMagnitude > Dead * Dead || AnyButton(pad)))
                    UsingGamepad = true;
            }
            return pad;
        }
    }

    static bool AnyButton(Gamepad p) =>
        p.buttonSouth.isPressed || p.buttonWest.isPressed || p.buttonEast.isPressed || p.buttonNorth.isPressed ||
        p.leftShoulder.isPressed || p.rightShoulder.isPressed || p.rightTrigger.isPressed || p.startButton.isPressed || p.dpad.ReadValue() != Vector2.zero;

    public static Vector2 Move
    {
        get
        {
            float x = 0f, y = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            }
            var pad = Pad;
            if (pad != null)
            {
                Vector2 s = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                if (Mathf.Abs(s.x) > Dead) x += Mathf.Sign(s.x);
                if (Mathf.Abs(s.y) > Dead) y += Mathf.Sign(s.y);
            }
            return new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
        }
    }

    public static bool JumpPressed
    {
        get
        {
            var kb = Keyboard.current; var pad = Pad;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }
    }

    // Space only (and the pad's A): the top-down jump, where W / Up move instead.
    public static bool JumpButtonPressed
    {
        get { var kb = Keyboard.current; var pad = Pad; return (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame); }
    }

    public static bool DropPressed
    {
        get
        {
            var kb = Keyboard.current; var pad = Pad;
            return (kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame))
                || (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame));
        }
    }

    // Left click (the caller skips clicks over UI) or the pad's X.
    public static bool AttackClicked { get { var m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame; } }
    public static bool AttackPadPressed { get { var pad = Pad; return pad != null && pad.buttonWest.wasPressedThisFrame; } }

    public static bool DashPressed
    {
        get
        {
            var m = Mouse.current; var pad = Pad;
            return (m != null && m.rightButton.wasPressedThisFrame)
                || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame));
        }
    }

    // Skill slot 0 / 1 / 2: Q / E / R, or LB / RB / Y.
    public static bool SkillPressed(int slot)
    {
        var kb = Keyboard.current; var pad = Pad;
        bool key = kb != null && (slot == 0 ? kb.qKey : slot == 1 ? kb.eKey : kb.rKey).wasPressedThisFrame;
        bool button = pad != null && (slot == 0 ? pad.leftShoulder : slot == 1 ? pad.rightShoulder : pad.buttonNorth).wasPressedThisFrame;
        return key || button;
    }

    public static bool PausePressed
    {
        get { var kb = Keyboard.current; var pad = Pad; return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame); }
    }

    // Menus with a gamepad: puts the UI selection on `button`, so A (submit) presses it and the d-pad moves on from
    // it (the scenes' InputSystemUIInputModule handles that once something is selected).
    public static void SelectForPad(UnityEngine.UI.Selectable button)
    {
        if (button == null || Gamepad.current == null) return;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(button.gameObject);
    }

    // Where the player aims, in world space, from `origin`: the mouse, or with a gamepad the right stick
    // (`reach` units out) or straight ahead (`facing`).
    public static Vector2 AimPoint(Camera cam, Vector2 origin, float facing, float reach)
    {
        var pad = Pad;
        if (UsingGamepad || Mouse.current == null || cam == null)
        {
            Vector2 stick = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
            Vector2 dir = stick.sqrMagnitude > Dead * Dead ? stick.normalized : new Vector2(facing >= 0f ? 1f : -1f, 0f);
            return origin + dir * reach;
        }
        return cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
    }
}
