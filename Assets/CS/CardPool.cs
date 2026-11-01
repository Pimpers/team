using System.Collections.Generic;
using UnityEngine;

// The run's cards. After each normal stage the draft offers 3 random cards from this pool;
// picking one you already own levels it up, maxed cards stop appearing. Levels live in
// PlayerStats (carried between stage scenes, reset every new run); the effects are read
// where they apply (PlayerStats / PlayerAttack / PlayerController2D / RunManager / SkillSystem).
//
// Two families share the draft:
// - Stat cards (any mercenary): attack +1, speed, hearts...
// - Skill cards, one set per mercenary (검사 / 궁수). The cats start a run with no skills at all;
//   these are found on the way to the boss and are gone when the run ends.
//     Active  - a move on Q / E / R (slots fill in the order picked, at most 3), with a cooldown.
//     Passive - always on.
//     Enhance - changes a basic move (the swing / arrow, the dash, the jump attack).
//
// Data: the card list (grade, level cap, icon, cooldown, tags), the grade weights and the set steps are in
// Resources/Data/Cards.json; every name and description is in the text table (Loc, Resources/Strings/ko.txt)
// under card.<id>.name / .text / .text2. What a card actually does is code, found by its id.
public static class CardPool
{
    public enum Kind { Stat, Active, Passive, Enhance }
    public enum Job { Any, Knight, Archer }
    // Card grade: its background in the draft (green / blue / purple) and how often it shows up.
    public enum Tier { Common, Rare, Epic }
    // Tags and set bonuses. Every card (but the one-shot 낮잠) carries one or two tags; owning 2 different
    // cards of a tag turns on that tag's first bonus, 3 its second. A run offers only ~4 drafts, so 2 / 3 are
    // the realistic steps (a card's levels don't count again: it is about collecting different cards).
    public enum Tag { Claw, Wind, Fur, Hunt, Treat }

    public class Card
    {
        public string id, name;
        public int maxLevel;           // 0 = one-shot (no level, can be picked any number of times)
        public System.Func<int, string> effect; // text for the level being bought
        public Kind kind = Kind.Stat;
        public Job job = Job.Any;
        public string icon;            // Vfx id whose frame is the HUD icon
        public float cooldown;         // actives: seconds at level 1 (see SkillSystem.Cooldown)
        public string tag;             // enhance cards: what they change (평타 / 대시 / 점프 공격)
        public Tier tier = Tier.Common;
        public Tag[] tags = NoTags;
        public bool offensive;         // raises damage output (see Draw)
        public bool IsSkill => kind != Kind.Stat;

        // Hand-drawn 64 px icon (Resources/SkillIcons/<id>.png); null falls back to the Vfx frame in `icon`.
        Sprite art; bool artLoaded;
        public Sprite Art
        {
            get
            {
                if (!artLoaded) { artLoaded = true; art = Resources.Load<Sprite>("SkillIcons/" + id); }
                return art;
            }
        }
    }

    // ---- The data, read once from Resources/Data/Cards.json.
    [System.Serializable] class TierRow { public string id; public float weight; }
    [System.Serializable] class TagRow { public string id; public string color; }
    [System.Serializable]
    class CardRow
    {
        public string id, kind, job, tier, icon, enhance;
        public int maxLevel;
        public float cooldown;
        public string[] tags;
        public bool offensive;
    }
    [System.Serializable]
    class CardFile
    {
        public string starterCard;
        public int setStep1, setStep2;
        public TierRow[] tiers;
        public TagRow[] tags;
        public CardRow[] cards;
    }

    public const string DataPath = "Data/Cards";
    static readonly Tag[] NoTags = new Tag[0];

    public static readonly Card[] Stats, Skills, All;
    public static readonly string[] TierNames;
    static readonly float[] TierWeight;   // relative draw weights (a draft still forces one skill and one attack card)
    public static readonly string[] TagNames;
    // Readable on the cream / blue / lavender card boxes and on the parchment pause menu.
    public static readonly string[] TagColors;
    public static readonly int SetStep1, SetStep2;
    // [tag] = { text at SetStep1, text at SetStep2 (what it adds on top) }
    public static readonly string[][] SetText;
    static readonly string StarterId;
    static readonly Dictionary<string, Card> byId = new Dictionary<string, Card>();

