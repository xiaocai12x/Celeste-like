using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, IMovingPlatformRider
{
    public PlayerStateMachine StateMachine { get; private set; }
    public PlayerData playerData;

    public Rigidbody2D RB { get; private set; }
    public Collider2D Col { get; private set; }
    public Animator Anim { get; private set; }
    public SpriteRenderer SR { get; private set; }
    public PlayerAnimator PlayerAnim { get; private set; }
    public PlayerAfterImagePool AfterImage { get; private set; }

    public PlayerInputActions InputHandler { get; private set; }

    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerInAirState InAirState { get; private set; }
    public PlayerDashState DashState { get; private set; }
    public PlayerWallSlideState WallSlideState { get; private set; }
    public PlayerWallGrabState WallGrabState { get; private set; }
    public PlayerCutsceneState CutsceneState { get; private set; }
    public PlayerSpawnState SpawnState { get; private set; }

    public bool IsResumingState { get; private set; }

    private int _lastPlatformFrame;

    public Vector2 CurrentInput { get; private set; }

    public Transform wallCheck;
    public float wallCheckDistance = 0.5f;
    public LayerMask whatIsWall;

    public Transform groundCheck;
    public float groundCheckRadius = 0.3f;
    public LayerMask whatIsGround;

    public AudioClip hitSFX;
    public AudioClip explosionSFX;
    private AudioSource audioSource;

    public LayerMask whatIsSpikes;
    public LayerMask whatIsInvisibleBarrier;

    public GameObject Visuals;

    private float defaultGravity;
    private float defaultDrag;

    public bool IsWallJumping { get; private set; }
    private float wallJumpStartTime;
    public float inputLockTimer;

    private float lastWallExitTime;
    private float wallBounceEndTime;
    private int lastJumpWallDir;
    private bool isWallKickBackstepping;

    private PlayerState stateBeforeTransition;
    private Vector2 velocityBeforeTransition;
    private float dashAttackTimerBefore;
    private float varJumpTimerBefore;
    private float stateElapsedBefore;

    public static event System.Action<PlayerController, Vector3, DeathStrategy> OnPlayerDied;

    public bool IsJumping { get; set; }
    public bool IsGolden { get; set; }
    public bool JumpInput { get; private set; }
    public bool IsGrounded { get; private set; }
    public float DefaultGravity { get; private set; }
    public float LastOnGroundTime { get; private set; }
    public float JumpInputStartTime { get; private set; }
    public float DashInputStartTime { get; private set; }
    public bool IsTouchingWall { get; private set; }
    public float CurrentStamina { get; private set; }
    public bool GrabInput { get; private set; }
    public bool IsStartingTransition { get; private set; }
    public float LastDashEndTime { get; private set; } = -100f;
    public bool IsDead;

    public int FacingDirection
    {
        get
        {
            if (SR == null) return 1;
            return SR.flipX ? -1 : 1;
        }
    }

    public float DashAttackTimer;
    public float VarJumpTimer;

    public SoundData jumpSound;
    public SoundData dashSound;
    public SoundData wallJumpSound;
    public SoundData landSound;
    public SoundData wallSlideSound;
    public SoundData runStepSound;

    public float stepInterval = 0.28f;

    private AudioSource wallLoopSource;

    private void Awake()
    {
        StateMachine = new PlayerStateMachine();

        IdleState = new PlayerIdleState(this, StateMachine, playerData, "Idle");
        RunState = new PlayerRunState(this, StateMachine, playerData, "Run");
        JumpState = new PlayerJumpState(this, StateMachine, playerData, "Jump");
        InAirState = new PlayerInAirState(this, StateMachine, playerData, "InAir");
        DashState = new PlayerDashState(this, StateMachine, playerData, "Dash");
        WallSlideState = new PlayerWallSlideState(this, StateMachine, playerData, "WallSlide");
        WallGrabState = new PlayerWallGrabState(this, StateMachine, playerData, "WallGrab");
        CutsceneState = new PlayerCutsceneState(this, StateMachine, playerData, "Run");
        SpawnState = new PlayerSpawnState(this, StateMachine, playerData, "Spawn");

        RB = GetComponent<Rigidbody2D>();
        Col = GetComponent<Collider2D>();
        Anim = GetComponentInChildren<Animator>();
        SR = GetComponentInChildren<SpriteRenderer>();
        PlayerAnim = GetComponent<PlayerAnimator>();
        AfterImage = GetComponent<PlayerAfterImagePool>();

        InputHandler = new PlayerInputActions();
        LoadBindingOverrides();

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        defaultGravity = RB.gravityScale;
        defaultDrag = RB.linearDamping;

        DashInputStartTime = float.MinValue;
        JumpInputStartTime = float.MinValue;

        wallLoopSource = gameObject.AddComponent<AudioSource>();
        wallLoopSource.playOnAwake = false;
        wallLoopSource.spatialBlend = 0f;
    }

    private void Start()
    {
        DefaultGravity = RB.gravityScale;
        ResetStamina();
        StateMachine.Initialize(IdleState);
    }

    private void OnEnable() => InputHandler.Enable();
    private void OnDisable() => InputHandler.Disable();

    private void Update()
    {
        if (IsDead) return;

        UpdateVisualsOnly();

        if (StateMachine.CurrentState == CutsceneState)
        {
            CurrentInput = Vector2.zero;
            return;
        }

        UpdateTimers();
        HandleInput();
        UpdateWallStatus();
        StateMachine.CurrentState.LogicUpdate();

        if (CheckIfGrounded()) LastOnGroundTime = Time.time;
        HandleStaminaRecovery();
    }

    private void FixedUpdate()
    {
        if (IsDead) return;

        StateMachine.CurrentState.PhysicsUpdate();
        CheckIfTouchingWall();
    }

    private void HandleInput()
    {
        if (inputLockTimer > 0)
        {
            inputLockTimer -= Time.deltaTime;
            CurrentInput = Vector2.zero;
            return;
        }

        CurrentInput = InputHandler.Gameplay.Move.ReadValue<Vector2>();
        GrabInput = InputHandler.Gameplay.Grab.IsPressed();

        if (InputHandler.Gameplay.Jump.WasPressedThisFrame())
        {
            JumpInput = true;
            JumpInputStartTime = Time.time;
        }

        if (InputHandler.Gameplay.Dash.WasPressedThisFrame())
        {
            DashInputStartTime = Time.time;
        }
    }

    private void UpdateVisualsOnly()
    {
        UpdateFlashColor();
    }

    private void UpdateTimers()
    {
        if (DashAttackTimer > 0) DashAttackTimer -= Time.deltaTime;
        if (VarJumpTimer > 0) VarJumpTimer -= Time.deltaTime;
    }

    public void UseJumpInput() { JumpInput = false; JumpInputStartTime = float.MinValue; }
    public bool IsJumpChecking => InputHandler.Gameplay.Jump.IsPressed();
    public void UseDashInput() => DashInputStartTime = float.MinValue;
    public bool CheckDashInput() => Time.time - DashInputStartTime <= playerData.dashInputBufferTime;

    public void SetVelocityX(float velocity) => RB.linearVelocity = new Vector2(velocity, RB.linearVelocity.y);
    public void SetVelocityY(float velocity) => RB.linearVelocity = new Vector2(RB.linearVelocity.x, velocity);
    public void SetVelocityZero() => RB.linearVelocity = Vector2.zero;
    public void SetGravityScale(float scale) => RB.gravityScale = scale;

    public bool CheckIfGrounded()
    {
        if (Time.frameCount == _lastPlatformFrame) return true;
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, whatIsGround);
    }

    private void CheckIfTouchingWall()
    {
        Bounds bounds = Col.bounds;
        float xOrigin = (FacingDirection == 1) ? (bounds.max.x + 0.01f) : (bounds.min.x - 0.01f);
        Vector2 origin = new Vector2(xOrigin, bounds.center.y);
        Vector2 direction = Vector2.right * FacingDirection;
        IsTouchingWall = Physics2D.Raycast(origin, direction, wallCheckDistance, whatIsWall);
    }

    private void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Bounds bounds = col.bounds;

        int faceDir = 1;
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.flipX) faceDir = -1;

        float xOrigin = (faceDir == 1) ? (bounds.max.x + 0.01f) : (bounds.min.x - 0.01f);
        Vector2 origin = new Vector2(xOrigin, bounds.center.y);
        Vector2 direction = Vector2.right * faceDir;

        Gizmos.color = IsTouchingWall ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + direction * wallCheckDistance);

        float offset = 0.15f;
        float dUp = playerData != null ? playerData.cornerCorrectionDistance : 0.05f;
        float yTop = bounds.max.y + 0.01f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector2(bounds.min.x + offset, yTop), new Vector2(bounds.min.x + offset, yTop + dUp));
        Gizmos.DrawLine(new Vector2(bounds.max.x - offset, yTop), new Vector2(bounds.max.x - offset, yTop + dUp));

        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector2(bounds.center.x, yTop), new Vector2(bounds.center.x, yTop + dUp));

        float dDown = 0.1f;
        float yBot = bounds.min.y + 0.05f;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector2(bounds.min.x + offset, yBot), new Vector2(bounds.min.x + offset, yBot - dDown));
        Gizmos.DrawLine(new Vector2(bounds.max.x - offset, yBot), new Vector2(bounds.max.x - offset, yBot - dDown));

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(new Vector2(bounds.center.x, yBot), new Vector2(bounds.center.x, yBot - dDown));
    }

    public bool CheckJumpInput() => Time.time - JumpInputStartTime <= playerData.jumpBufferTime;

    public void SetWallJumpVelocity(Vector2 velocity)
    {
        RB.linearVelocity = velocity;
        if (velocity.x != 0) SR.flipX = (velocity.x < 0);
        IsWallJumping = true;
        wallJumpStartTime = Time.time;
    }

    public void PerformWallBounce(int wallDir)
    {
        float dirX = -wallDir;
        float bounceSpeedX = 170f / 8f;
        float bounceSpeedY = 160f / 8f;

        SetVelocityX(bounceSpeedX * dirX);
        SetVelocityY(bounceSpeedY);

        VarJumpTimer = 15f / 60f;

        if (PlayerAnim != null) PlayerAnim.PlayJump();

        IsWallJumping = false;

        if (SR != null) SR.flipX = (dirX < 0);
    }

    public void CheckWallJumpLock()
    {
        if (IsWallJumping && Time.time >= wallJumpStartTime + playerData.wallJumpTime)
            IsWallJumping = false;
    }

    public void CheckFlip()
    {
        if (isWallKickBackstepping) return;

        if (CurrentInput.x != 0 && SR != null)
        {
            SR.flipX = (CurrentInput.x < 0);
        }
    }

    public void ResetStamina() => CurrentStamina = playerData.maxStamina;

    public void DecreaseStamina(float amount)
    {
        CurrentStamina -= amount;
        if (CurrentStamina < 0) CurrentStamina = 0;
    }

    public void HandleStaminaRecovery()
    {
        if (CheckIfGrounded()) ResetStamina();
    }

    public void PerformWallJump()
    {
        int wallDir = IsTouchingWall ? FacingDirection : 0;

        if (wallDir == 0)
        {
            if (Physics2D.Raycast(wallCheck.position, Vector2.right, wallCheckDistance, whatIsWall)) wallDir = 1;
            else if (Physics2D.Raycast(wallCheck.position, Vector2.left, wallCheckDistance, whatIsWall)) wallDir = -1;
            if (wallDir == 0) wallDir = lastJumpWallDir;
        }

        bool canWallJump = IsTouchingWall || (Time.time - lastWallExitTime <= playerData.wallMemoryTime);

        if (!canWallJump) return;

        bool isComboJump = (Time.time < wallBounceEndTime) && !GrabInput;

        int xInput = Mathf.RoundToInt(CurrentInput.x);

        if (isComboJump)
        {
            Vector2 jumpForce = new Vector2(-wallDir * playerData.wallJumpOff.x, playerData.wallJumpOff.y);
            SetWallJumpVelocity(jumpForce);
        }
        else if (GrabInput)
        {
            if (CurrentStamina <= 0) return;
            DecreaseStamina(playerData.wallJumpStaminaCost);
            RB.linearVelocity = new Vector2(0, playerData.wallJumpClimb.y);
        }
        else
        {
            if (CurrentStamina <= 0) return;
            DecreaseStamina(playerData.wallJumpStaminaCost);

            Vector2 jumpForce = new Vector2(-wallDir * playerData.wallJumpOff.x, playerData.wallJumpOff.y);

            if (xInput == 0)
            {
                isWallKickBackstepping = true;
                RB.linearVelocity = jumpForce;
            }
            else
            {
                isWallKickBackstepping = false;
                SetWallJumpVelocity(jumpForce);
                if (SR != null) SR.flipX = (jumpForce.x < 0);
            }
        }

        inputLockTimer = playerData.wallJumpTime;

        wallBounceEndTime = Time.time + playerData.wallBounceWindow;

        lastJumpWallDir = wallDir;
        IsJumping = true;
        IsWallJumping = true;
        wallJumpStartTime = Time.time;
        UseJumpInput();
        Anim.SetBool("Jump", true);

        if (PlayerAnim != null) PlayerAnim.PlayWallJump();

        if (wallJumpSound != null)
        {
            AudioManager.Instance.PlaySFX(wallJumpSound, transform.position);
        }
    }
    private void UpdateWallStatus()
    {
        if (IsTouchingWall)
        {
            lastWallExitTime = Time.time;
            isWallKickBackstepping = false;
        }
    }


    public void PerformSuperJump()
    {
        float currentSpeedX = RB.linearVelocity.x;
        Vector2 superJumpVelocity;
        superJumpVelocity.x = currentSpeedX * playerData.superDashSpeedMult;
        superJumpVelocity.y = playerData.jumpVelocity;
        RB.linearVelocity = superJumpVelocity;
        IsJumping = true;
        Anim.SetBool("Jump", true);
    }

    public void UpdateFlashColor()
    {
        if (CurrentStamina < playerData.tiredThreshold)
        {
            SR.color = (Time.time % 0.2f < 0.1f) ? Color.red : Color.white;
        }
        else
        {
            SR.color = Color.white;
        }
    }

    public void CheckCornerCorrection(float yVelocity)
    {
        if (yVelocity <= 0) return;
        float edgeOffset = 0.04f;
        Bounds bounds = Col.bounds;
        float yOrigin = bounds.max.y + 0.01f;
        Vector2 topLeft = new Vector2(bounds.min.x + edgeOffset, yOrigin);
        Vector2 topRight = new Vector2(bounds.max.x - edgeOffset, yOrigin);
        Vector2 topCenter = new Vector2(bounds.center.x, yOrigin);
        float dist = playerData.cornerCorrectionDistance;

        bool hitLeft = Physics2D.Raycast(topLeft, Vector2.up, dist, whatIsGround);
        bool hitRight = Physics2D.Raycast(topRight, Vector2.up, dist, whatIsGround);
        bool hitCenter = Physics2D.Raycast(topCenter, Vector2.up, dist, whatIsGround);

        if (hitLeft && !hitCenter)
            transform.position += new Vector3(playerData.cornerCorrectionNudgeAmount, 0, 0);
        else if (hitRight && !hitCenter)
            transform.position -= new Vector3(playerData.cornerCorrectionNudgeAmount, 0, 0);
    }

    public void CheckLandingCorrection(float yVelocity)
    {
        if (yVelocity >= 0) return;
        Bounds bounds = Col.bounds;
        float edgeOffset = 0.04f;
        float yOrigin = bounds.min.y + 0.05f;
        Vector2 bottomLeft = new Vector2(bounds.min.x + edgeOffset, yOrigin);
        Vector2 bottomRight = new Vector2(bounds.max.x - edgeOffset, yOrigin);
        Vector2 bottomCenter = new Vector2(bounds.center.x, yOrigin);
        float dist = 0.1f;

        bool hitLeft = Physics2D.Raycast(bottomLeft, Vector2.down, dist, whatIsGround);
        bool hitRight = Physics2D.Raycast(bottomRight, Vector2.down, dist, whatIsGround);
        bool hitCenter = Physics2D.Raycast(bottomCenter, Vector2.down, dist, whatIsGround);

        if (hitLeft && !hitCenter)
            transform.position -= new Vector3(playerData.cornerCorrectionNudgeAmount, 0, 0);
        else if (hitRight && !hitCenter)
            transform.position += new Vector3(playerData.cornerCorrectionNudgeAmount, 0, 0);
    }

    public void FinishDash()
    {
        LastDashEndTime = Time.time;
    }

    public void LoadBindingOverrides()
    {
        string rebinds = PlayerPrefs.GetString("rebinds", string.Empty);

        if (!string.IsNullOrEmpty(rebinds))
        {
            InputHandler.LoadBindingOverridesFromJson(rebinds);
        }
        else
        {
            foreach (var action in InputHandler)
            {
                action.RemoveAllBindingOverrides();
            }
        }
    }

    public void RefillStats()
    {
        if (DashState is PlayerDashState dashState)
        {
            dashState.ResetCanDash();
        }
        CurrentStamina = playerData.maxStamina;

        var hair = GetComponentInChildren<HairController>();
        if (hair != null)
        {
            hair.OnRefill();
        }
    }

    public void Bounce(Vector2 force, float lockTime = 0.1f)
    {
        StateMachine.ChangeState(InAirState);
        if (Mathf.Abs(force.y) < 0.5f) force.y = 2f;
        RB.linearVelocity = force;
        RefillStats();
        ResetStamina();
        inputLockTimer = lockTime;
        JumpInputStartTime = -100f;
        DashInputStartTime = -100f;
        LastOnGroundTime = -100f;
        JumpInput = false;
        CurrentInput = Vector2.zero;
    }

    public void OverrideVelocity(Vector2 newVelocity, float lockInputDuration)
    {
        RB.linearVelocity = newVelocity;

        if (StateMachine.CurrentState != InAirState)
        {
            StateMachine.ChangeState(InAirState);
        }

        if (lockInputDuration > 0)
        {
            inputLockTimer = lockInputDuration;
            CurrentInput = Vector2.zero;
        }

        VarJumpTimer = 0;
        IsJumping = false;
        IsWallJumping = false;
        JumpInputStartTime = -100f;
        DashInputStartTime = -100f;
        LastOnGroundTime = -100f;
    }

    public Vector3 GetGroundPosition()
    {
        RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, 1.5f, whatIsGround);
        if (hit.collider != null) return hit.point;
        return groundCheck.position;
    }

    public void Die(Vector3 hitDir, DeathStrategy strategy = null)
    {
        if (IsDead) return;
        IsDead = true;

        if (Col != null) Col.enabled = false;

        OnPlayerDied?.Invoke(this, hitDir, strategy);
    }

    public void StartShake(Vector3 dir)
    {
        FXManager.Instance.ShakeDirectional(dir, 1.5f);
    }

    public void ReviveInternal()
    {
        IsDead = false;
        RB.simulated = true;
        RB.linearVelocity = Vector2.zero;
        RB.gravityScale = playerData.gravityScale;
        RB.linearDamping = 0f;
        Col.enabled = true;
        if (Anim != null) { Anim.Rebind(); Anim.Update(0f); }
        StateMachine.Initialize(IdleState);
        ResetStamina();
        RefillStats();
    }

    public void SetVisualState(bool isActive)
    {
        if (SR != null) SR.enabled = isActive;

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in allRenderers)
        {
            if (r.gameObject != gameObject)
            {
                r.enabled = isActive;
            }
        }
    }

    public void ConsumeCoyoteTime()
    {
        LastOnGroundTime = -100f;
    }

    public void StartRoomTransition(float xDirection)
    {

        IsStartingTransition = true;

        stateBeforeTransition = StateMachine.CurrentState;
        velocityBeforeTransition = RB.linearVelocity;
        dashAttackTimerBefore = DashAttackTimer;
        varJumpTimerBefore = VarJumpTimer;

        stateElapsedBefore = Time.time - stateBeforeTransition.GetStartTime();

        StateMachine.ChangeState(CutsceneState);

        IsStartingTransition = false;

        Anim.speed = 0;
        RB.simulated = false;

        if (xDirection != 0) SR.flipX = xDirection < 0;
    }

    public void EndRoomTransition()
    {
        RB.simulated = true;
        Anim.speed = 1;

        IsResumingState = true;
        stateBeforeTransition.SetStartTime(Time.time - stateElapsedBefore);
        RB.linearVelocity = velocityBeforeTransition;
        DashAttackTimer = dashAttackTimerBefore;
        VarJumpTimer = varJumpTimerBefore;

        UseJumpInput();
        UseDashInput();
        CurrentInput = InputHandler.Gameplay.Move.ReadValue<Vector2>();

        StateMachine.ChangeState(stateBeforeTransition);

        IsResumingState = false;
    }


    public Vector2 GetCurrentPlatformVelocity()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.5f, whatIsGround);

        if (hit.collider != null)
        {
            if (hit.collider.TryGetComponent(out IVelocityProvider provider))
            {
                return provider.GetVelocityAtPoint(transform.position);
            }
        }

        return Vector2.zero;
    }

    public void SetInputLock(float duration)
    {
        inputLockTimer = duration;
    }

    public void PlayWallLoop(bool play)
    {
        if (play)
        {
            if (!wallLoopSource.isPlaying && wallSlideSound != null)
            {
                wallLoopSource.clip = wallSlideSound.clip;
                wallLoopSource.volume = wallSlideSound.volume;
                wallLoopSource.pitch = wallSlideSound.minPitch;
                wallLoopSource.loop = true;
                wallLoopSource.Play();
            }
        }
        else
        {
            wallLoopSource.Stop();
        }
    }

    public void ManualMove(Vector2 delta)
    {
        RB.position += delta;

        _lastPlatformFrame = Time.frameCount;

        if (RB.linearVelocity.y <= 0)
        {
            float gravityCompensation = -Physics2D.gravity.y * RB.gravityScale * Time.fixedDeltaTime;
            RB.linearVelocity = new Vector2(RB.linearVelocity.x, gravityCompensation);

            if (StateMachine.CurrentState == InAirState || StateMachine.CurrentState == JumpState)
            {
                LastOnGroundTime = Time.time;
                IsJumping = false;

                if (Mathf.Abs(CurrentInput.x) > 0.01f)
                {
                    StateMachine.ChangeState(RunState);
                }
                else
                {
                    StateMachine.ChangeState(IdleState);
                }
            }
        }
    }

    public bool CheckWallForCrush(Vector2 pushDirection, float dist)
    {
        LayerMask crushLayers = whatIsGround | whatIsWall;

        Vector2 size = Col.bounds.size;
        size.x -= 0.02f;
        size.y -= 0.02f;

        RaycastHit2D hit = Physics2D.BoxCast(
            RB.position + Col.offset,
            size,
            0f,
            pushDirection,
            dist + 0.01f,
            crushLayers
        );

        return hit.collider != null;
    }

    public void DieByCrush(DeathStrategy strategy)
    {
        Die(Vector3.zero, strategy);

        Debug.Log($"[Physics] Player Crushed by Platform using strategy: {(strategy != null ? strategy.name : "None")}");
    }
}