using UnityEngine;
using UnityEngine.InputSystem;

// Movement plus Idle/Run/Attack/TakeDamage/Jump/Death sprite-frame switching.
// - Stage scenes (a StageMap exists): side-scrolling platformer. A/D move, Space/W jump
//   (gravity, lands on solid ground and one-way platforms), S drops through a platform.
// - Legacy GameScene (no StageMap): free top-down WASD movement as before.
// Input comes through GameInput (keyboard + mouse or a gamepad, Input System devices) since this project's
// Active Input Handler is set to "Input System Package (New)" only -
// the legacy Input.GetAxis()/GetKey() calls would throw at runtime here.
//
// The SpriteRenderer lives on a child (`visual`), not on this object. Every Meow Knight
// frame is auto-trimmed to a different width/height with its pivot at that frame's own
// bottom-left corner, so swapping sr.sprite directly would make the character visibly
// bob up/down (e.g. sink during the attack swing) as frame height changes. SetFrame()
// re-centers `visual` vertically every time so the on-screen center stays put regardless
// of which frame's trimmed size is showing.
public class PlayerController2D : MonoBehaviour
{
    public Transform visual;
    [Tooltip("Set by PlayerCharacterLoader. Supplies per-frame alignment so the body doesn't jump around.")]
    public CharacterDefinition character;
    public Sprite[] idleFrames;
    public Sprite[] runFrames;
    public Sprite[] attackFrames;
    public Sprite[] damageFrames;
    public Sprite[] jumpFrames;
    public Sprite[] deathFrames;
    public float framesPerSecond = 8f;
    public float attackFramesPerSecond = 20f;
    public float damageFramesPerSecond = 10f;
    public float jumpFramesPerSecond = 16f;
    public float deathFramesPerSecond = 8f;
    public float jumpCooldown = 0.6f;

    [Tooltip("Distance from this object's position (the visual center) down to the feet. Tile collision against StageMap is checked at the feet.")]
    public float feetOffset = 1.25f;
    [Tooltip("Half the width of the body box used for tile collision.")]
    public float feetHalfWidth = 0.4f;

    [Header("Side-scroll (Stage scenes)")]
    [Tooltip("Feet to top of head, for bumping into solid ceilings and side walls.")]
    public float bodyHeight = 2.4f;
    public float gravity = 32f;
    [Tooltip("14.5 with gravity 32 reaches about 3.3 tiles high.")]
    public float jumpSpeed = 14.5f;
    public float maxFallSpeed = 20f;
    [Tooltip("Upward speed given by an enemy's knockback hit.")]
    public float knockbackHopSpeed = 5f;
    [Tooltip("Extra jumps allowed in the air before landing (1 = double jump).")]
    public int airJumps = 1;
    public float airJumpSpeed = 13f;
    [Tooltip("Ranged characters keep this fraction of their walk speed while shooting (melee ones stand still).")]
    public float rangedAttackMove = 0.6f;

    [Header("Dash (right mouse button)")]
    [Tooltip("Set by PlayerCharacterLoader: the roll animation.")]
    public Sprite[] dodgeFrames;
    public float dashSpeed = 17f;
    public float dashDuration = 0.22f;
    [Tooltip("Dashes that can be used back to back (the 연속 대시 card adds up to 2 more).")]
    public int dashCharges = 2;
    [Tooltip("Seconds to get one used dash back (they come back one at a time; the 그림자 대시 card shortens it).")]
    public float dashRecharge = 1.1f;
    [Tooltip("Shortest pause between two dashes.")]
    public float dashGap = 0.08f;
    [Tooltip("Invulnerable for this long from the start of the dash.")]
    public float dashInvulnerable = 0.2f;

