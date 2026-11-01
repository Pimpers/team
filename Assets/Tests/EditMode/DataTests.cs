using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

// The data moved out of the scripts: the text table (Resources/Strings/ko.txt) and the cards
// (Resources/Data/Cards.json). These catch a key typo, a broken {0}, or text slipping back into the code.
public class DataTests
{
    static string[] RuntimeScripts() => Directory.GetFiles(Path.Combine(Application.dataPath, "CS"), "*.cs", SearchOption.AllDirectories);

    // Keys written out whole (not built like "card." + id - those are checked through CardPool below).
    static readonly Regex KeyCall = new Regex("Loc\\.(T|F)\\(\\s*\"([^\"]+)\"\\s*[,)]");

    [Test]
    public void EveryTextKeyTheCodeUses_IsInTheTable()
    {
        Loc.Reload();
        var missing = new List<string>();
        foreach (var file in RuntimeScripts())
            foreach (Match m in KeyCall.Matches(File.ReadAllText(file)))
                if (!Loc.Has(m.Groups[2].Value)) missing.Add($"{Path.GetFileName(file)}: {m.Groups[2].Value}");
        Assert.That(missing, Is.Empty, string.Join("\n", missing));
    }

    [Test]
    public void PlainTextKeys_HaveNoPlaceholders()
    {
        var wrong = new List<string>();
        foreach (var file in RuntimeScripts())
            foreach (Match m in KeyCall.Matches(File.ReadAllText(file)))
                if (m.Groups[1].Value == "T" && Loc.Has(m.Groups[2].Value) && Regex.IsMatch(Loc.T(m.Groups[2].Value), "\\{\\d"))
                    wrong.Add(m.Groups[2].Value);
        Assert.That(wrong, Is.Empty, "Loc.T would show these {0} literally - use Loc.F:\n" + string.Join("\n", wrong));
    }