    static CardPool()
    {
        var asset = Resources.Load<TextAsset>(DataPath);
        var file = asset != null ? JsonUtility.FromJson<CardFile>(asset.text) : null;
        if (file == null || file.cards == null) { Debug.LogError($"[CardPool] Resources/{DataPath}.json missing or unreadable"); file = new CardFile { cards = new CardRow[0], tiers = new TierRow[0], tags = new TagRow[0] }; }

        int tierCount = System.Enum.GetValues(typeof(Tier)).Length, tagCount = System.Enum.GetValues(typeof(Tag)).Length;
        TierNames = new string[tierCount];
        TierWeight = new float[tierCount];
        foreach (var t in file.tiers)
        {
            int i = (int)Parse<Tier>(t.id);
            TierNames[i] = Loc.T("tier." + t.id);
            TierWeight[i] = t.weight;
        }
        TagNames = new string[tagCount];
        TagColors = new string[tagCount];
        SetText = new string[tagCount][];
        foreach (var t in file.tags)
        {
            int i = (int)Parse<Tag>(t.id);
            TagNames[i] = Loc.T("tag." + t.id);
            TagColors[i] = t.color;
            SetText[i] = new[] { Loc.T($"set.{t.id}.1"), Loc.T($"set.{t.id}.2") };
        }
        SetStep1 = file.setStep1;
        SetStep2 = file.setStep2;
        StarterId = file.starterCard;

        var stats = new List<Card>();
        var skills = new List<Card>();
        foreach (var row in file.cards)
        {
            var c = Build(row);
            byId[c.id] = c;
            (c.IsSkill ? skills : stats).Add(c);
        }
        Stats = stats.ToArray();
        Skills = skills.ToArray();
        All = new Card[Stats.Length + Skills.Length];
        Stats.CopyTo(All, 0);
        Skills.CopyTo(All, Stats.Length);
    }

    static T Parse<T>(string s) where T : struct => (T)System.Enum.Parse(typeof(T), s);

    static Card Build(CardRow r)
    {
        var c = new Card
        {
            id = r.id, name = Loc.T($"card.{r.id}.name"), maxLevel = r.maxLevel, icon = r.icon, cooldown = r.cooldown,
            kind = Parse<Kind>(r.kind), job = Parse<Job>(r.job), tier = Parse<Tier>(r.tier), offensive = r.offensive,
            tag = string.IsNullOrEmpty(r.enhance) ? null : Loc.T("enhance." + r.enhance),
        };
        if (r.tags != null && r.tags.Length > 0)
        {
            c.tags = new Tag[r.tags.Length];
            for (int i = 0; i < r.tags.Length; i++) c.tags[i] = Parse<Tag>(r.tags[i]);
        }
        string text = Loc.T($"card.{r.id}.text");
        string text2 = Loc.Has($"card.{r.id}.text2") ? Loc.T($"card.{r.id}.text2") : text;
        if (c.kind == Kind.Active)
        {
            float cd = c.cooldown;
            c.effect = lv => lv <= 1 ? text + Loc.F("card.cooldown", cd) : Loc.T("card.active_up");
        }
        else c.effect = lv => Fill(lv <= 1 ? text : text2, lv);
        return c;
    }

    // The numbers some card texts show for the level being bought.
    static string Fill(string text, int lv)
    {
        if (text.IndexOf('{') < 0) return text;
        return text.Replace("{lifesteal}", (Lifesteal(lv) * 100f).ToString("0"))
                   .Replace("{crit}", (Crit(lv) * 100f).ToString("0"))
                   .Replace("{dashes}", (2 + lv).ToString());
    }

    public static Tag[] TagsOf(string id) => byId.TryGetValue(id, out var c) ? c.tags : NoTags;
    public static bool Has(string id, Tag tag) { foreach (var t in TagsOf(id)) if (t == tag) return true; return false; }

    // 0 = no bonus yet, 1 = first bonus (2 cards), 2 = both (3 cards).
    public static int SetLevel(int count) => count >= SetStep2 ? 2 : count >= SetStep1 ? 1 : 0;

