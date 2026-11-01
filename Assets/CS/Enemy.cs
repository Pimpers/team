using System.Collections.Generic;
using UnityEngine;

// An enemy. Two modes:
// - With a `definition` (Stage scenes): animated (idle/move/attack/hurt/death), patrols
//   around its spawn point, chases the player when it sees them on roughly the same level,
//   and attacks (bite / stab / claw) when close - damage lands on the attack's hit frame.
//   Walks along the StageMap tiles and won't walk off ledges or into walls.
// - Without one (legacy GameScene): stands still and hurts the player on contact.
// The only way to damage an enemy is the player's attack (PlayerAttack / ArrowProjectile),
// which calls ReceiveDamage().
public partial class Enemy : MonoBehaviour
{
    public EnemyDefinition definition;

    public bool isBoss = false;
    public int hp = 1;
    public int goldReward = 5;
    [Tooltip("Hearts lost per hit: 0.5 for normal monsters, 1 for the boss.")]
    public float contactDamage = 0.5f;
    public float hitRadius = 0.6f;
    public float hitCooldown = 1f;
    public float knockbackDistance = 0.8f;

    enum State { Idle, Move, Attack, Hurt, Dead }

    Transform player;
    SpriteRenderer sr;
    PlayerController2D playerController;
    float cooldownTimer;

    State state = State.Idle;
    Sprite[] frames;
    float fps;
    int frame;
    float frameTimer;
    bool attackLanded;
    float hurtTimer;
    float deadTimer;
    float flashTimer;
    float spawnX;
    float patrolTargetX;
    float patrolWait;
    int facing = -1; // -1 = left, 1 = right (sprites are drawn facing right)
    float knockVelocity;
    float ambushTimer;
    bool verticalHop;
    float verticalHopElapsed, verticalHopCooldown, verticalHopWindup;
    Vector3 verticalHopFrom, verticalHopTo;
    float alertTimer;      // > 0: has seen the player recently and keeps chasing
    bool wasAlerted;
    const float KnockSpeed = 7f;   // initial slide speed of a normal hit (~0.6 units)
    const float KnockDecel = 40f;

    // Hit feel on the body itself: squashed flat by a hit (springs back once the hit stop lets go), shaking a
    // pixel either way while the game is frozen, and a little hop as it is knocked away by the killing blow.
    // Drawn as an offset added in LateUpdate and taken back off at the start of Update, so nothing that moves
    // or measures the enemy ever sees it.
    Vector3 baseScale = Vector3.one;
    float squashTimer, squash;
    float jitterUntil;
    float hopTimer, hopHeight;
    Vector3 drawnOffset;
    const float SquashTime = 0.16f, HopTime = 0.32f;

    public bool IsDead => state == State.Dead;
    public SpriteRenderer Renderer => sr;
    public int HP => hp;
    public int MaxHP { get; private set; }

    // ---- Boss hooks (used by BossBrain; ordinary monsters leave them at their defaults)
    [HideInInspector] public float speedMultiplier = 1f;
    [HideInInspector] public float cooldownMultiplier = 1f;
    [Tooltip("Extra claws chained after each attack (boss enrage).")]
    [HideInInspector] public int extraStrikes;
    [HideInInspector] public Color baseTint = Color.white;  // colour when not flashing
    [HideInInspector] public float damageTakenMultiplier = 1f; // > 1 while a boss is staggered (BossBrain's opening)
    BossBrain brain;
    int strikesLeft;