    bool isDashing;
    float dashTimer, dashElapsed, dashDir, dashCooldownTimer;
    int dashesLeft = -1, dashMaxSeen;   // -1 = not filled yet (first frame)
    float dashRefill;
    int airJumpsLeft;
    // Forgiving jumps: a jump pressed just after running off a ledge still counts as a ground jump (coyote time,
    // so it doesn't spend the double jump), and one pressed just before landing goes off on touchdown (buffer).
    const float CoyoteTime = 0.1f, JumpBufferTime = 0.12f;
    float coyoteTimer, jumpBufferTimer;

    float velocityY;
    bool grounded;
    float dropThroughTimer;
    Vector3 spawnPosition;

    SpriteRenderer sr;
    Vector2 moveInput;
    bool isRunning;
    int currentFrame;
    float frameTimer;

    bool isAttacking;
    float attackTimer;
    // The swing being played: attackFrames for a single attack, or one combo move.
    Sprite[] playingAttack;
    float playingAttackFps;

    // Jump attack: dive straight down holding plungeFall, then play plungeImpact on landing.
    bool isPlunging;
    Sprite[] plungeFall, plungeImpact;
    float plungeFps;
    float plungeHangTimer;
    public event System.Action PlungeLanded;

    float stepTimer;
    string stepSound = "step_grass";

    [Header("Jump attack")]
    [Tooltip("Brief stop in mid-air before diving, so the dive reads as a deliberate move.")]
    public float plungeHang = 0.12f;
    [Tooltip("Dive speed - faster than a normal fall (maxFallSpeed).")]
    public float plungeSpeed = 28f;

    bool isHit;
    float hitTimer;

    bool isJumping;
    float jumpTimer;
    float jumpCooldownTimer;

    bool isDead;

    // How long a full attack swing takes to play out. PlayerAttack uses this so its
    // click cooldown never cuts the animation off mid-swing (that's what caused the
    // "choppy" restart when clicking again before the previous swing finished).
    public float AttackDuration => (attackFrames != null && attackFrames.Length > 0) ? attackFrames.Length / attackFramesPerSecond : 0f;

    public bool IsJumping => isJumping;
    public bool IsDashing => isDashing;
    // Dash charges for the HUD: how many are ready, how many there can be, and how far the next one has refilled (0..1).
    public int DashesLeft => Mathf.Max(0, dashesLeft);
    public int MaxDashes => dashCharges + (PlayerStats.Instance != null ? PlayerStats.Instance.ExtraDashes : 0);
    public float DashRefillProgress => dashesLeft >= MaxDashes ? 1f : Mathf.Clamp01(dashRefill / DashRechargeTime);
    float DashRechargeTime => dashRecharge * (PlayerStats.Instance != null ? PlayerStats.Instance.DashCooldownMultiplier : 1f); // dash card
    // The first moments of a dash dodge attacks (checked by PlayerStats.IsInvulnerable).
    public bool IsDashInvulnerable => isDashing && dashElapsed < dashInvulnerable;
    public SpriteRenderer Renderer => sr;

    // Once dead, all input/animation is frozen on the last Death frame - checked by
    // PlayerAttack and this script itself to stop accepting further input.
    public bool IsDead => isDead;

    public bool IsAttacking => isAttacking;
    public bool IsPlunging => isPlunging;
    // In the air in a stage scene (where a jump attack is possible).
    public bool IsAirborne => SideScroll && !grounded;
    // Falls through one-way platforms for `seconds` (the bear shaking the cat off its ledge).
    public void DropThrough(float seconds) => dropThroughTimer = Mathf.Max(dropThroughTimer, seconds);
    // Frame index of the swing currently playing (valid while IsAttacking).
    public int AttackFrame => currentFrame;
    // Not dead, flinching, dashing or diving.
    public bool CanAttack => !isDead && !isHit && !isDashing && !isPlunging && !skillLock;