    // Per-level values (used by the effect code and the card text).
    public static float Lifesteal(int lv) => lv <= 0 ? 0f : 0.05f + 0.1f * lv;   // 15 / 25 / 35%
    public static float Crit(int lv) => 0.1f * lv;                               // 10 / 20 / 30%

    public static Card Get(string id) => id != null && byId.TryGetValue(id, out var c) ? c : null;

    // Every fresh run starts with the same small attack boost. A fixed card keeps
    // the first fight fair while still starting the build immediately.
    public static Card DrawStarter(PlayerStats stats)
        => stats == null ? null : Get(StarterId);

    // Three offers per draft: one new active skill while a slot is empty, one damage card,
    // then a weighted choice. Epic skills begin at stage 3; a hunt reward can still force
    // a rare-or-better card once one is eligible.
    public static List<Card> Draw(PlayerStats stats, int count)
    {
        Job job = stats.Job;
        int stage = RunManager.Instance != null ? RunManager.Instance.stageNumber : 3;
        bool slotsFull = stats.ActiveSkills.Count >= SkillSystem.MaxActiveSlots;
        var candidates = new List<Card>();
        foreach (var c in All)
        {
            int lv = stats.CardLevel(c.id);
            if (c.maxLevel > 0 && lv >= c.maxLevel) continue;
            if (c.id == "heal" && stats.CurrentHP >= stats.MaxHP) continue;
            if (c.IsSkill && (job == Job.Any || c.job != job)) continue;
            // Big build-defining moves belong to the deep forest, not the first two stages.
            if (stage > 0 && stage < 3 && c.tier == Tier.Epic) continue;
            if (c.kind == Kind.Active && lv == 0 && slotsFull) continue;
            candidates.Add(c);
        }
        WeightedShuffle(candidates, stats); // 희귀 / 전설 cards come up less often; a tag you already collect shows up a bit more

        // Pull one skill to the front, then one offensive card next to it (every draft offers one while any
        // is left, so a run can't reach the boss (270 HP, Enemies/Bear.asset) with no way to hit harder); the rest are random.
        // While a Q / E / R slot is empty that skill is a new active when one is left (a run has only four
        // drafts - a play test reached Stage 3 with a single skill key because none was offered).
        int skill = slotsFull ? -1 : candidates.FindIndex(c => c.kind == Kind.Active && stats.CardLevel(c.id) == 0);
        if (skill < 0) skill = candidates.FindIndex(c => c.IsSkill);
        if (skill > 0) (candidates[0], candidates[skill]) = (candidates[skill], candidates[0]);
        int off = candidates.Count > 1 ? candidates.FindIndex(1, c => c.offensive) : -1;
        if (off > 1) (candidates[1], candidates[off]) = (candidates[off], candidates[1]);
        // A hunt-route reward can reserve a rare card. Keep it inside the three offered cards,
        // then shuffle as usual so its slot stays a surprise.
        int rare = candidates.FindIndex(c => c.tier != Tier.Common);
        if (RouteProgress.TryConsumeRareDraft(rare >= 0) && rare > 0)
            (candidates[0], candidates[rare]) = (candidates[rare], candidates[0]);
        if (candidates.Count > count) candidates.RemoveRange(count, candidates.Count - count);
        Shuffle(candidates); // so the guaranteed cards aren't always in the first slots
        return candidates;
    }

    // Random order where each card is ahead of the others in proportion to its grade weight
    // (key = u^(1/w), largest first).
    static void WeightedShuffle(List<Card> list, PlayerStats stats)
    {
        var keys = new Dictionary<Card, float>();
        foreach (var c in list)
        {
            float w = TierWeight[(int)c.tier];
            // Only a nudge toward finishing a set (not once its second bonus is on): the draft stays varied.
            // x2: with only ~4 drafts a run, at 1.5 a run could easily end with no set opened at all.
            foreach (var t in c.tags) if (stats.SetCount(t) >= 1 && stats.SetCount(t) < SetStep2 && stats.CardLevel(c.id) == 0) { w *= 2f; break; }
            keys[c] = Mathf.Pow(Mathf.Max(0.000001f, RunRng.Value), 1f / w);
        }
        list.Sort((a, b) => keys[b].CompareTo(keys[a]));
    }

    static void Shuffle(List<Card> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RunRng.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
