using UnityEngine;

public class PlayerDashState : PlayerState
{
    public bool CanDash { get; private set; }
    private float lastDashTime;

    public Vector2 DashDirection { get; private set; }

    public PlayerDashState(PlayerController player, PlayerStateMachine stateMachine, PlayerData playerData, string animBoolName) : base(player, stateMachine, playerData, animBoolName)
    {
        CanDash = true;
    }

    public bool CheckIfCanDash()
    {
        return CanDash && Time.time >= lastDashTime + playerData.dashCooldown;
    }

    public void ResetCanDash()
    {
        CanDash = true;
    }

    public override void Enter()
    {
        if (player.IsResumingState)
        {
            player.Anim.SetBool(animBoolName, true);
            return;
        }

        base.Enter();

        AudioManager.Instance.PlaySFX(player.dashSound, player.transform.position);

        CanDash = false;
        lastDashTime = Time.time;

        player.SetGravityScale(0f);

        player.DashAttackTimer = 18f / 60f;

        DashDirection = player.CurrentInput;
        if (DashDirection == Vector2.zero)
        {
            DashDirection = new Vector2(player.FacingDirection, 0);
        }
        DashDirection.Normalize();

        player.RB.linearVelocity = DashDirection * playerData.dashSpeed;

        startTime = Time.time;

        if (player.PlayerAnim != null)
        {
            player.PlayerAnim.PlayDashStart(DashDirection);
        }

        if (player.AfterImage != null)
        {
            player.AfterImage.CheckIfShouldPlaceGhost();
        }
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        player.RB.linearVelocity = DashDirection * playerData.dashSpeed;

        if (player.AfterImage != null)
        {
            player.AfterImage.CheckIfShouldPlaceGhost();
        }

        if (DashDirection.y > 0 && player.DashAttackTimer > 0 && player.CheckJumpInput())
        {
            if (CheckWallBounce(out int wallDir))
            {
                player.UseJumpInput();
                player.PerformWallBounce(wallDir);
                stateMachine.ChangeState(player.InAirState);
                return;
            }
        }

        if (Time.time >= startTime + playerData.dashTime)
        {
            if (isGrounded)
                stateMachine.ChangeState(player.IdleState);
            else
                stateMachine.ChangeState(player.InAirState);
        }

        if (player.CheckIfGrounded() && player.CheckJumpInput())
        {
            player.UseJumpInput();
            player.PerformSuperJump();
            stateMachine.ChangeState(player.InAirState);
            return;
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
        if (player.RB.linearVelocity.y > 0)
        {
            player.CheckCornerCorrection(player.RB.linearVelocity.y);
        }
    }

    public override void Exit()
    {
        base.Exit();

        player.FinishDash();
        player.SetGravityScale(player.DefaultGravity);

        if (player.IsStartingTransition) return;

        if (player.RB.linearVelocity.y > 0)
        {
            player.SetVelocityY(player.RB.linearVelocity.y * playerData.dragY);
        }
        else
        {
            if (player.RB.linearVelocity.y < -15f)
            {
                player.SetVelocityY(-15f);
            }
        }

        if (player.PlayerAnim != null)
        {
            player.PlayerAnim.PlayDashStop();
        }
    }

    private bool CheckWallBounce(out int wallDir)
    {
        wallDir = 0;
        Vector2 origin = player.transform.position;

        float distSafe = 4f / 8f;
        float distSpike = 2f / 8f;

        if (CheckSide(origin, 1, distSafe, distSpike))
        {
            wallDir = 1;
            return true;
        }
        if (CheckSide(origin, -1, distSafe, distSpike))
        {
            wallDir = -1;
            return true;
        }
        return false;
    }

    private bool CheckSide(Vector2 origin, int dir, float safeDist, float spikeDist)
    {
        Vector2 direction = Vector2.right * dir;

        if (Physics2D.Raycast(origin, direction, safeDist + 0.1f, player.whatIsInvisibleBarrier))
        {
            return false;
        }

        bool hasSpikes = Physics2D.Raycast(origin, direction, safeDist, player.whatIsSpikes);
        float checkDist = hasSpikes ? spikeDist : safeDist;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, checkDist, player.whatIsWall);

        return hit.collider != null;
    }
}