    // ---- Run skills (SkillSystem) drive the body while they play:
    // skillLock = no walking / jumping / dashing / attacking from input; skillHover = no gravity either.
    [System.NonSerialized] public bool skillLock;
    [System.NonSerialized] public bool skillHover;
    public bool IsGrounded => !SideScroll || grounded;
    // 공중 재도약 card: one more air jump, up to what a fresh take-off gives.
    public void RefundAirJump() => airJumpsLeft = Mathf.Min(airJumpsLeft + 1, airJumps + (PlayerStats.Instance != null ? PlayerStats.Instance.ExtraAirJumps : 0));
    public Vector3 Feet => FeetPoint;
    public event System.Action<float> DashStarted;
    public event System.Action DashEnded;

    // Moves sideways in small steps, stopping at walls. Returns how far it got.
    public float TryMove(float dx)
    {
        if (isDead || dx == 0f) return 0f;
        if (!SideScroll) { transform.position += new Vector3(dx, 0f, 0f); return dx; }
        float moved = 0f, sign = Mathf.Sign(dx);
        while (Mathf.Abs(moved) < Mathf.Abs(dx))
        {
            float step = sign * Mathf.Min(0.1f, Mathf.Abs(dx) - Mathf.Abs(moved));
            if (!MoveHorizontal(step)) break;
            moved += step;
        }
        return moved;
    }

    // Sends the body up (or down) at this speed; gravity takes over from there.
    public void Launch(float velocity)
    {
        if (isDead || !SideScroll) return;
        velocityY = velocity;
        grounded = false;
    }

    // Stops a swing / dash / dive in progress so a skill can take over.
    public void CancelActions()
    {
        isAttacking = false;
        isDashing = false;
        isPlunging = false;
        isJumping = false;
    }
    bool CanDash => SideScroll && !isDead && !isHit && !isPlunging && !isDashing && dashCooldownTimer <= 0f
        && dashesLeft > 0 && dodgeFrames != null && dodgeFrames.Length > 0;
    public float FacingSign => sr != null && sr.flipX ? -1f : 1f;

    bool SideScroll => StageMap.Instance != null;

    void Awake()
    {
        // Grass underfoot in the forest (stages 1-4, the tutorial path), stone at the boss altar and in the village.
        var run = RunManager.Instance != null ? RunManager.Instance : FindAnyObjectByType<RunManager>();
        bool forest = run != null && (run.tutorial || (run.stageNumber >= 1 && run.stageNumber <= 4));
        stepSound = forest ? "step_grass" : "step_stone";
        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
        spawnPosition = transform.position;
    }

    // Swaps the sprite and re-centers `visual` vertically so different-height trimmed
    // frames don't make the character appear to bob up/down.
    void SetFrame(Sprite frame)
    {
        sr.sprite = frame;
        if (frame == null || visual == null) return;
        // frame.bounds is in the sprite's own unscaled local units, but localPosition is
        // in the PARENT's space - has to be multiplied by visual's own localScale to land
        // in the right place. Missing this the first time round is why the previous fix
        // barely changed anything (the correction was ~15x too small).
        // Preferred: per-frame body alignment computed by the editor (see CharacterDefinition).
        if (character != null && character.TryGetVisualOffset(frame, sr.flipX, visual.localScale.x, out Vector2 aligned))
        {
            visual.localPosition = new Vector3(aligned.x, aligned.y, visual.localPosition.z);
            return;
        }

        Vector3 lp = visual.localPosition;
        lp.y = -frame.bounds.center.y * visual.localScale.y;
        // Same idea horizontally: frames have different widths, and flipX mirrors the
        // sprite around its (bottom-left) pivot, so without this the character jumps
        // sideways when turning around or when a wider frame (bow, sword swing) shows.
        float cx = frame.bounds.center.x * visual.localScale.x;
        lp.x = sr.flipX ? cx : -cx;
        visual.localPosition = lp;
    }

    // Turns the character to face left/right (used when attacking toward the mouse).
    public void FaceTowards(float directionX)
    {
        if (isDead || sr == null || Mathf.Abs(directionX) < 0.01f) return;
        sr.flipX = directionX < 0f;
    }

