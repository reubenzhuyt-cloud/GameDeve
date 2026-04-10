using UnityEngine;

public class PlayerState : EntityState
{
    protected Player player;

    public StateMachine<PlayerState> stateMachine { get; private set; }
    public float stateRuntime;

    public PlayerState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName) : base(player, animationBoolName)
    {
        this.player = player;
        this.animationBoolName = animationBoolName;
        this.stateMachine = stateMachine;
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
        player.XInput = Input.GetAxisRaw("Horizontal");
        stateRuntime -= Time.deltaTime;
    }
}
