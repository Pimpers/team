using System.Collections.Generic;
using UnityEngine;

// The one owner of "is the game paused". Each screen that stops play (card draft, route choice, pause book,
// result board) asks for a pause under its own name and gives it back when it closes; the game runs again only
// once nobody holds one - so a pause book opened over another screen can't set the game running behind it.
// Hit stops and slow motion (HitStop) shape the speed while nothing holds a pause. Scene changes clear it all.
public static class GameClock
{
    static readonly HashSet<string> holds = new HashSet<string>();

    public static bool Paused => holds.Count > 0;

    public static void Pause(string who)
    {
        holds.Add(who);
        Time.timeScale = 0f;
    }

    public static void Resume(string who)
    {
        holds.Remove(who);
        if (holds.Count == 0) Time.timeScale = HitStop.Scale;
    }

    // A fresh scene (or leaving one) starts at full speed with nothing held.
    public static void ResetAll()
    {
        holds.Clear();
        HitStop.Clear();
        Time.timeScale = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => holds.Clear(); // (domain reload may be off in the editor)
}