    // Every active enemy in the scene, so attacks/arrows/the minimap don't have to search
    // the whole scene (FindObjectsByType) every frame.
    public static readonly List<Enemy> All = new List<Enemy>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (definition == null) return;
        isBoss = definition.isBoss;
        hp = definition.hp;
        goldReward = definition.goldReward;
        contactDamage = definition.damage;
        hitRadius = definition.hitRadius;
        knockbackDistance = definition.knockback;
        transform.localScale = baseScale = new Vector3(definition.scale, definition.scale, 1f);
        Corruption.ApplyTo(this); // 타락도: tougher, faster, harder-hitting, richer
        hp = Mathf.CeilToInt(hp * StageScaling.HpMultiplier(isBoss)); // later stages are tougher
        // Slimes are the line holders: their larger health pool makes them the first target in a mixed pack.
        if (!isBoss && definition.combatRole == EnemyDefinition.CombatRole.Tank) hp = Mathf.CeilToInt(hp * 1.5f);
        MaxHP = hp;
        // Bosses get their phase/pattern logic automatically.
        if (definition.isBoss) brain = GetComponent<BossBrain>() ?? gameObject.AddComponent<BossBrain>();
    }

    public bool IsElite { get; private set; }

    // Set by BossBrain after its entrance roar: from then on the boss goes after the player from
    // anywhere in the arena (its normal sight range is only 20 tiles).
    [HideInInspector] public bool engaged;

    // Elite variant (picked at random per run by StageRandomizer): bigger, golden, tougher,
    // hits harder, pays more. Call after Awake (it scales the definition's stats).
    public void MakeElite()
    {
        if (IsElite || isBoss || definition == null) return;
        IsElite = true;
        hp *= 3;
        MaxHP = hp;
        goldReward *= 4;
        contactDamage += 0.5f;
        transform.localScale = baseScale = new Vector3(definition.scale * 1.3f, definition.scale * 1.3f, 1f);
        baseTint = new Color(1f, 0.82f, 0.45f);
        if (sr != null) sr.color = baseTint;
        AddEliteTag();
    }

    // A gold tint barely shows on dark creatures (the spider), so elites also get a pixel outline that swaps
    // between gold and ember red (EliteOutline). Hidden when the enemy dies (see ReceiveDamage).
    GameObject eliteTag;
    EnemyHPBar hpBar;

    void AddEliteTag()
    {
        if (sr != null) eliteTag = EliteOutline.Attach(sr);
    }

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController2D>();
        }
        spawnX = transform.position.x;
        patrolTargetX = spawnX;
        patrolWait = RunRng.Range(0.2f, 1f);
        verticalHopCooldown = RunRng.Range(0.6f, 1.4f);
        if (definition != null) Play(State.Idle);
        // Health bar visible from the start (not just after the first hit), so the player can read a
        // monster's remaining HP at a glance mid-fight. The boss keeps its own bar at the top of the screen.
        if (hpBar == null && !isBoss && definition != null && hp > 0) hpBar = EnemyHPBar.Attach(this);
    }

    public void RelocateTo(Vector2 feet)
    {
        transform.position -= drawnOffset;
        drawnOffset = Vector3.zero;
        transform.position = feet;
        spawnX = feet.x;
        patrolTargetX = spawnX;
        patrolWait = 0f;
        knockVelocity = 0f;
        engaged = true;
    }

    void Update()
    {
        if (definition == null) { LegacyContactUpdate(); return; }
        transform.position -= drawnOffset; // the hit shake / hop is only drawn
        drawnOffset = Vector3.zero;
        if (squashTimer > 0f) squashTimer -= Time.deltaTime;
        if (hopTimer > 0f) hopTimer -= Time.deltaTime;
        if (ambushTimer > 0f) ambushTimer -= Time.deltaTime;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            sr.color = flashTimer > 0f ? new Color(1f, 0.45f, 0.45f) : baseTint;
        }

        // Knockback slide from the last hit (stops at ledges and walls).
        if (knockVelocity != 0f)
        {
            if (!TryWalk(knockVelocity * Time.deltaTime)) knockVelocity = 0f;
            knockVelocity = Mathf.MoveTowards(knockVelocity, 0f, KnockDecel * Time.deltaTime);
        }

        if (state != State.Dead) UpdatePoison();
        if (state != State.Dead && stunTimer <= 0f) UpdateContactDamage();

        // A boss special move (charge, leap, roar) drives the body itself while it runs.
        if (brain != null && state != State.Dead && brain.Tick())
        {
            Animate();
            return;
        }

        // Skill status: stunned / charmed / trapped monsters stand still, lured ones walk to the bait.
        if (state != State.Dead && UpdateControlStatus())
        {
            Animate();
            return;
        }

        switch (state)
        {
            case State.Dead: UpdateDead(); break;
            case State.Hurt:
                hurtTimer -= Time.deltaTime;
                if (hurtTimer <= 0f) Play(State.Idle);
                break;
            case State.Attack: UpdateAttack(); break;
            default: UpdateMovement(); break;
        }
        Animate();
    }

    // ---------------------------------------------------------------- animation
    void Play(State s)
    {
        state = s;
        frame = 0;
        frameTimer = 0f;
        switch (s)
        {
            case State.Move: frames = definition.moveFrames; fps = definition.moveFps; break;
            case State.Attack: frames = definition.attackFrames; fps = definition.attackFps; break;
            case State.Hurt: frames = definition.hurtFrames; fps = 8f; break;
            case State.Dead: frames = definition.deathFrames; fps = definition.deathFps; break;
            default: frames = definition.idleFrames; fps = definition.idleFps; break;
        }
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    float lastWalkX;

    void Animate()
    {
        if (frames == null || frames.Length == 0) return;
        float walked = Mathf.Abs(transform.position.x - lastWalkX);
        lastWalkX = transform.position.x;
        if (state == State.Move && definition != null && definition.walkStridePixels > 0f)
        {
            // Walk by distance: one frame per stride, so the feet never slide (standing still holds the pose).
            float stride = definition.walkStridePixels / 16f * Mathf.Abs(baseScale.x); // (not the hit squash)
            frameTimer += walked / stride;
            if (frameTimer < 1f) return;
            frame = (frame + (int)frameTimer) % frames.Length;
            frameTimer -= (int)frameTimer;
            sr.sprite = frames[frame];
            return;
        }
        frameTimer += Time.deltaTime;
        if (frameTimer < 1f / fps) return;
        bool loops = state == State.Idle || state == State.Move;
        if (frame < frames.Length - 1) { frame++; frameTimer = 0f; }
        else if (loops) { frame = 0; frameTimer = 0f; }
        sr.sprite = frames[frame];
    }

    // ---------------------------------------------------------------- legacy (GameScene)
    void LegacyContactUpdate()
    {
        if (player == null || PlayerStats.Instance == null) return;

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        Vector2 myCenter = Center;
        Vector2 playerCenter = PlayerCenter;
        if (Vector2.Distance(myCenter, playerCenter) > hitRadius) return;

        cooldownTimer = hitCooldown;
        string source = definition != null ? (IsElite ? Loc.F("enemy.elite_name", definition.displayName) : definition.displayName) : Loc.T("damage.enemy_contact");
        if (!PlayerStats.Instance.TakeDamage(contactDamage, source)) return; // dodged

        Vector2 pushDir = playerCenter - myCenter;
        pushDir = pushDir.sqrMagnitude > 0.0001f ? pushDir.normalized : Vector2.up;
        if (playerController != null)
            playerController.ApplyKnockback(pushDir, knockbackDistance);
        else
            player.position += (Vector3)(pushDir * knockbackDistance);
    }
}