    // Called by PlayerAttack when the player clicks to attack. The attack animation
    // plays for its full duration (frame count / attackFramesPerSecond) regardless of
    // movement input, then normal Idle/Run resumes.
    public void TriggerAttack()
    {
        PlayAttack(attackFrames, attackFramesPerSecond);
    }

    // Plays one swing from its first frame (also used to chain combo moves: calling it
    // mid-swing starts the next move right away).
    public void PlayAttack(Sprite[] frames, float fps)
    {
        if (isDead || frames == null || frames.Length == 0) return;
        isJumping = false;
        isAttacking = true;
        playingAttack = frames;
        playingAttackFps = fps;
        attackTimer = frames.Length / fps;
        currentFrame = 0;
        frameTimer = 0f;
        SetFrame(frames[0]);
    }

    // Jump attack: fall straight down at full speed holding `fall` (played once, last frame
    // held), then play `impact` as an attack the moment the feet touch ground.
    public void StartPlunge(Sprite[] fall, Sprite[] impact, float fps)
    {
        if (isDead || !SideScroll || grounded || fall == null || fall.Length == 0) return;
        isJumping = false;
        isAttacking = false;
        isPlunging = true;
        plungeFall = fall;
        plungeImpact = impact;
        plungeFps = fps;
        plungeHangTimer = plungeHang;
        velocityY = 0f;
        currentFrame = 0;
        frameTimer = 0f;
        SetFrame(fall[0]);
    }

    void StartDash(float dir)
    {
        Vfx.Play("dust_side", FeetPoint, 1f, flipX: dir < 0f); // a streak of dust behind the dash
        Sfx.Play("dash");
        isDashing = true;
        isAttacking = false;
        isJumping = false;
        dashDir = dir;
        dashTimer = dashDuration;
        dashElapsed = 0f;
        dashesLeft--;
        sr.flipX = dir < 0f;
        velocityY = 0f;
        currentFrame = 0;
        frameTimer = 0f;
        SetFrame(dodgeFrames[0]);
        DashStarted?.Invoke(dir);
    }

    void UpdateDash(float dt)
    {
        dashElapsed += dt;
        dashTimer -= dt;
        float dashMult = PlayerStats.Instance != null ? PlayerStats.Instance.DashDistanceMultiplier : 1f; // dash card
        TryMove(dashDir * dashSpeed * dashMult * dt); // a wall simply stops it (stepped: a long frame can't skip a thin wall)
        velocityY = 0f;
        // No gravity runs during a dash, so check the floor here: dashing off a ledge must
        // count as airborne (otherwise the double-jump refill would stay available).
        grounded = HasFloorUnderFeet();
        if (dashTimer <= 0f)
        {
            isDashing = false;
            dashCooldownTimer = dashGap;
            currentFrame = 0;
            frameTimer = 0f;
            DashEnded?.Invoke();
        }
    }

    // Used dashes come back one at a time. A charge gained from a card is ready at once.
    void UpdateDashCharges(float dt)
    {
        int max = MaxDashes;
        if (dashesLeft < 0) dashesLeft = max;
        if (max > dashMaxSeen && dashMaxSeen > 0) dashesLeft += max - dashMaxSeen;
        dashMaxSeen = max;
        dashesLeft = Mathf.Min(dashesLeft, max);
        if (dashesLeft >= max) { dashRefill = 0f; return; }
        if (isDashing) return; // the refill starts once the roll is over
        dashRefill += dt;
        if (dashRefill >= DashRechargeTime)
        {
            dashRefill = 0f;
            dashesLeft++;
        }
    }

    // Small forward step during an attack (blocked by walls in stage scenes).
    public void Nudge(float dx)
    {
        if (isDead || dx == 0f) return;
        if (SideScroll) MoveHorizontal(dx);
        else transform.position += new Vector3(dx, 0f, 0f);
    }

