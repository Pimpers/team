using System.Collections.Generic;
using UnityEngine;

// Scene singleton holding the player's stats for this run.
// Meta stats (attack/speed/HP upgrade levels) are bought at the village blacksmith and
// read from PlayerPrefs here (see MetaProgress).
// Card bonuses and gold reset every time GameScene loads (a new run).
[DefaultExecutionOrder(-100)]
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    public float baseMoveSpeed = 3f;
    [Tooltip("Starting max HP in hearts (1 = one heart; damage comes in 0.5 heart steps).")]
    public float baseMaxHP = 5f;
    // Attack is counted in quarters of the old scale: base 4, each blacksmith level or card level adds 1
    // (= +25% of the base), and every enemy's HP is x4 of what it used to be. That lets the upgrades be
    // percentages - the old 1 -> 4 (x4 damage) from three levels made the boss a four-second fight.
    public const int BaseAttack = 4;

    public int MetaAttackLevel { get; private set; }
    public int MetaSpeedLevel { get; private set; }
    public int MetaHPLevel { get; private set; }

    // Skill-card levels for this run (see CardPool), by card id.
    readonly Dictionary<string, int> cardLevels = new Dictionary<string, int>();
    public int CardLevel(string id) => cardLevels.TryGetValue(id, out int lv) ? lv : 0;
    public IReadOnlyDictionary<string, int> CardLevels => cardLevels;

    // Skill cards in the order they were picked: the actives among them fill Q, E, R in this order.
    readonly List<string> skillOrder = new List<string>();
    public IReadOnlyList<string> SkillOrder => skillOrder;
    public List<string> ActiveSkills
    {
        get
        {
            var list = new List<string>();
            foreach (var id in skillOrder)
            {
                var c = CardPool.Get(id);
                if (c != null && c.kind == CardPool.Kind.Active) list.Add(id);
            }
            return list;
        }
    }

    // Which skill set the draft offers: the mercenary picked in the lobby (none in the legacy demo).
    public CardPool.Job Job => animator == null || animator.character == null ? CardPool.Job.Any
        : animator.character.ranged ? CardPool.Job.Archer : CardPool.Job.Knight;

    public int Gold { get; private set; }
    public float CurrentHP { get; private set; }
    public string StarterCardName { get; private set; } = "";

    // ---- Set bonuses (CardPool tags): how many different cards of each tag are owned, and which step that unlocks.
    readonly int[] setCounts = new int[System.Enum.GetValues(typeof(CardPool.Tag)).Length]; // not CardPool.TagNames: field initializers can't load its data
    public int SetCount(CardPool.Tag tag) => setCounts[(int)tag];
    public int SetLv(CardPool.Tag tag) => CardPool.SetLevel(setCounts[(int)tag]);

    void RecountSets()
    {
        System.Array.Clear(setCounts, 0, setCounts.Length);
        foreach (var kv in cardLevels)
            if (kv.Value > 0) foreach (var t in CardPool.TagsOf(kv.Key)) setCounts[(int)t]++;
    }

    float ClawDamage => SetLv(CardPool.Tag.Claw) >= 2 ? 1.25f : SetLv(CardPool.Tag.Claw) == 1 ? 1.12f : 1f;
    float ClawCrit => SetLv(CardPool.Tag.Claw) >= 2 ? 0.1f : 0f;
    public bool CritStuns => SetLv(CardPool.Tag.Claw) >= 2; // a critical hit dazes (Enemy.ReceiveDamage)
    // Finished sets: 발톱 dazes (above), 사냥 spreads poison, 간식 turns a wasted heal into gold (OnKill); 바람 and
    // 털가죽 stay plain numbers. (Four of the five dazed for a while: they read as one effect and stacked.)
    public bool HuntChain => SetLv(CardPool.Tag.Hunt) >= 2;  // a dazed / lured / poisoned monster's death poisons those near it (Enemy.ReceiveDamage)
    float WindSpeed => SetLv(CardPool.Tag.Wind) >= 1 ? 1.1f : 1f;
    public float SkillCooldownMultiplier => SetLv(CardPool.Tag.Wind) >= 2 ? 0.85f : 1f;
    public float StatusDurationMultiplier => SetLv(CardPool.Tag.Hunt) >= 1 ? 1.4f : 1f;
    public float StatusDamageMultiplier => SetLv(CardPool.Tag.Hunt) >= 2 ? 1.3f : 1f;
    public float ClearHealBonus => SetLv(CardPool.Tag.Fur) >= 2 ? 1f : 0f;

    public int AttackPower => BaseAttack + MetaAttackLevel + CardLevel("attack");
    public float MoveSpeed => baseMoveSpeed * (1f + 0.15f * MetaSpeedLevel) * (1f + 0.12f * CardLevel("speed")) * WindSpeed * (slowTimer > 0f ? slowFactor : 1f);

    // Slowed (stuck in the spider's web, EnemyWeb): moves at `factor` speed for `seconds`.
    float slowTimer, slowFactor = 1f;
    public bool Slowed => slowTimer > 0f;
    public void Slow(float seconds, float factor)
    {
        slowTimer = Mathf.Max(slowTimer, seconds);
        slowFactor = factor;
    }
    public float MaxHP => baseMaxHP + MetaHPLevel * 1f + CardLevel("heart") + (SetLv(CardPool.Tag.Fur) >= 1 ? 1f : 0f)
        + (animator != null && animator.character != null ? animator.character.bonusMaxHP : 0f);

    // Card effects read by the attack / movement code.
    // Melee stands inside every claw and web to land a hit while the archer shoots from safety for the same damage:
    // the knight swings farther and harder (and has a second bonus heart, CharacterDefinition.bonusMaxHP).
    const float KnightReach = 1.2f, KnightDamage = 1.25f;
    public float ReachMultiplier => (1f + 0.15f * CardLevel("reach")) * SkillSystem.RangeMultiplier(this) * (Job == CardPool.Job.Knight ? KnightReach : 1f);
    public float AttackSpeedMultiplier => (1f + 0.12f * CardLevel("haste")) * SkillSystem.AttackSpeedMultiplier(this) * WindSpeed;
    public float DashCooldownMultiplier => Mathf.Pow(0.75f, CardLevel("dash"));
    public float DashDistanceMultiplier => (1f + 0.2f * CardLevel("dash")) * SkillSystem.DashDistanceMultiplier(this); // same duration, faster = farther
    public int ExtraAirJumps => CardLevel("jump");
    public int ExtraDashes => CardLevel("dash_more"); // 연속 대시: 2 -> 3 -> 4 dashes in a row
    public float CritChance => CardPool.Crit(CardLevel("crit")) + SkillSystem.CritBonus(this) + ClawCrit;

    // What `read` gives with card `id` taken once more (the draft's preview line); the card levels and sets are put
    // back right after, and nothing else (HP, skills, banners) is touched.
    public T Preview<T>(string id, System.Func<T> read)
    {
        int had = CardLevel(id);
        cardLevels[id] = had + 1;
        RecountSets();
        try { return read(); }
        finally
        {
            if (had == 0) cardLevels.Remove(id); else cardLevels[id] = had;
            RecountSets();
        }
    }

    // Damage for one hit: attack x move multiplier (+50% per finisher level on the strong
    // moves), then a crit roll that doubles it.
    public int RollDamage(float moveMultiplier, out bool crit)
    {
        float mult = moveMultiplier * SkillSystem.ConsumeDodgeBonus(this); // 회피 반격 card: the first hit after a dash
        if (moveMultiplier > 1f) mult *= 1f + 0.5f * CardLevel("finisher");
        crit = RunRng.Value < CritChance;
        float dmg = AttackPower * mult * ClawDamage * (Job == CardPool.Job.Knight ? KnightDamage : 1f) * (crit ? 2f : 1f);
        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }

    // Set by the tutorial: hits still land but never take the last half heart.
    public static bool Immortal;

    [Tooltip("After taking a hit the player can't be hit again for this long (and blinks).")]
    public float hurtInvulnerableTime = 1f;
    float HurtInvulnerable => hurtInvulnerableTime + 0.3f * CardLevel("guard") + (SetLv(CardPool.Tag.Fur) >= 2 ? 0.4f : 0f);

    // Dodged: right after a hit, the start of a dash, and the jump-attack dive.
    public bool IsInvulnerable => hurtTimer > 0f || (animator != null && (animator.IsDashInvulnerable || animator.IsPlunging))
        || (SkillSystem.Instance != null && SkillSystem.Instance.Invulnerable);

    PlayerController2D animator;
    // The player's body (looked up once): for code that needs it every frame, instead of GetComponent each time.
    public PlayerController2D Body => animator != null ? animator : (animator = GetComponent<PlayerController2D>());
    float hurtTimer;
    bool dimmed;
    bool freshRun;
    Color blinkBase = Color.white;

    void Awake()
    {
        Instance = this;
        animator = GetComponent<PlayerController2D>();
        if (GetComponent<SkillSystem>() == null) gameObject.AddComponent<SkillSystem>();

        MetaAttackLevel = MetaProgress.Level(MetaProgress.Upgrade.Attack);
        MetaSpeedLevel = MetaProgress.Level(MetaProgress.Upgrade.Speed);
        MetaHPLevel = MetaProgress.Level(MetaProgress.Upgrade.Heart);

        var session = RunSession.Current;
        if (session.HasCarry)
        {
            foreach (var kv in session.CarryCards) cardLevels[kv.Key] = kv.Value;
            skillOrder.AddRange(session.CarrySkillOrder);
            Gold = session.CarryGold;
            RecountSets();
            CurrentHP = Mathf.Min(session.CarryHP, MaxHP);
            session.ClearCarry();
        }
        else
        {
            freshRun = true;
            session.ClearCarry();
            Gold = 0;
            CurrentHP = MaxHP;
            RunStats.Reset();
            RouteProgress.Reset();
        }
    }

    void Start()
    {
        if (animator != null && animator.Renderer != null) PlayerOutline.Attach(animator.Renderer);
        var run = RunManager.Instance;
        if (run != null && run.stageNumber == 1 && !run.tutorial)
        {
            hurtInvulnerableTime = Mathf.Max(hurtInvulnerableTime, 1.25f);
            if (freshRun)
            {
                var starter = CardPool.DrawStarter(this);
                if (starter != null)
                {
                    StarterCardName = starter.name;
                    ApplyCard(starter.id);
                    run.RefreshHudNow();
                    StartCoroutine(AnnounceStarter(starter));
                }
                if (MetaProgress.Level(MetaProgress.Upgrade.Starter) > 0) StartCoroutine(OpeningDraft());
            }
        }
    }

    System.Collections.IEnumerator AnnounceStarter(CardPool.Card card)
    {
        yield return null;
        while (StageTitle.Showing || StageWipe.Busy || ScreenFader.Switching) yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        var run = RunManager.Instance;
        if (run != null && run.hudText != null && run.hudText.canvas != null)
            StageBanner.Show(run.hudText.canvas.rootCanvas, run.hudText.font, Loc.T("card.starter"), card.name, 1.6f, compact: true);
    }

    // 든든한 출발 (blacksmith): one card draft before the first fight, once the stage's name and the starter banner are gone.
    System.Collections.IEnumerator OpeningDraft()
    {
        yield return null;
        while (StageTitle.Showing || StageWipe.Busy || ScreenFader.Switching || StageBanner.Up || GameClock.Paused) yield return null;
        var run = RunManager.Instance;
        if (run != null && run.cardDraftUI != null && !run.RunEnded) run.cardDraftUI.Show(() => { });
    }

    // Copies this scene's player state into the explicit cross-scene session.
    public void SaveForNextStage()
    {
        var session = RunSession.Current;
        session.HasCarry = true;
        session.CarryCards.Clear();
        foreach (var kv in cardLevels) session.CarryCards[kv.Key] = kv.Value;
        session.CarrySkillOrder.Clear();
        session.CarrySkillOrder.AddRange(skillOrder);
        session.CarryGold = Gold;
        session.CarryHP = CurrentHP;
    }

    // Spends this run's gold (the card draft's reroll). False if there isn't enough.
    public bool SpendGold(int amount)
    {
        if (amount < 0 || Gold < amount) return false;
        Gold -= amount;
        RunStats.GoldSpent += amount;
        return true;
    }

    public void AddGold(int amount)
    {
        Gold += amount;
    }

    // Takes a skill card (or levels it up). Healing cards act at once.
    public void ApplyCard(string id)
    {
        if (id == "heal") { Heal(2f); return; }
        var before = (int[])setCounts.Clone();
        cardLevels[id] = CardLevel(id) + 1;
        RecountSets();
        if (id == "heart") CurrentHP += 1f; // the new heart comes filled
        AnnounceSets(before);
        var card = CardPool.Get(id);
        if (card != null && card.IsSkill && !skillOrder.Contains(id))
        {
            skillOrder.Add(id);
            if (SkillSystem.Instance != null) SkillSystem.Instance.OnSkillLearned(id);
        }
    }

    // A set bonus switched on by the card just taken: say so, and fill the heart a 털가죽 set adds.
    void AnnounceSets(int[] before)
    {
        for (int i = 0; i < setCounts.Length; i++)
        {
            int was = CardPool.SetLevel(before[i]), now = CardPool.SetLevel(setCounts[i]);
            if (now <= was) continue;
            if (i == (int)CardPool.Tag.Fur && was == 0) CurrentHP += 1f;
            ColorUtility.TryParseHtmlString(CardPool.TagColors[i], out var col);
            string setName = CardPool.TagNames[i];
            string milestone = now == 2 ? Loc.T("set.complete") : Loc.T("set.unlocked");
            DamagePopup.SpawnLabel(transform.position + Vector3.up * (1.3f + 0.35f * i), $"{setName} {milestone}!", Color.Lerp(col, Color.white, 0.35f));
            // A full-screen ribbon, burst and shake make a set threshold feel like a real build moment.
            var run = RunManager.Instance;
            if (run != null && run.hudText != null && run.hudText.canvas != null)
                StageBanner.Show(run.hudText.canvas.rootCanvas, run.hudText.font, $"{setName} {milestone}!", CardPool.SetText[i][now - 1], 1.1f, compact: true); // short and up top: the big one held the middle of the fight for 2.35 s (1.1 s fully shown now the fades are quick)
            // At the feet (the heal glow stands on its bottom edge; transform.position is the body centre), cat-sized.
            Vfx.Play("heal", transform.position + Vector3.down * (animator != null ? animator.feetOffset : 0.67f), now == 2 ? 1f : 0.5f, tint: col, parent: transform);
            CameraFollow.Shake(now == 2 ? 0.36f : 0.24f, now == 2 ? 0.28f : 0.18f);
            Sfx.Play("stage_clear", now == 2 ? 0.95f : 0.7f, now == 2 ? 1.5f : 1.3f);
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || CurrentHP <= 0f) return;
        float before = CurrentHP;
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        if (CurrentHP > before)
        {
            // A warm glow around the cat, following it while it plays.
            float feet = animator != null ? animator.feetOffset : 0.67f;
            Vfx.Play("heal", transform.position + Vector3.down * feet, 0.5f, parent: transform); // 0.5: x1 (3.8 units) dwarfed the cat
        }
    }

    // A monster died to the player: card effects that trigger on kills. Returns bonus gold.
    public int OnKill()
    {
        float steal = CardPool.Lifesteal(CardLevel("lifesteal"));
        if (SetLv(CardPool.Tag.Treat) >= 2) steal = 1f - (1f - steal) * 0.8f; // 간식 세트: its own 20% roll on top
        bool full = CurrentHP >= MaxHP, treat2 = SetLv(CardPool.Tag.Treat) >= 2;
        int spare = 0;
        if (steal > 0f && (!full || treat2) && RunRng.Value < steal)
        {
            if (full) // 간식 세트 완성: a heal with nowhere to go is 2 gold instead
            {
                spare = 2;
                DamagePopup.SpawnLabel(transform.position + Vector3.up * 1.1f, Loc.T("set.treat_gold"), UiTheme.Gold);
            }
            else
            {
                Heal(0.5f);
                DamagePopup.SpawnLabel(transform.position + Vector3.up * 1.1f, Loc.T("player.healed"), new Color(1f, 0.45f, 0.55f));
            }
        }
        return CardLevel("greed") + (SetLv(CardPool.Tag.Treat) >= 1 ? 1 : 0) + spare;
    }

    const float ParryCooldown = 4f;
    float parryReadyAt;

    // Returns false if the hit was dodged (so the attacker skips its knockback too).
    public bool TakeDamage(float amount, string source = "")
    {
        if (IsInvulnerable || (animator != null && animator.IsDead)) return false;
        // 식빵 자세 (shield) / 수염 센서 (counter) can eat the hit.
        if (SkillSystem.Instance != null && SkillSystem.Instance.TryBlockHit()) return false;
        // 쳐내기 (the knight only): a hit that lands while the sword is swinging is knocked aside, once every
        // ParryCooldown. The knight fights inside every claw's reach; attacking is its guard.
        if (Job == CardPool.Job.Knight && animator != null && animator.IsAttacking && Time.time >= parryReadyAt)
        {
            parryReadyAt = Time.time + ParryCooldown;
            hurtTimer = Mathf.Max(hurtTimer, 0.4f); // a second claw in the same swing doesn't land either
            Vfx.Play("hit_mid", transform.position + Vector3.right * animator.FacingSign * 0.5f, 1f, tint: new Color(1f, 0.9f, 0.6f));
            Sfx.Play("hit_strong", 0.8f, 1.5f);
            DamagePopup.SpawnLabel(transform.position + Vector3.up * 1.1f, Loc.T("skill.parry"), new Color(1f, 0.9f, 0.6f));
            HitStop.Do(0.06f);
            OneTimeTip.ShowInRun("parry"); // the first time: what just happened
            return false;
        }

        CurrentHP -= amount;
        RunStats.RecordDamage(source, amount);
        if (SkillSystem.Instance != null) SkillSystem.Instance.OnHurt(); // breaks a skill in progress, resets 그루밍
        if (Immortal) CurrentHP = Mathf.Max(0.5f, CurrentHP); // tutorial: hits hurt but never kill
        Sfx.Play("player_hurt");
        Vfx.Play("hurt", transform.position); // red hit flash
        HitStop.Do(0.08f);
        CameraFollow.Shake(0.2f, 0.15f);

        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            if (animator != null) animator.TriggerDeath();
            if (RunManager.Instance != null) RunManager.Instance.OnPlayerDied();
        }
        else
        {
            hurtTimer = HurtInvulnerable;
            if (animator != null) animator.TriggerHit();
        }
        return true;
    }

    void Update()
    {
        if (slowTimer > 0f) slowTimer -= Time.deltaTime;
        if (hurtTimer <= 0f) return;
        hurtTimer -= Time.deltaTime;
        // Blink while invulnerable after a hit.
        // Blink by darkening and reddening, never by fading: the white cat turned transparent against the
        // bright sky and clouds and vanished mid-fight (at alpha 0.35 and still at 0.6).
        bool dim = hurtTimer > 0f && Mathf.FloorToInt(hurtTimer * 8f) % 2 == 0;
        if (dim != dimmed && animator != null && animator.Renderer != null)
        {
            dimmed = dim;
            var r = animator.Renderer;
            if (dim)
            {
                blinkBase = r.color;
                r.color = new Color(blinkBase.r * 0.95f, blinkBase.g * 0.5f, blinkBase.b * 0.5f, 1f);
            }
            else r.color = new Color(blinkBase.r, blinkBase.g, blinkBase.b, 1f);
        }
    }
}
