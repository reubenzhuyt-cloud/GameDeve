using UnityEngine;

public class PlayerIdleState : PlayerState
{


    public PlayerIdleState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName) : base(player, stateMachine, animationBoolName)
    {

    }
    public override void Enter()
    {
        base.Enter();
        player.SetVelocity(0);
    }
    public override void Update()
    {
        base.Update();
        if (player.JumpInput())
        {
            stateMachine.ChangeState(player.jumpState);
        }
        if (player.XInput != 0)
        {
            stateMachine.ChangeState(player.moveState);
        }
    }
    public override void Exit()
    {
        base.Exit();
    }
}
