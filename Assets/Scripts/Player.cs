using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Player : Entity
{
    [Header("Player Move")]
    public float moveSpeed = 2f;
    public float jumpForce = 5f;
    [Header("Player UI")]
    [SerializeField] public TextMeshProUGUI interactionUGUI;
    [SerializeField] private KeyCode questPanelKey = KeyCode.J;
    [Header("Check Info")]

    [Header("Attack Info")]
    public float CounterDuration;

    public DialogueObj dialogueObj;
    public DialogueLogicBase dialogueLogic;
    public NPC currentNPC;
    public InteractableObject currentInteractable;
    /// <summary>若在阴阳门触发区内，优先用 F 传场景，不进入对话状态。</summary>
    public YinYangGate activeYinYangGate;
    public float XInput;
    public int playerFaceRight { get; private set; } = -1;
    public StateMachine<PlayerState> stateMachine { get; private set; }
    public PlayerIdleState idleState { get; private set; }
    public PlayerMoveState moveState { get; private set; }
    public PlayerChatState chatState { get; private set; }
    public PlayerJumpState jumpState { get; private set; }
    public PlayerAirState airState { get; private set; }
    public PlayerDashState dashState { get; private set; }
    public PlayerAttackCounterState attackCounter { get; private set; }
    public PlayerLightState lightState { get; private set; }
    
    [Header("Light Skill Settings")]
    [Tooltip("按键冷却（秒），可给 UI 读")]
    public float lightSkillCD = 5f;
    [Tooltip("光效与 Light 状态持续时间（秒），与粒子播放时长一致")]
    public float lightSkillEffectDuration = 3f;
    [Tooltip("剩余冷却，运行时递减，可给 UI 读")]
    public float lightSkillTimer;
    public bool canUseLightSkill => lightSkillTimer <= 0;

    [Header("Light · WeakSoul 安抚")]
    [Tooltip("与场景里 WeakSoul 使用的 Unity Tag 一致（需在 Editor 里创建该 Tag）")]
    public string weakSoulTag = "WeakSoul";
    [Tooltip("玩家与此 Tag 物体距离小于该值则判定安抚成功")]
    public float lightSootheRange = 2.5f;
    [Tooltip("Resources/Dialogue 下文件名，不要带 .json")]
    public string lightSootheDialogueFile = "WeakSoulLightSoothe";

    /// <summary>光芒安抚对话结束后在 PlayerChatState 里调用 CompleteLightSoothe</summary>
    public WeakSoul pendingLightSootheSoul;
    /// <summary>由 PlayerLightState 设置，聊天状态里 StartDialogue 后清空</summary>
    public string pendingLightSootheDialogueFile;
    
    public override void Awake()
    {
        base.Awake();
        stateMachine = new StateMachine<PlayerState>();
        idleState = new PlayerIdleState(this, stateMachine, "Idle");
        moveState = new PlayerMoveState(this, stateMachine, "Walk");
        chatState = new PlayerChatState(this, stateMachine, "Idle");
        jumpState = new PlayerJumpState(this, stateMachine, "Jump");
        dashState = new PlayerDashState(this, stateMachine, "Dash");
        lightState = new PlayerLightState(this, stateMachine, "Light");
        lightSkillTimer = 0f;
    }
    public override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);

        if (interactionUGUI != null && UIManager.instance != null)
        {
            UIManager.instance.RegisterPanel(UIType.InteractionTip, interactionUGUI.gameObject, false, true);
        }
    }
    public override void Update()
    {
        base.Update();
        stateMachine.currentState.Update();
        
        if (lightSkillTimer > 0)
        {
            lightSkillTimer -= Time.deltaTime;
        }
        
        bool inDialogue = DialogueSystem.instance != null && DialogueSystem.instance.IsInDialogue();

        if (activeYinYangGate != null && !inDialogue && stateMachine.currentState != chatState)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                activeYinYangGate.StartTransition();
                return;
            }
        }

        bool interactionVisible = UIManager.instance != null 
            ? UIManager.instance.IsVisible(UIType.InteractionTip) 
            : (interactionUGUI != null && interactionUGUI.gameObject.activeSelf);
            
        if (interactionVisible && stateMachine.currentState != chatState)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                stateMachine.ChangeState(chatState);
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Q) && canUseLightSkill && stateMachine.currentState != lightState)
        {
            stateMachine.ChangeState(lightState);
        }

        if (Input.GetKeyDown(questPanelKey) && !inDialogue && !UIManager.IsUIBlocked(UIType.QuestPanel))
        {
            if (QuestPanelUI.instance != null)
                QuestPanelUI.instance.TogglePanel();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && QuestPanelUI.instance != null && QuestPanelUI.instance.IsOpen)
            QuestPanelUI.instance.ClosePanel();
    }
    
    public void StartLightSkillCD()
    {
        lightSkillTimer = lightSkillCD;
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

    public void SetVelocityZero()=>rb.linearVelocity=new Vector2(0, 0);
  

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
        return IsGroundDetected();
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
            interactionUGUI.text = "Press F to interact";
            
            if (UIManager.instance != null)
            {
                if (_show)
                    UIManager.instance.Show(UIType.InteractionTip);
                else
                    UIManager.instance.Hide(UIType.InteractionTip);
            }
            else
            {
                interactionUGUI.gameObject.SetActive(_show);
            }
        }
        else
        {
            Debug.LogError("InteractionUGUI is null");
        }
    }

    /// <summary>与孟婆/NPC 同款：用 UIManager 管理 InteractionTip，并指定世界坐标锚点。</summary>
    public void SetWorldInteractionTip(bool visible, string message, Vector3 worldAnchor)
    {
        if (interactionUGUI == null)
            return;

        if (visible)
        {
            interactionUGUI.text = message;
            if (Camera.main != null)
                interactionUGUI.rectTransform.position = Camera.main.WorldToScreenPoint(worldAnchor);
        }

        if (UIManager.instance != null)
        {
            if (visible)
                UIManager.instance.Show(UIType.InteractionTip);
            else
                UIManager.instance.Hide(UIType.InteractionTip);
        }
        else
            interactionUGUI.gameObject.SetActive(visible);
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("entered the trigger");
        if (other.CompareTag("CanInteractWith"))
        {
            dialogueObj = other.GetComponent<DialogueObj>();
            dialogueLogic = other.GetComponent<DialogueLogicBase>();
            currentNPC = other.GetComponent<NPC>();
            currentInteractable = other.GetComponent<InteractableObject>();
            ShowInteractionUGUI(true);

        }
    }
    public void JumpControl()
    {
        if (JumpInput() && isGrounded())
        {
            stateMachine.ChangeState(jumpState);
        }
    }
    public void OnTriggerStay2D(Collider2D other)
    {
        bool interactionVisible = UIManager.instance != null 
            ? UIManager.instance.IsVisible(UIType.InteractionTip) 
            : (interactionUGUI != null && interactionUGUI.gameObject.activeSelf);
            
        if (other.CompareTag("CanInteractWith") && !interactionVisible)
        {
            ShowInteractionUGUI(true);
            interactionUGUI.transform.position = other.GetComponent<Transform>().position
            + new Vector3(0, 0.5f, 0);
        }
    }
    public void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log("exited the trigger");
        if (other.CompareTag("CanInteractWith"))
        {
            ShowInteractionUGUI(false);
            dialogueObj = null;
            dialogueLogic = null;
            currentNPC = null;
            currentInteractable = null;
        }
    }

}
