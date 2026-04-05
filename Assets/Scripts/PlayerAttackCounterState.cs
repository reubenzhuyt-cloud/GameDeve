using UnityEngine;

public class PlayerAttackCounterState : PlayerState
{

    public PlayerAttackCounterState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName) : base(player, stateMachine, animationBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        stateTime = player.CounterDuration;
        //player.anim.SetBool("SuccessfulCounter", false);

    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        player.SetVelocityZero();

        Collider2D[] colliders = Physics2D.OverlapCircleAll(player.attackCheck.position, player.attackCheckRadius);

        foreach (var hit in colliders)
        {
            if (hit.GetComponent<Enemy>() != null)
            {
                if (hit.GetComponent<Enemy>().CanBeStunned())
                {
                    stateTime = 10;
                    //player.anim.SetBool("SuccessfulCounter", true);
                }
            }
        }
        if (stateTime < 0)// triggerCalled
            stateMachine.ChangeState(player.idleState);
    }

}

