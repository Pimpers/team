using UnityEngine;

// Selected mercenary and names, persisted through GameSave.
public static class CharacterSelection
{
    public const int MaxNameLength = 10;

    public static int SelectedIndex
    {
        get => Mathf.Clamp(GameSave.Data.selectedCharacter, 0, 1);
        set
        {
            GameSave.Data.selectedCharacter = Mathf.Clamp(value, 0, 1);
            GameSave.Save();
        }
    }

    public static bool HasName(int index) => GetName(index).Length > 0;

    public static string GetName(int index)
    {
        var names = GameSave.Data.characterNames;
        if (names == null || index < 0 || index >= names.Length) return "";
        return (names[index] ?? "").Trim();
    }

    public static void SetName(int index, string name)
    {
        var names = GameSave.Data.characterNames;
        if (names == null || index < 0 || index >= names.Length) return;
        name = name == null ? "" : name.Trim();
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
        names[index] = name;
        GameSave.Save();
    }

    public static string SelectedName => GetName(SelectedIndex);
}
