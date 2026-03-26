using UnityEngine;

public class PlayerJumpState : PlayerState
{
    public PlayerJumpState(Player player, StateMachine<PlayerState> stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    override public void Enter()
    {
        base.Enter();
        player.rb.AddForce(new Vector2(player.jumpForce, player.jumpForce), ForceMode2D.Impulse);
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
