using System.Collections.Generic;
using UnityEngine;

// The game's text table: every line the player reads that the code builds lives in Resources/Strings/ko.txt as
// `key = text`, so wording is changed (or a language added) in one file instead of across the scripts.
//   - "\n" in a value is a line break; lines starting with # are notes.
//   - Loc.F fills {0}, {1}... with string.Format (format specs work: {0:0.#}).
//   - $name in a value is a UiTheme rich-text colour (<color=$gold>), so the palette has one home too.
// A missing key shows as the key itself (and is reported once), which keeps a typo visible instead of blank.
public static class Loc
{
    public const string DefaultLanguage = "ko";
    static Dictionary<string, string> table;
    static readonly HashSet<string> reported = new HashSet<string>();

    static Dictionary<string, string> Table => table ??= Load(DefaultLanguage);

    public static string T(string key)
    {
        if (Table.TryGetValue(key, out var text)) return text;
        if (reported.Add(key)) Debug.LogWarning($"[Loc] missing text key '{key}'");
        return key;
    }

    public static string F(string key, params object[] args)
    {
        string format = T(key);
        try { return string.Format(format, args); }
        catch (System.FormatException) { Debug.LogWarning($"[Loc] bad format for '{key}': {format}"); return format; }
    }

    public static bool Has(string key) => Table.ContainsKey(key);

    // Every key and its raw text (tests check the table against the code).
    public static IReadOnlyDictionary<string, string> All => Table;

    public static Dictionary<string, string> Load(string language)
    {
        var result = new Dictionary<string, string>();
        var asset = Resources.Load<TextAsset>("Strings/" + language);
        if (asset == null) { Debug.LogError($"[Loc] Resources/Strings/{language}.txt not found"); return result; }
        Parse(asset.text, result);
        return result;
    }

    public static void Parse(string text, Dictionary<string, string> into)
    {
        foreach (var raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            int eq = line.IndexOf(" = ");
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string value = line.Substring(eq + 3);
            into[key] = UiTheme.ExpandColours(Unescape(value));
        }
    }

    static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // Tests and the editor reload after editing the file.
    public static void Reload() { table = null; reported.Clear(); }
}
