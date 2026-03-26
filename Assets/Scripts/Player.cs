using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Player : Entity
{
    [Header("Player Move")]
    public float moveSpeed = 2f;

    [Header("Player UI")]
    [SerializeField] public TextMeshProUGUI interactionUGUI;
    [Header("Check Info")]
    public Transform GroundCheck;
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.5f;

    public DialogueObj dialogueObj;
    public float XInput;
    public int playerFaceRight { get; private set; } = -1;
    public StateMachine<PlayerState> stateMachine { get; private set; }
    public PlayerIdleState idleState { get; private set; }
    public PlayerMoveState moveState { get; private set; }
    public PlayerChatState chatState { get; private set; }
    public PlayerJumpState jumpState { get; private set; }
    public PlayerAirState airState { get; private set; }
    public PlayerDashState dashState { get; private set; }
    public override void Awake()
    {
        base.Awake();
        stateMachine = new StateMachine<PlayerState>();
        idleState = new PlayerIdleState(this, stateMachine, "Idle");
        moveState = new PlayerMoveState(this, stateMachine, "Walk");
        chatState = new PlayerChatState(this, stateMachine, "Idle");
    }
    public override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);
    }
    public override void Update()
    {
        base.Update();
        stateMachine.currentState.Update();
        if (interactionUGUI.gameObject.activeSelf && stateMachine.currentState != chatState)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                stateMachine.ChangeState(chatState);
            }
        }
    }
    public void FlipCheck()
    {
        if (XInput != 0 && XInput != playerFaceRight)
        {
            Flip();
        }
    }
    public void Flip()
    {
        playerFaceRight = playerFaceRight == -1 ? 1 : -1;
        transform.Rotate(0, 180, 0);
    }
    public void SetVelocity(float velocityX)
    {
        rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
    }

    public void SetVelocity(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
    }

    public void SetVelocity(Vector3 velocity)
    {
        rb.linearVelocity = new Vector2(velocity.x, velocity.y);
    }

    public void SetVelocity(float velocityX, float velocityY)
    {
        rb.linearVelocity = new Vector2(velocityX, velocityY);
    }
    public bool JumpInput()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Joystick1Button0);
    }
    public bool isGrounded()
    {
        return Physics2D.Raycast(GroundCheck.position, Vector2.down, groundCheckDistance, groundLayer);
    }
    public void ShowInteractionUGUI(bool _show)
    {
        if (dialogueObj != null)
        {
            Vector3 worldPos = dialogueObj.transform.position + new Vector3(2f, 0.3f, 0);
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            interactionUGUI.rectTransform.position = screenPos;
        }
        if (interactionUGUI != null)
        {
            interactionUGUI.gameObject.SetActive(_show);
            interactionUGUI.text = "Press F to interact";
        }
        else
        {
            Debug.LogError("InteractionUGUI is null");
        }
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("entered the trigger");
        if (other.CompareTag("CanInteractWith"))
        {
            dialogueObj = other.GetComponent<DialogueObj>();
            ShowInteractionUGUI(true);

        }
    }
    // public void OnTriggerStay2D(Collider2D other)
    // {
    //     if (other.CompareTag("CanInteractWith") && !interactionUGUI.gameObject.activeSelf)
    //     {
    //         ShowInteractionUGUI(true);
    //         interactionUGUI.transform.position = other.GetComponent<Transform>().position
    //         + new Vector3(0, 0.5f, 0);
    //     }
    // }
    public void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log("exited the trigger");
        if (other.CompareTag("CanInteractWith"))
        {
            ShowInteractionUGUI(false);
            dialogueObj = null;
        }
    }
    public void OnDrawGizmos()
    {
        Gizmos.DrawLine(GroundCheck.position, GroundCheck.position + Vector3.down * 0.5f);
    }
}
