using UnityEngine;

public class EnemyState : EntityState
{
    protected Enemy enemy;

    public StateMachine<EnemyState> stateMachine { get; private set; }
    private string animBoolName;

    public EnemyState(Enemy _enemy, StateMachine<EnemyState> _stateMachine, string _animBoolName) : base(_enemy, _animBoolName)
    {
        this.enemy = _enemy;
        this.stateMachine = _stateMachine;
    }

    public override void Enter()
    {

    }

    public override void Update()
    {

    }

    public override void Exit()
    {

    }

}
