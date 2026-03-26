using UnityEngine;

public class EntityState
{
    protected Entity entity;
    protected string animationBoolName;

    protected float stateTime;

    public EntityState(Entity _entity, string _animBoolName)
    {
        this.entity = _entity;
        this.animationBoolName = _animBoolName;
    }

    public virtual void Enter()
    {
        stateTime = 0f;
        // entity.animator.SetBool(animationBoolName, true);
        Debug.Log(entity.name + "Enter " + animationBoolName);
    }

    public virtual void Update()
    {
        stateTime += Time.deltaTime;
    }

    public virtual void Exit()
    {
        // entity.animator.SetBool(animationBoolName, false);
        Debug.Log(entity.name + " Exit " + animationBoolName);
    }
}
