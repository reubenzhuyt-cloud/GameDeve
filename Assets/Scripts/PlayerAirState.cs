using UnityEngine;

public class PlayerAirState : PlayerState
{
    public PlayerAirState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName) : base(player, stateMachine, animationBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
    }
}
