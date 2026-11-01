using UnityEngine;
using UnityEngine.InputSystem;

// Left mouse click attacks toward the mouse cursor.
// - Characters with a combo (CharacterDefinition.combo): each click continues the combo.
//   Clicking during a move queues the next one, which starts as soon as the current one
//   finishes (cutting off its return-to-idle frames); clicking shortly after a move ends
//   also continues. On each move's hit frame a melee move hits enemies within the blade's
//   reach (read from the art) in front of the player, a ranged move releases its arrow.
// - Clicking in the air: the character's jump attack (dive, then a landing shockwave).
// - No combo (legacy GameScene / old data): one swing or shot as before.
[RequireComponent(typeof(PlayerController2D))]
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("Legacy single swing: extra reach beyond body contact (plus the enemy's hitRadius).")]
    public float attackRange = 0.8f;
    public float attackCooldown = 0.4f;
    [Range(0f, 180f)] public float attackAngle = 100f;

    [Header("Ranged (set by PlayerCharacterLoader for archer characters)")]
    public bool ranged;
    public Sprite projectileSprite;
    public float projectileSpeed = 16f;
    public float projectileRange = 14f;
    public float releaseDelay = 0.35f;
    public float projectileScale = 15f;

    float cooldownTimer;
    Camera cam;
    PlayerController2D animator;
    SpriteRenderer sr;

    // Combo state.
    int step = -1;          // move being played, -1 = none
    int lastStep = -1;      // last move that finished, for continuing the combo
    float comboTimer;       // time left to continue after a move finished
    bool queued;            // clicked during the current move
    bool hitDone;
    float lungeLeft;        // world units still to step forward this move
    bool plunging;          // jump attack: diving, or playing the landing impact
    bool plungeLanded;
    Vector2 moveAim;        // mouse direction when the current move started

    AttackMove[] Combo => animator != null && animator.character != null ? animator.character.combo : null;
    bool UseCombo => Combo != null && Combo.Length > 0;
    CharacterDefinition Character => animator != null ? animator.character : null;
    bool HasJumpAttack => Character != null && Character.jumpAttack != null
        && Character.jumpAttack.frames != null && Character.jumpAttack.frames.Length > 0
        && Character.jumpAttackFallFrames != null && Character.jumpAttackFallFrames.Length > 0;

    void Awake()
    {
        cam = Camera.main;
        animator = GetComponent<PlayerController2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        animator.PlungeLanded += () =>
        {
            plungeLanded = true;
            CameraFollow.Shake(0.25f, 0.18f);
            CameraFollow.Kick(Vector2.down, 0.2f); // the slam pushes the view down
        };
    }

    // Clicking a UI button (e.g. the tutorial's skip button) must not also swing the sword.
    static bool PointerOverUi() =>
        UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

    void Update()
    {
        // Card draft open: clicking a card must not also swing/shoot.
        if (CardDraftUI.IsPaused) return;
        // A run skill (SkillSystem) is playing: it owns the body until it ends.
        if (SkillSystem.Instance != null && SkillSystem.Instance.BlocksAttack) return;
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (animator != null && animator.IsDead) { step = -1; return; }

        bool click = (GameInput.AttackClicked && !PointerOverUi()) || GameInput.AttackPadPressed;

        if (UseCombo)
        {
            UpdateCombo(click);
            return;
        }

        if (click && cooldownTimer <= 0f)
        {
            // Never let the cooldown be shorter than the swing animation itself,
            // or clicking again mid-swing restarts it from frame 0 and it looks choppy.
            float animDuration = animator != null ? animator.AttackDuration : 0f;
            cooldownTimer = Mathf.Max(attackCooldown, animDuration);
            Attack();
        }
    }

    // ------------------------------------------------------------------ melee combo
    void UpdateCombo(bool click)
    {
        var combo = Combo;
        if (comboTimer > 0f) comboTimer -= Time.deltaTime;

        if (plunging)
        {
            UpdatePlunge();
            return;
        }
        // Clicking in the air is the jump attack (a dive), never a ground combo move.
        if (click && step < 0 && HasJumpAttack && animator.IsAirborne && animator.CanAttack)
        {
            StartPlunge();
            return;
        }

        // The current move ended (played out, or cut off by a hit / dash / jump). A move
        // cancelled into a dash or a jump drops the queued click: the combo starts over.
        if (step >= 0 && !animator.IsAttacking)
        {
            int finished = step;
            step = -1;
            if (queued && animator.CanAttack && !animator.IsAirborne)
            {
                StartMove((finished + 1) % combo.Length);
            }
            else
            {
                lastStep = finished;
                comboTimer = animator.CanAttack && !animator.IsAirborne ? animator.character.comboWindow : 0f;
                queued = false;
            }
        }

        if (click)
        {
            if (step >= 0) queued = true;
            else if (animator.CanAttack)
                StartMove(comboTimer > 0f && lastStep >= 0 ? (lastStep + 1) % combo.Length : 0);
        }

        if (step < 0) return;
        var move = combo[step];
        int frame = animator.AttackFrame;

        // The step forward lands with the strike frame, where the drawn feet step too: gliding it in over the
        // wind-up frames (feet still planted in the drawing) slid the cat along the ground.
        if (!hitDone && frame >= move.hitFrame)
        {
            if (lungeLeft > 0f) { animator.Nudge(lungeLeft * animator.FacingSign); lungeLeft = 0f; }
            hitDone = true;
            HitWith(move);
        }

        // Queued next move: skip this move's return-to-idle frames.
        if (queued && move.recoveryFrames > 0 && frame >= move.frames.Length - move.recoveryFrames)
            StartMove((step + 1) % combo.Length);
    }

    void StartPlunge()
    {
        Sfx.Play("dash", 1f, 0.8f); // dive whoosh
        plunging = true;
        plungeLanded = false;
        hitDone = false;
        queued = false;
        animator.FaceTowards(AimDirection().x);
        animator.StartPlunge(Character.jumpAttackFallFrames, Character.jumpAttack.frames, Character.jumpAttack.framesPerSecond);
    }

    void UpdatePlunge()
    {
        var move = Character.jumpAttack;
        // Cut off by a hit (or anything else) before landing.
        if (!animator.IsPlunging && !plungeLanded) { EndPlunge(); return; }
        if (!plungeLanded) return;

        if (!hitDone && animator.IsAttacking && animator.AttackFrame >= move.hitFrame)
        {
            hitDone = true;
            HitWith(move);
            if (SkillSystem.Instance != null) SkillSystem.Instance.OnJumpAttackImpact(); // 캣타워 점프 찍기
        }
        if (!animator.IsAttacking) EndPlunge();
    }

    // A run skill took over the body: drop the combo / dive in progress.
    public void CancelCombo()
    {
        step = -1;
        lastStep = -1;
        queued = false;
        hitDone = true;
        comboTimer = 0f;
        plunging = false;
        plungeLanded = false;
    }

    // The ground combo starts over after a jump attack.
    void EndPlunge()
    {
        plunging = false;
        plungeLanded = false;
        lastStep = -1;
        comboTimer = 0f;
    }

    void StartMove(int index)
    {
        var move = Combo[index];
        step = index;
        queued = false;
        hitDone = false;
        comboTimer = 0f;
        lungeLeft = move.lunge * VisualScale;
        // Each move can turn toward the mouse, so the combo can follow a moving enemy.
        moveAim = AimDirection();
        animator.FaceTowards(moveAim.x);
        animator.PlayAttack(move.frames, move.framesPerSecond * (PlayerStats.Instance != null ? PlayerStats.Instance.AttackSpeedMultiplier : 1f)); // haste card
    }

    // Enemies whose body overlaps the blade's reach in front (or all around) and the
    // player's height band.
    void HitWith(AttackMove move)
    {
        if (PlayerStats.Instance == null) return;
        if (move.IsRanged)
        {
            Shoot(move);
            return;
        }
        Sfx.Play("swing", 1f, move.damageMultiplier > 1f ? 0.85f : 1f); // heavier moves swing lower
        Vector2 me = transform.position; // body centre (the visual is aligned to it)
        float facing = animator.FacingSign;
        float reach = move.reach * VisualScale * PlayerStats.Instance.ReachMultiplier; // reach card
        float feet = me.y - animator.feetOffset;
        float top = feet + animator.bodyHeight + 0.3f;
        int damage = PlayerStats.Instance.RollDamage(move.damageMultiplier, out bool crit); // finisher + crit cards
        damage = Mathf.Max(1, Mathf.RoundToInt(damage * SkillSystem.MeleeMultiplier(PlayerStats.Instance))); // 솜방망이 skill
        SwingEffect(move, me, facing, reach);
        EnemyWeb.SwatAround(me + new Vector2(facing * reach * 0.5f, 0f), reach * 0.6f + 0.4f); // a swing cuts spit webs out of the air
        CardRewardChest.TryMeleeHit(me, facing, reach, feet, top);
        // The combo's last swing is the finisher: it lands with a longer stop and a harder shove (Enemy.ReceiveDamage).
        bool finisher = Combo != null && Combo.Length >= 3 && step == Combo.Length - 1 && !move.hitsAllAround;

        var enemies = Enemy.All;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];
            if (enemy.IsDead) continue;
            float dx = enemy.BodyX - me.x;
            float half = enemy.BodyHalfWidth;
            if (Mathf.Abs(dx) - half > reach) continue;
            // Behind us only counts for all-around moves, or when standing inside it.
            bool behind = dx * facing < 0f && Mathf.Abs(dx) > half + animator.feetHalfWidth;
            if (behind && !move.hitsAllAround) continue;

            var esr = enemy.Renderer;
            if (esr != null && (esr.bounds.max.y < feet || esr.bounds.min.y > top)) continue;

            // Knock away from us; heavier moves (thrust, slam) push harder.
            float pushDir = move.hitsAllAround && Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : facing;
            bool strong = move.damageMultiplier > 1f || crit;
            enemy.ReceiveDamage(damage, pushDir * move.damageMultiplier, strong, crit, finisher: finisher);
            if (SkillSystem.Instance != null) SkillSystem.Instance.OnMeleeHit(enemy);
            if (strong) CameraFollow.Shake(0.12f, 0.1f);
        }
    }

    // The swing's smear (pimen's Smear VFX 01, 48 px at the cat's pixel size), laid over the smear the knight's own
    // frames draw on the hit frame: 내려베기 - the arc from over the head down to the front foot; 올려베기 - the same
    // arc mirrored upside down (from the foot up); 찌르기 - the long flat thrust streak. White, gold with 솜방망이.
    // Offsets are in the cat's pixels (0.08 units) from the body centre, forward / up, matched to the drawn smears.
    static readonly Vector2[] SmearOffset = { new Vector2(11f, 3f), new Vector2(9f, 3f), new Vector2(16f, 0f) };
    const float CatPixel = 0.08f;

    void SwingEffect(AttackMove move, Vector2 me, float facing, float reach)
    {
        if (move.hitsAllAround) return;
        int s = Mathf.Clamp(step, 0, 2);
        bool gold = PlayerStats.Instance != null && PlayerStats.Instance.CardLevel("k_paw") > 0;
        string id = (s == 2 ? "smear_thrust" : "smear_arc") + (gold ? "_gold" : "");
        var o = SmearOffset[s] * CatPixel;
        var at = new Vector3(me.x + facing * o.x, me.y + o.y, 0f);
        bool up = s == 1;
        // Upside down = a half turn plus a horizontal mirror (Vfx only flips sideways).
        bool flip = up ? facing > 0f : facing < 0f;
        Vfx.Play(id, at, 1f, flipX: flip, rotationZ: up ? 180f : 0f);
        if (SkillSystem.Instance != null) SkillSystem.Instance.OnMeleeSwing(at, 1f, facing < 0f, 0f); // 솜방망이 sparkle
    }

    // Releases the move's arrow from the bow toward where the mouse was when the move began.
    void Shoot(AttackMove move)
    {
        Sfx.Play("arrow");
        float facing = animator.FacingSign;
        Vector2 dir = moveAim;
        if (dir.x * facing < 0f) dir.x = -dir.x; // never fire out of the back of the bow
        Vector3 origin = transform.position + new Vector3(move.projectileSpawn.x * facing, move.projectileSpawn.y, 0f) * VisualScale;
        int damage = PlayerStats.Instance.RollDamage(move.damageMultiplier, out bool crit);
        if (SkillSystem.Instance != null) { SkillSystem.Instance.ShootBasic(move, origin, dir, damage, crit); return; } // arrow skills
        var arrow = ArrowProjectile.Spawn(move.projectileFrames, move.projectileFps, move.projectilePierces, origin, dir,
            projectileSpeed * move.projectileSpeedMultiplier, projectileRange * PlayerStats.Instance.ReachMultiplier, damage, VisualScale, crit);
        arrow.opensChest = true;
    }

    float VisualScale => animator != null && animator.visual != null ? Mathf.Abs(animator.visual.lossyScale.x) : 1f;

    Vector2 AimDirection()
    {
        if (cam == null) cam = Camera.main;
        // The mouse, or the gamepad's right stick / facing (GameInput).
        Vector2 aim = GameInput.AimPoint(cam, transform.position, animator.FacingSign, 4f);
        Vector2 dir = aim - (Vector2)transform.position;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : new Vector2(animator.FacingSign, 0f);
    }

    // ------------------------------------------------------------------ single attack
    void Attack()
    {
        if (animator != null) animator.TriggerAttack();

        if (PlayerStats.Instance == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Use bounds.center (the sprite's actual rendered center for whatever frame is
        // showing), not transform.position - in the legacy scene the frames are trimmed to
        // different sizes, so transform.position alone drifts from where the character is.
        Vector2 myCenter = sr != null ? (Vector2)sr.bounds.center : (Vector2)transform.position;

        Vector2 aimPoint = GameInput.AimPoint(cam, myCenter, animator != null ? animator.FacingSign : 1f, 4f);
        Vector2 aimDir = (aimPoint - myCenter).normalized;
        if (animator != null) animator.FaceTowards(aimDir.x);

        if (ranged)
        {
            StartCoroutine(FireArrowAfterDelay(aimDir));
            return;
        }

        var enemies = Enemy.All.ToArray(); // a copy: a legacy enemy may be destroyed while we loop
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            var enemySr = enemy.Renderer;
            Vector2 enemyCenter = enemySr != null ? (Vector2)enemySr.bounds.center : (Vector2)enemy.transform.position;

            Vector2 toEnemy = enemyCenter - myCenter;
            float dist = toEnemy.magnitude;
            // Bigger enemies (e.g. the boss) have a bigger hitRadius, so they're
            // reachable from further out - same body-size scaling Enemy.cs uses for
            // its own contact check.
            if (dist > attackRange + enemy.hitRadius) continue;

            bool facingIt = dist < 0.05f || Vector2.Angle(aimDir, toEnemy.normalized) <= attackAngle * 0.5f;
            if (facingIt)
                enemy.ReceiveDamage(PlayerStats.Instance.AttackPower);
        }
    }

    // The arrow leaves the bow partway into the draw animation, not on the click itself.
    System.Collections.IEnumerator FireArrowAfterDelay(Vector2 aimDir)
    {
        yield return new WaitForSeconds(releaseDelay);
        if (animator != null && animator.IsDead) yield break;
        if (PlayerStats.Instance == null) yield break;
        Sfx.Play("arrow");

        Vector3 origin = sr != null ? sr.bounds.center : transform.position;
        origin += (Vector3)(aimDir * 0.6f);
        var arrow = ArrowProjectile.Spawn(projectileSprite, origin, aimDir, projectileSpeed, projectileRange, PlayerStats.Instance.AttackPower, projectileScale);
        arrow.opensChest = true;
    }
}
