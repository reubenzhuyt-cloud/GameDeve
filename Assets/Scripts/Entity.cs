using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Entity : MonoBehaviour
{
    public Animator animator { get; private set; }
    public Rigidbody2D rb { get; private set; }

    [Header("Collision Info")]
    [SerializeField] protected Transform GroundCheck;
    [SerializeField] protected float GroundCheckDistance;
    [SerializeField] protected LayerMask whatIsGround;

    [Header("Attack Info")]
    [SerializeField] public Transform attackCheck;
    [SerializeField] public float attackCheckRadius;





    public virtual void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public virtual void Start()
    {

    }

    // Update is called once per frame
    public virtual void Update()
    {

    }

    public virtual bool IsGroundDetected() => Physics2D.Raycast(GroundCheck.position, Vector2.down, GroundCheckDistance, whatIsGround);

    protected virtual void OnDrawGizmos()
    {
        if (GroundCheck != null)
        {
            Gizmos.DrawLine(GroundCheck.position, new Vector3(GroundCheck.position.x, GroundCheck.position.y - GroundCheckDistance));
        }
    }

   

}