    // Called by PlayerStats on a non-fatal hit. Interrupts whatever else is playing,
    // shows a brief hit reaction, then returns to normal Idle/Run.
    public void TriggerHit()
    {
        if (isDead || damageFrames == null || damageFrames.Length == 0) return;
        isAttacking = false;
        isJumping = false;
        isPlunging = false;
        isDashing = false;
        isHit = true;
        hitTimer = damageFrames.Length / damageFramesPerSecond;
        currentFrame = 0;
        frameTimer = 0f;
    }

    // Called by PlayerStats when a hit brings HP to 0 or below. Plays the death
    // animation once and freezes on its last frame; all input stops afterward.
    public void TriggerDeath()
    {
        if (isDead) return;
        isAttacking = false;
        isHit = false;
        isJumping = false;
        isPlunging = false;
        isDashing = false;
        isDead = true;
        currentFrame = 0;
        frameTimer = 0f;
        if (deathFrames == null || deathFrames.Length == 0) return;
        SetFrame(deathFrames[0]);
    }

    void Update()
    {
        // Card draft open: stay frozen, don't queue jumps/drops from key presses.
        if (CardDraftUI.IsPaused) return;

        if (isDead)
        {
            // Keep falling if killed mid-air.
            if (SideScroll) ApplyGravity(Time.deltaTime);
            UpdateAnimation();
            return;
        }

        Vector2 move = GameInput.Move; // keyboard or gamepad (GameInput)
        float x = move.x, y = move.y;

        if (jumpCooldownTimer > 0f) jumpCooldownTimer -= Time.deltaTime;
        float speed = PlayerStats.Instance != null ? PlayerStats.Instance.MoveSpeed : 3f;

        if (SideScroll)
        {
            moveInput = new Vector2(x, 0f);
            // Facing is locked mid-swing (the attack already turned toward the mouse) and mid-dash.
            if (moveInput.x != 0f && !isAttacking && !isPlunging && !isDashing && !skillLock) sr.flipX = moveInput.x < 0f;

            // Dash (right mouse button): also cancels an attack in progress. Uses one charge (ground or air).
            if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
            UpdateDashCharges(Time.deltaTime);
            bool dashPressed = GameInput.DashPressed;
            if (dashPressed && CanDash && !skillLock)
                StartDash(moveInput.x != 0f ? Mathf.Sign(moveInput.x) : FacingSign);

            // Footsteps while running on the ground (faster with more move speed).
            if (grounded && moveInput.x != 0f && !isDashing && !isAttacking && !isHit)
            {
                stepTimer -= Time.deltaTime * speed / 3f;
                if (stepTimer <= 0f)
                {
                    stepTimer = 0.3f;
                    Sfx.Play(stepSound);
                    if (++stepCount % 2 == 0) Vfx.Play("dust_small", FeetPoint, 0.7f, flipX: moveInput.x < 0f); // every other step kicks up a little dust
                }
            }
            else stepTimer = 0.05f;

            // Jump from the ground, or a double jump in the air; either cancels an attack.
            bool jumpPressed = GameInput.JumpPressed;
            if (grounded) coyoteTimer = CoyoteTime;
            else coyoteTimer -= Time.deltaTime;
            if (jumpPressed) jumpBufferTimer = JumpBufferTime;
            else jumpBufferTimer -= Time.deltaTime;
            bool canGroundJump = grounded || (coyoteTimer > 0f && velocityY <= 0f);
            // A fresh press: ground (or coyote) jump, else the double jump. A buffered one only fires a ground jump.
            if ((jumpPressed || (jumpBufferTimer > 0f && canGroundJump)) && !isHit && !isPlunging && !isDashing && !skillLock)
            {
                if (canGroundJump)
                {
                    jumpBufferTimer = 0f;
                    coyoteTimer = 0f;
                    velocityY = jumpSpeed;
                    grounded = false;
                    StartJump();
                    Sfx.Play("jump");
                    Vfx.Play("dust_small", FeetPoint);
                }
                else if (jumpPressed && airJumpsLeft > 0)
                {
                    jumpBufferTimer = 0f;
                    airJumpsLeft--;
                    velocityY = airJumpSpeed;
                    StartJump();
                    Sfx.Play("jump", 1f, 1.2f); // double jump: a bit higher
                    Vfx.Play("ring_small", FeetPoint + Vector3.up * 0.1f, 1f); // a puff of air under the feet
                }
            }

            bool dropPressed = GameInput.DropPressed;
            if (dropPressed && grounded && StandingOnPlatformOnly())
                dropThroughTimer = 0.25f;
            if (dropThroughTimer > 0f) dropThroughTimer -= Time.deltaTime;

            if (isDashing)
            {
                UpdateDash(Time.deltaTime); // straight line, no gravity
            }
            else
            {
                // Hit/dive stop horizontal movement but gravity always applies. A sword swing roots the knight.
                // The archer's shot is drawn standing (feet planted), so on the ground it holds still while shooting -
                // walking on would slide it; in the air it keeps drifting. Moving again between shots (or a dash) kites.
                float moveMul = !isAttacking ? 1f : character != null && character.ranged && !grounded ? rangedAttackMove : 0f;
                if (moveMul > 0f && !isHit && !isPlunging && !skillLock)
                    TryMove(moveInput.x * speed * moveMul * Time.deltaTime);
                if (isPlunging)
                {
                    // Hang for a moment, then drive straight down.
                    plungeHangTimer -= Time.deltaTime;
                    velocityY = plungeHangTimer > 0f ? gravity * Time.deltaTime : -plungeSpeed;
                }
                if (skillHover) velocityY = 0f;
                else ApplyGravity(Time.deltaTime);
                if (isPlunging && grounded)
                {
                    isPlunging = false;
                    PlayAttack(plungeImpact, plungeFps);
                    PlungeLanded?.Invoke();
                    Sfx.Play("land_heavy");
                    Vfx.Play("dust_big", FeetPoint, 1.3f);
                    Vfx.Play("ring_small", FeetPoint + Vector3.up * 0.15f, 1f);
                }
            }
            // Standing on something refills the double jump.
            if (grounded)
                airJumpsLeft = airJumps + (PlayerStats.Instance != null ? PlayerStats.Instance.ExtraAirJumps : 0); // jump card


            // Safety net: fell out of the map somehow -> back to the stage start.
            if (transform.position.y - feetOffset < -5f)
            {
                transform.position = spawnPosition;
                velocityY = 0f;
            }
        }
        else
        {
            moveInput = new Vector2(x, y).normalized;
            if (moveInput.x != 0f && !isAttacking) sr.flipX = moveInput.x < 0f;

            if (GameInput.JumpButtonPressed && !isJumping && !isHit && jumpCooldownTimer <= 0f)
                StartJump();

            // Jump keeps moving (it's a quick hop, not a full stop); only attack/hit freeze movement.
            if (!isAttacking && !isHit)
                transform.position += (Vector3)(moveInput * speed * Time.deltaTime);
        }

        UpdateAnimation();
    }

