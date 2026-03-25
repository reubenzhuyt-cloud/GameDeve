using UnityEngine;

public class PlayerJumpState : PlayerState
{
    public PlayerJumpState(Player player, StateMachine<PlayerState> stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    override public void Enter()
    {
        base.Enter();
    }
    override public void Update()
    {
        base.Update();
    }
    override public void Exit()
    {
        base.Exit();
    }
}
