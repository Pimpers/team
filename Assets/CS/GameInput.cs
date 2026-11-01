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
// Attack, dash, jump and the three skills can be rebound on the keyboard / mouse (settings > 키 설정, saved in
// GameSave.keyBindings); W / Up still jump in the side view, and the gamepad buttons stay as they are.
public static class GameInput
{
    const float Dead = 0.4f;

    // ---- Rebindable keyboard / mouse actions.
    public enum Action { Attack, Dash, Jump, Skill1, Skill2, Skill3 }
    public const int MouseLeft = -1, MouseRight = -2, MouseMiddle = -3;
    public static readonly int[] DefaultBindings = { MouseLeft, MouseRight, (int)Key.Space, (int)Key.Q, (int)Key.E, (int)Key.R };
    public static int ActionCount => DefaultBindings.Length;

    // What `a` is bound to: the saved binding when it is a usable one, else the default.
    public static int Binding(Action a)
    {
        int i = (int)a;
        var saved = GameSave.Data.keyBindings;
        return saved != null && i < saved.Length && IsBindable(saved[i]) ? saved[i] : DefaultBindings[i];
    }

    // A mouse button, or a real keyboard key other than ESC (pause) and the movement keys.
    public static bool IsBindable(int code)
    {
        if (code == MouseLeft || code == MouseRight || code == MouseMiddle) return true;
        if (code <= 0 || !System.Enum.IsDefined(typeof(Key), code)) return false;
        var k = (Key)code;
        return k != Key.Escape && k != Key.A && k != Key.D && k != Key.S && k != Key.LeftArrow && k != Key.RightArrow && k != Key.DownArrow && k != Key.F11;
    }

    // Binds `a` to `code`; an action that had `code` takes `a`'s old binding, so no two actions share one.
    public static void Rebind(Action a, int code)
    {
        if (!IsBindable(code)) return;
        var b = new int[ActionCount];
        for (int i = 0; i < b.Length; i++) b[i] = Binding((Action)i);
        int old = b[(int)a];
        for (int i = 0; i < b.Length; i++) if (b[i] == code) b[i] = old;
        b[(int)a] = code;
        GameSave.Data.keyBindings = b;
        GameSave.Save();
    }

    public static void ResetBindings()
    {
        GameSave.Data.keyBindings = new int[0];
        GameSave.Save();
    }

    // "마우스 왼쪽", "Space", "Q"...
    public static string BindingName(Action a) => CodeName(Binding(a));
    public static string CodeName(int code)
    {
        if (code == MouseLeft) return Loc.T("keys.mouse_left");
        if (code == MouseRight) return Loc.T("keys.mouse_right");
        if (code == MouseMiddle) return Loc.T("keys.mouse_middle");
        var kb = Keyboard.current;
        var control = kb != null && System.Enum.IsDefined(typeof(Key), code) ? kb[(Key)code] : null;
        string name = control != null ? control.displayName : ((Key)code).ToString();
        return string.IsNullOrEmpty(name) ? ((Key)code).ToString() : name;
    }

    static bool Down(int code)
    {
        if (code < 0)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return (code == MouseLeft ? m.leftButton : code == MouseRight ? m.rightButton : m.middleButton).wasPressedThisFrame;
        }
        var kb = Keyboard.current;
        return kb != null && System.Enum.IsDefined(typeof(Key), code) && kb[(Key)code].wasPressedThisFrame;
    }

    static bool Down(Action a) => Down(Binding(a));

    // The key / mouse button pressed this frame (for the rebinding screen), or 0 when none. ESC is not returned.
    public static int AnyBindablePressed()
    {
        var m = Mouse.current;
        if (m != null)
        {
            if (m.leftButton.wasPressedThisFrame) return MouseLeft;
            if (m.rightButton.wasPressedThisFrame) return MouseRight;
            if (m.middleButton.wasPressedThisFrame) return MouseMiddle;
        }
        var kb = Keyboard.current;
        if (kb != null)
            foreach (var key in kb.allKeys)
                if (key != null && key.wasPressedThisFrame && IsBindable((int)key.keyCode)) return (int)key.keyCode;
        return 0;
    }

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
            return Down(Action.Jump) || (kb != null && (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }
    }

    // The jump key only (Space by default, and the pad's A): the top-down jump, where W / Up move instead.
    public static bool JumpButtonPressed
    {
        get { var pad = Pad; return Down(Action.Jump) || (pad != null && pad.buttonSouth.wasPressedThisFrame); }
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

    // The attack binding when it is a mouse button (the caller skips clicks over UI) - left click by default;
    // AttackPadPressed is the pad's X, or the attack binding when it was moved to a keyboard key (no UI check).
    public static bool AttackClicked { get { int b = Binding(Action.Attack); return b < 0 && Down(b); } }
    public static bool AttackPadPressed
    {
        get { var pad = Pad; int b = Binding(Action.Attack); return (pad != null && pad.buttonWest.wasPressedThisFrame) || (b > 0 && Down(b)); }
    }

    public static bool DashPressed
    {
        get
        {
            var pad = Pad;
            return Down(Action.Dash) || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame));
        }
    }

    // Skill slot 0 / 1 / 2: Q / E / R (or what they were rebound to), or LB / RB / Y.
    public static bool SkillPressed(int slot)
    {
        var pad = Pad;
        bool key = Down((Action)((int)Action.Skill1 + Mathf.Clamp(slot, 0, 2)));
        bool button = pad != null && (slot == 0 ? pad.leftShoulder : slot == 1 ? pad.rightShoulder : pad.buttonNorth).wasPressedThisFrame;
        return key || button;
    }

    // Moving a dialogue on: a click, Space, Enter, or the pad's A.
    public static bool SubmitPressed
    {
        get
        {
            var kb = Keyboard.current; var m = Mouse.current; var pad = Pad;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }
    }

    // Held: the build sheet (BuildSheet) shows while Tab, or the pad's View / Select button, is down.
    public static bool BuildSheetHeld
    {
        get { var kb = Keyboard.current; var pad = Pad; return (kb != null && kb.tabKey.isPressed) || (pad != null && pad.selectButton.isPressed); }
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

    // Left / right on the pad's d-pad or stick this frame (-1 / 0 / 1): stepping through things that aren't UI
    // buttons, like the lobby's cats.
    public static int PadNavX
    {
        get
        {
            var pad = Pad;
            if (pad == null) return 0;
            if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) return -1;
            if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) return 1;
            return 0;
        }
    }

    public static bool PadSubmitPressed { get { var pad = Pad; return pad != null && pad.buttonSouth.wasPressedThisFrame; } }
    public static bool PadBackPressed { get { var pad = Pad; return pad != null && pad.buttonEast.wasPressedThisFrame; } }

    // With a gamepad in use, keeps the UI selection inside `scope`: when nothing (or something hidden or outside it)
    // is selected, selects `preferred`, or else the first usable Selectable under `scope`. The d-pad then moves on
    // from there (EventSystem navigation). Call every frame from a screen's Update.
    public static void KeepPadSelection(Transform scope, UnityEngine.UI.Selectable preferred = null)
    {
        var pad = Pad;
        if (pad == null || !UsingGamepad || scope == null) return;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return;
        var current = es.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy && current.transform.IsChildOf(scope)) return;
        var pick = preferred != null && preferred.isActiveAndEnabled && preferred.IsInteractable() ? preferred : null;
        if (pick == null)
            foreach (var s in scope.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                if (s.IsInteractable() && s.navigation.mode != UnityEngine.UI.Navigation.Mode.None) { pick = s; break; }
        if (pick != null) es.SetSelectedGameObject(pick.gameObject);
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