    // Called by Enemy on a contact hit. Side-scroll: pushed horizontally away (blocked by
    // walls) with a small hop. Top-down legacy: pushed straight along the direction.
    public void ApplyKnockback(Vector2 direction, float distance)
    {
        if (!SideScroll)
        {
            transform.position += (Vector3)(direction * distance);
            return;
        }

        float sign = direction.x >= 0f ? 1f : -1f;
        // Move in small steps so a big push can't skip over a 1-tile wall.
        float remaining = distance;
        while (remaining > 0f)
        {
            float step = Mathf.Min(0.1f, remaining);
            if (!MoveHorizontal(sign * step)) break;
            remaining -= step;
        }
        if (grounded)
        {
            velocityY = knockbackHopSpeed;
            grounded = false;
        }
    }

    // ---------- side-scroll collision (StageMap tile lookups, no physics engine) ----------

    float FeetY => transform.position.y - feetOffset;
    Vector3 FeetPoint => new Vector3(transform.position.x, FeetY, 0f);
    int stepCount;

    // Returns false if a solid tile blocked the move.
    bool MoveHorizontal(float dx)
    {
        if (dx == 0f) return true;
        var map = StageMap.Instance;
        float newX = transform.position.x + dx;
        float edgeX = newX + (dx > 0f ? feetHalfWidth : -feetHalfWidth);
        float feet = FeetY;
        if (map.IsSolid(new Vector2(edgeX, feet + 0.05f)) ||
            map.IsSolid(new Vector2(edgeX, feet + bodyHeight * 0.5f)) ||
            map.IsSolid(new Vector2(edgeX, feet + bodyHeight - 0.05f)))
            return false;

        Vector3 p = transform.position;
        p.x = newX;
        transform.position = p;
        return true;
    }

