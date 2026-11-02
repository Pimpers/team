using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// The blacksmith's speech bubble: a press while a line types finishes it, the next press goes on - from the
// keyboard (Enter) and the pad (A) alike; and on this narrow test screen (640 x 480, 4:3) the bubble stays clear of
// the village's right-hand column (the goals board and the buttons).
public class SmithBubblePlayModeTests
{
    [TearDown]
    public void Unpause() => GameClock.ResetAll();

    static IEnumerator OpenSmith(System.Action<BlacksmithUI> got)
    {
        SceneManager.LoadScene("MainScene");
        yield return new WaitForSecondsRealtime(1f);
        var smith = Object.FindAnyObjectByType<BlacksmithUI>(FindObjectsInactive.Include);
        Assert.That(smith, Is.Not.Null);
        smith.Open();
        yield return null;
        yield return null; // past the frame it opened on (that press is ignored)
        got(smith);
    }

    static IEnumerator Press<TState>(InputDevice device, TState down, TState up) where TState : struct, IInputStateTypeInfo
    {
        InputSystem.QueueStateEvent(device, down);
        yield return null; // processed at the start of this frame: wasPressedThisFrame while BlacksmithUI updates
        InputSystem.QueueStateEvent(device, up);
        yield return null;
    }

    static IEnumerator SkipThenNext(System.Func<IEnumerator> press)
    {
        BlacksmithUI smith = null;
        yield return OpenSmith(s => smith = s);
        Assert.That(smith.IsTalking, Is.True);
        Assert.That(smith.LineTyped, Is.False, "the line should still be typing");
        int line = smith.LineIndex;
        yield return press();
        Assert.That(smith.LineTyped, Is.True, "the first press finishes the line");
        Assert.That(smith.LineIndex, Is.EqualTo(line), "...without moving on");
        yield return press();
        Assert.That(!smith.IsTalking || smith.LineIndex == line + 1, Is.True, "the next press goes on");
    }

    [UnityTest]
    public IEnumerator Keyboard_FinishesTheLine_ThenGoesOn()
    {
        // A batch run has no focus, and keyboards are switched off without it (pads run in the background): let
        // input through regardless for this test.
        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorBehavior = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var kb = InputSystem.AddDevice<Keyboard>();
        kb.MakeCurrent(); // (there may already be a keyboard: GameInput reads Keyboard.current)
        try { yield return SkipThenNext(() => Press(kb, new KeyboardState(Key.Enter), new KeyboardState())); }
        finally
        {
            InputSystem.RemoveDevice(kb);
            settings.backgroundBehavior = background;
            settings.editorInputBehaviorInPlayMode = editorBehavior;
        }
    }

    [UnityTest]
    public IEnumerator Gamepad_FinishesTheLine_ThenGoesOn()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        pad.MakeCurrent();
        try { yield return SkipThenNext(() => Press(pad, new GamepadState().WithButton(GamepadButton.South), new GamepadState())); }
        finally { InputSystem.RemoveDevice(pad); }
    }

    [UnityTest]
    public IEnumerator Bubble_StaysClearOfTheRightColumn_OnA4By3Screen()
    {
        BlacksmithUI smith = null;
        yield return OpenSmith(s => smith = s);
        yield return new WaitForSecondsRealtime(0.3f);
        Canvas.ForceUpdateCanvases();
        Rect bubble = WorldRect(smith.BubbleRect);
        var goals = GameObject.Find("VillageGoals");
        if (goals != null) Assert.That(bubble.Overlaps(WorldRect((RectTransform)goals.transform)), Is.False, "the bubble runs under the goals board");
        foreach (var b in Object.FindObjectsByType<Button>())
        {
            if (!b.isActiveAndEnabled || b.transform.IsChildOf(smith.transform)) continue;
            Assert.That(bubble.Overlaps(WorldRect((RectTransform)b.transform)), Is.False, "the bubble covers the button " + b.name);
        }
    }

    static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }
}
