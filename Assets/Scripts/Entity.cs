using UnityEngine;

public class Entity : MonoBehaviour
{
    public Animator animator { get; private set; }
    public Rigidbody2D rb { get; private set; }
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
}