    void ApplyGravity(float dt)
    {
        var map = StageMap.Instance;
        velocityY = Mathf.Max(velocityY - gravity * dt, isPlunging ? -plungeSpeed : -maxFallSpeed);
        float dy = velocityY * dt;
        Vector3 p = transform.position;
        float feet = p.y - feetOffset;

        if (dy <= 0f)
        {
            if (TryFindLanding(map, feet, feet + dy, out float surface))
            {
                p.y = surface + feetOffset;
                // A real fall kicks up a puff of dust where the feet land (not every step on a slope).
                if (!grounded && velocityY < -7f && !isPlunging) Vfx.Play("dust_mid", new Vector3(p.x, surface, 0f));
                velocityY = 0f;
                grounded = true;
            }
            else
            {
                p.y += dy;
                grounded = false;
            }
        }
        else
        {
            float head = feet + bodyHeight + dy;
            if (map.IsSolid(new Vector2(p.x - feetHalfWidth, head)) || map.IsSolid(new Vector2(p.x + feetHalfWidth, head)))
                velocityY = 0f; // bonked a solid ceiling (platforms are passed through from below)
            else
                p.y += dy;
            grounded = false;
        }
        transform.position = p;
    }

    // Looks for the highest tile top between the feet's old and new height (tiles are
    // 1 unit, so tile tops sit on whole numbers). Platforms only count when not dropping
    // through - and since we only ever land moving down, they're naturally one-way.
    bool TryFindLanding(StageMap map, float fromFeet, float toFeet, out float surface)
    {
        float x = transform.position.x;
        for (float s = Mathf.Floor(fromFeet + 0.001f); s >= toFeet; s -= 1f)
        {
            for (int i = 0; i < 2; i++)
            {
                var probe = new Vector2(i == 0 ? x - feetHalfWidth : x + feetHalfWidth, s - 0.5f);
                if (map.IsSolid(probe) || (dropThroughTimer <= 0f && map.IsPlatform(probe)))
                {
                    surface = s;
                    return true;
                }
            }
        }
        surface = 0f;
        return false;
    }

    bool HasFloorUnderFeet()
    {
        var map = StageMap.Instance;
        float y = FeetY - 0.05f;
        float x = transform.position.x;
        for (int i = 0; i < 2; i++)
        {
            var probe = new Vector2(i == 0 ? x - feetHalfWidth : x + feetHalfWidth, y);
            if (map.IsSolid(probe) || (dropThroughTimer <= 0f && map.IsPlatform(probe))) return true;
        }
        return false;
    }

    bool StandingOnPlatformOnly()
    {
        var map = StageMap.Instance;
        float y = FeetY - 0.5f;
        float x = transform.position.x;
        bool onSolid = map.IsSolid(new Vector2(x - feetHalfWidth, y)) || map.IsSolid(new Vector2(x + feetHalfWidth, y));
        return !onSolid;
    }

