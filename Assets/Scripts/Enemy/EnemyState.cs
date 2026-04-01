using UnityEngine;

public class EnemyState : EntityState
{
    protected Enemy enemy;

    public StateMachine<EnemyState> stateMachine { get; private set; }
    private string animBoolName;

    public EnemyState(Enemy _enemy, StateMachine _stateMachine, string _animBoolName)
    {
        this.enemy = _enemy;
        this.animBoolName = _animBoolName;
        this.stateMachine = _stateMachine;
    }

    public virtual void Enter()
    {

    }

    public virtual void Update()
    {

    }

    public virtual void Exit()
    {

    }

}
