using UnityEngine;

public class PlayerJumpState : PlayerAirState
{
    public PlayerJumpState(Player player, StateMachine<PlayerState> stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    override public void Enter()
    {
        base.Enter();
        player.SetVelocity(player.rb.linearVelocityX, player.jumpForce);
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