    void StartJump()
    {
        if (jumpFrames == null || jumpFrames.Length == 0) return;
        isAttacking = false;
        isJumping = true;
        jumpTimer = jumpFrames.Length / jumpFramesPerSecond;
        jumpCooldownTimer = jumpCooldown;
        currentFrame = 0;
        frameTimer = 0f;
    }

    void UpdateAnimation()
    {
        if (isDead)
        {
            if (deathFrames == null || deathFrames.Length == 0) return;
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / deathFramesPerSecond && currentFrame < deathFrames.Length - 1)
            {
                frameTimer = 0f;
                currentFrame++;
            }
            SetFrame(deathFrames[currentFrame]);
            return;
        }

        if (isHit)
        {
            hitTimer -= Time.deltaTime;
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / damageFramesPerSecond)
            {
                frameTimer = 0f;
                currentFrame = Mathf.Min(currentFrame + 1, damageFrames.Length - 1);
            }
            SetFrame(damageFrames[currentFrame]);

            if (hitTimer <= 0f)
            {
                isHit = false;
                currentFrame = 0;
                frameTimer = 0f;
            }
            return;
        }

        if (isDashing)
        {
            // The whole roll plays once over the dash.
            int f = Mathf.Min(dodgeFrames.Length - 1, (int)(dashElapsed / dashDuration * dodgeFrames.Length));
            SetFrame(dodgeFrames[f]);
            return;
        }

        if (isPlunging)
        {
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / plungeFps)
            {
                frameTimer = 0f;
                currentFrame = Mathf.Min(currentFrame + 1, plungeFall.Length - 1);
            }
            SetFrame(plungeFall[currentFrame]);
            return;
        }

        if (isJumping)
        {
            jumpTimer -= Time.deltaTime;
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / jumpFramesPerSecond)
            {
                frameTimer = 0f;
                currentFrame = Mathf.Min(currentFrame + 1, jumpFrames.Length - 1);
            }
            SetFrame(jumpFrames[currentFrame]);

            if (jumpTimer <= 0f)
            {
                isJumping = false;
                currentFrame = 0;
                frameTimer = 0f;
            }
            return;
        }

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            frameTimer += Time.deltaTime;
            float frameTime = 1f / playingAttackFps;
            // Subtract instead of resetting so the swing keeps its real speed at low fps.
            while (frameTimer >= frameTime)
            {
                frameTimer -= frameTime;
                currentFrame = Mathf.Min(currentFrame + 1, playingAttack.Length - 1);
            }
            SetFrame(playingAttack[currentFrame]);

            if (attackTimer <= 0f)
            {
                isAttacking = false;
                currentFrame = 0;
                frameTimer = 0f;
            }
            return;
        }

        // Airborne after the jump animation finished (or walked off a ledge): hold a
        // mid-jump pose until landing instead of running in mid-air.
        if (SideScroll && !grounded && jumpFrames != null && jumpFrames.Length > 0)
        {
            SetFrame(jumpFrames[jumpFrames.Length / 2]);
            isRunning = false;
            return;
        }

        bool wantsRun = moveInput.sqrMagnitude > 0.01f;
        Sprite[] frames = wantsRun ? runFrames : idleFrames;
        if (frames == null || frames.Length == 0) return;

        // Coming back from a jump / hit / attack pose: its frame index may be past the end of this loop.
        if (wantsRun != isRunning || currentFrame >= frames.Length)
        {
            isRunning = wantsRun;
            currentFrame = 0;
            frameTimer = 0f;
        }

        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / framesPerSecond)
        {
            frameTimer = 0f;
            currentFrame = (currentFrame + 1) % frames.Length;
        }
        SetFrame(frames[currentFrame]);
    }
}