    [Test]
    public void EveryTableLine_FormatsAndHasItsColoursResolved()
    {
        var dummy = new object[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        foreach (var pair in Loc.All)
        {
            string format = Regex.Replace(pair.Value, "\\{[a-z]+\\}", ""); // card texts' named numbers (CardPool.Fill)
            Assert.DoesNotThrow(() => string.Format(format, dummy), $"{pair.Key}: {pair.Value}");
            Assert.That(pair.Value, Does.Not.Contain("<color=$"), $"{pair.Key} names a colour UiTheme doesn't have");
        }
    }

    [Test]
    public void NoHangulStringLiterals_LeftInTheRuntimeScripts()
    {
        var literal = new Regex("\"(?:[^\"\\\\\\n]|\\\\.)*[\\uAC00-\\uD7A3](?:[^\"\\\\\\n]|\\\\.)*\"");
        var found = new List<string>();
        foreach (var file in RuntimeScripts())
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart();
                if (line.StartsWith("//") || line.StartsWith("[Tooltip(") || line.StartsWith("[Header(")) continue;
                int comment = CommentStart(line);
                if (comment >= 0) line = line.Substring(0, comment);
                if (literal.IsMatch(line)) found.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
            }
        }
        Assert.That(found, Is.Empty, "player-facing text belongs in Resources/Strings/ko.txt:\n" + string.Join("\n", found));
    }

    [Test]
    public void NoFieldInitializer_LoadsTheTextOrCardData()
    {
        // A field initializer (static or not) can run inside Unity's object construction, where Resources.Load
        // throws - which took the whole CardPool down once. Build such values on first use instead.
        var init = new Regex("\\breadonly\\s[^;=(){}]*=[^;]*?(Loc\\.|CardPool\\.(All|Stats|Skills|TagNames|TagColors|SetText|TierNames|SetStep))");
        var found = new List<string>();
        foreach (var file in RuntimeScripts())
        {
            if (Path.GetFileName(file) == "CardPool.cs") continue; // assigns them in its own static constructor
            foreach (Match m in init.Matches(File.ReadAllText(file))) found.Add($"{Path.GetFileName(file)}: {m.Value.Split('\n')[0].Trim()}");
        }
        Assert.That(found, Is.Empty, string.Join("\n", found));
    }

    [Test]
    public void EveryKoreanSceneText_IsLinkedToATableKey()
    {
        // Reads the scene files as text (no scene is opened): per build scene, each TMP text holding Hangul
        // (m_text, 가-힣 escapes) needs a LocText, and every LocText key must be in the table.
        string guid = Regex.Match(File.ReadAllText(Path.Combine(Application.dataPath, "CS/LocText.cs.meta")), "guid: (\\w+)").Groups[1].Value;
        var hangulText = new Regex("^  m_text: \".*\\\\u(A[C-F]|[BC][0-9A-F]|D[0-7])[0-9A-F]{2}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var problems = new List<string>();
        foreach (var s in UnityEditor.EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            string yaml = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), s.path));
            int texts = hangulText.Matches(yaml).Count, linked = 0;
            foreach (var block in yaml.Split(new[] { "\n--- !u!" }, System.StringSplitOptions.None))
            {
                if (!block.Contains("guid: " + guid)) continue;
                linked++;
                string key = Regex.Match(block, "\\n  key: (.*)").Groups[1].Value.Trim();
                if (!Loc.Has(key)) problems.Add($"{s.path}: LocText key '{key}' is not in the table");
            }
            if (linked != texts) problems.Add($"{s.path}: {texts} Korean texts but {linked} LocText (run Cats Smash > Link Scene Texts To Table)");
        }
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    [Test]
    public void LocText_SwapsTheBuiltInLine_ButKeepsTextAScriptWrote()
    {
        var awake = typeof(LocText).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var go = new GameObject("LocTextProbe");
        try
        {
            var t = go.AddComponent<TMPro.TextMeshProUGUI>();
            var loc = go.AddComponent<LocText>();
            loc.key = "ui.close"; loc.source = "built-in";
            t.text = "built-in";
            awake.Invoke(loc, null);
            Assert.That(t.text, Is.EqualTo(Loc.T("ui.close")));
            t.text = "written by a script";
            awake.Invoke(loc, null);
            Assert.That(t.text, Is.EqualTo("written by a script"));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void BuildScenes_HaveUniqueNames()
    {
        // The game loads scenes by name, and a name listed twice always opens the first one: a second copy
        // (an old Assets/MainScene.unity once sat there) is dead weight that looks like the real scene.
        var seen = new Dictionary<string, string>();
        var clashes = new List<string>();
        foreach (var s in UnityEditor.EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            string name = Path.GetFileNameWithoutExtension(s.path);
            if (seen.TryGetValue(name, out var first)) clashes.Add($"{name}: {first} and {s.path}");
            else seen[name] = s.path;
        }
        Assert.That(clashes, Is.Empty, string.Join("\n", clashes));
    }

    // Index of a // comment outside string literals, or -1.
    static int CommentStart(string line)
    {
        bool inString = false;
        for (int i = 0; i < line.Length - 1; i++)
        {
            char c = line[i];
            if (inString) { if (c == '\\') i++; else if (c == '"') inString = false; continue; }
            if (c == '"') inString = true;
            else if (c == '/' && line[i + 1] == '/') return i;
        }
        return -1;
    }

    [Test]
    public void Cards_LoadFromTheirDataFile_WithTextForEveryLevel()
    {
        Assert.That(CardPool.All.Length, Is.GreaterThan(0), "Resources/Data/Cards.json gave no cards");
        Assert.That(CardPool.Stats.Length + CardPool.Skills.Length, Is.EqualTo(CardPool.All.Length));
        foreach (var c in CardPool.All)
        {
            Assert.That(c.name, Is.Not.Empty.And.Not.StartWith("card."), $"{c.id} has no name in the text table");
            for (int lv = 1; lv <= Mathf.Max(1, c.maxLevel); lv++)
            {
                string text = c.effect(lv);
                Assert.That(text, Is.Not.Empty.And.Not.Contain("card.").And.Not.Contain("{"), $"{c.id} level {lv}: {text}");
            }
            if (c.kind == CardPool.Kind.Enhance) Assert.That(c.tag, Is.Not.Empty, $"{c.id} doesn't say what it enhances");
            if (c.kind == CardPool.Kind.Active) Assert.That(c.cooldown, Is.GreaterThan(0f), $"{c.id} has no cooldown");
        }
        var go = new GameObject("StarterProbe");
        try { Assert.That(CardPool.DrawStarter(go.AddComponent<PlayerStats>()), Is.Not.Null, "starterCard isn't a card"); }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void EveryTag_HasNameColourAndBothSetTexts()
    {
        for (int t = 0; t < CardPool.TagNames.Length; t++)
        {
            Assert.That(CardPool.TagNames[t], Is.Not.Null.And.Not.StartWith("tag."), $"tag {(CardPool.Tag)t}");
            Assert.That(ColorUtility.TryParseHtmlString(CardPool.TagColors[t], out _), Is.True, $"colour of {(CardPool.Tag)t}");
            Assert.That(CardPool.SetText[t], Has.Length.EqualTo(2));
        }
        foreach (var n in CardPool.TierNames) Assert.That(n, Is.Not.Null.And.Not.StartWith("tier."));
    }

    [Test]
    public void ActiveCooldownLine_StartsWithTheWordTheDraftCompacts()
    {
        // CardDraftUI.CardText finds "\n" + card.cooldown_word to move the cooldown up into the kind line.
        Assert.That(Loc.T("card.cooldown"), Does.StartWith("\n" + Loc.T("card.cooldown_word")));
    }

    [Test]
    public void BuiltTexts_ResolveEveryKey()
    {
        for (int lv = 0; lv <= Corruption.MaxLevel; lv++) Assert.That(Corruption.Describe(lv), Does.Not.Contain("corruption."));
        Assert.That(RunStats.DefeatTip, Does.Not.StartWith("tip."));
    }
}
