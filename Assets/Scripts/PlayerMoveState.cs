using UnityEngine;

public class PlayerMoveState : PlayerGroundedState
{
    public PlayerMoveState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName) : base(player, stateMachine, animationBoolName)
    {

    }
    public override void Enter()
    {
        base.Enter();
    }
    public override void Update()
    {
        base.Update();

        if (player.XInput != 0)
        {
            player.SetVelocity(player.XInput * player.moveSpeed);
        }
        if (player.XInput == 0)
        {
            stateMachine.ChangeState(player.idleState);
        }
        player.FlipCheck();
    }
    public override void Exit()
    {
        base.Exit();
    }
}
