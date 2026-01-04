using UnityEngine;

public class PlayerCutsceneState : PlayerState
{
    public PlayerCutsceneState(PlayerController player, PlayerStateMachine stateMachine, PlayerData playerData, string animBoolName)
        : base(player, stateMachine, playerData, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void LogicUpdate()
    {
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
    }
}