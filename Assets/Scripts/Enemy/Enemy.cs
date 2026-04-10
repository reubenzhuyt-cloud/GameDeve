using System.Collections;
using UnityEngine;

public class Enemy : Entity
{
    [Header("Knockback Info")]
    [SerializeField] protected Vector2 knockbackDirection;
    [SerializeField] protected float knockbackDuration;
    protected bool isKnocked;

    [Header("Stunned Info")]
    public float stunDuration;
    public Vector2 stunDirection;
    protected bool canBeStunned;
    [SerializeField] protected GameObject counterImage;

    public int facingDir = -1;
    protected bool facingRight = true;

    public StateMachine<PlayerState> stateMachine { get; private set; }

    public override void Awake()
    {
        base.Awake();
    }

    public override void Start()
    {
        base.Start();
        //stateMachine.Initialize(idleState);
    }

    public override void Update()
    {
        base.Update();
        stateMachine.currentState.Update();
    }

    public virtual bool CanBeStunned()
    {
        if (canBeStunned)
        {
            CloseCounterAttackWindow();
            return true;
        }
        return false;
    }

    public virtual void OpenCounterAttackWindow()
    {
        canBeStunned = true;
        counterImage.SetActive(true);
    }

    public virtual void CloseCounterAttackWindow()
    {
        canBeStunned = false;
        counterImage.SetActive(false);
    }

    public virtual IEnumerator HitKnockback()//不知道怎么改
    {
        isKnocked = true;

        rb.linearVelocity = new Vector2(knockbackDirection.x * -facingDir, knockbackDirection.y);

        yield return new WaitForSeconds(knockbackDuration);

        isKnocked = false;
    }

}

