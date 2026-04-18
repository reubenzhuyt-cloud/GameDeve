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
    /// <summary>由 <see cref="InteractableObject"/> 距离检测注册的交互源（与 Trigger「CanInteractWith」并列，二者择一显示）。</summary>
    private InteractableObject _proximityInteractable;
    /// <summary>当前进入的 CanInteractWith 触发器，用于对话结束后恢复 F 提示（仍在范围内时）。</summary>
    private Collider2D _activeInteractTrigger;
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
    
    [Header("Light Skill（光效 Prefab 挂在场景 EffectManager，勿挂 Player）")]
    [Tooltip("按键冷却（秒），可给 UI 读")]
    public float lightSkillCD = 5f;
    [Tooltip("光效持续时间（秒）。预制体在 EffectManager.Particle Effect Prefab（如场景物体 2DEffect），由 PlayerLightState 调 EffectManager.Play 生成并跟随玩家")]
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

        EnsureInteractionTipRegistered();
        if (interactionUGUI != null)
            UIManager.EnsureCanvasRootVisible(interactionUGUI.gameObject);
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
        
        // 对话中禁止 Q：否则会离开 Chat 再进 Light，打断安抚对白且 Dialogue 未关闭
        if (Input.GetKeyDown(KeyCode.Q) && canUseLightSkill
            && stateMachine.currentState != lightState
            && stateMachine.currentState != chatState
            && !inDialogue)
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
    /// <summary>切场景或 <see cref="UIManager.RemoveStalePanelReferences"/> 后须重新注册，否则 <see cref="UIManager.Show"/> 找不到 InteractionTip。</summary>
    /// <remarks>
    /// 不在此做「引用不一致就再 Register」：RegisterPanel(startHidden:true) 会立刻 HideImmediate，若与 Show 不同帧易导致提示永久不显示。
    /// 仅当未注册或字典里引用已销毁时再注册。
    /// </remarks>
    private void EnsureInteractionTipRegistered()
    {
        if (interactionUGUI == null || UIManager.instance == null)
            return;

        if (!UIManager.instance.IsPanelRegistered(UIType.InteractionTip))
        {
            UIManager.instance.RegisterPanel(UIType.InteractionTip, interactionUGUI.gameObject, false, true);
            return;
        }

        var go = UIManager.instance.GetPanel(UIType.InteractionTip);
        if (go == null)
        {
            UIManager.instance.UnregisterPanel(UIType.InteractionTip);
            UIManager.instance.RegisterPanel(UIType.InteractionTip, interactionUGUI.gameObject, false, true);
        }
    }

    public void ShowInteractionUGUI(bool _show)
    {
        EnsureInteractionTipRegistered();
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
                {
                    UIManager.instance.Show(UIType.InteractionTip);
                    interactionUGUI.gameObject.SetActive(true);
                }
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

        EnsureInteractionTipRegistered();

        if (visible)
        {
            interactionUGUI.text = message;
            if (Camera.main != null)
                interactionUGUI.rectTransform.position = Camera.main.WorldToScreenPoint(worldAnchor);
        }

        if (UIManager.instance != null)
        {
            if (visible)
            {
                UIManager.instance.Show(UIType.InteractionTip);
                interactionUGUI.gameObject.SetActive(true);
            }
            else
                UIManager.instance.Hide(UIType.InteractionTip);
        }
        else
            interactionUGUI.gameObject.SetActive(visible);
    }

    /// <summary>
    /// <see cref="InteractableObject"/> 用 OverlapCircle 检测到玩家时调用：与「带 CanInteractWith 的 Trigger」一样，写入对话引用并显示 F 提示。
    /// </summary>
    public void RegisterProximityInteractable(InteractableObject zone, string prompt, Vector3 worldAnchor)
    {
        if (zone == null)
            return;

        if (_proximityInteractable != null && _proximityInteractable != zone)
            UnregisterProximityInteractable(_proximityInteractable);

        _proximityInteractable = zone;
        dialogueLogic = zone.GetComponent<DialogueLogicBase>();
        dialogueObj = zone.GetComponent<DialogueObj>();
        currentNPC = zone.GetComponent<NPC>();
        currentInteractable = zone;

        string msg = string.IsNullOrEmpty(prompt) ? "按 F 对话" : prompt;
        SetWorldInteractionTip(true, msg, worldAnchor);
    }

    public void UnregisterProximityInteractable(InteractableObject zone)
    {
        if (zone == null || _proximityInteractable != zone)
            return;

        _proximityInteractable = null;

        if (ReferenceEquals(currentInteractable, zone))
            currentInteractable = null;

        var dl = zone.GetComponent<DialogueLogicBase>();
        if (ReferenceEquals(dialogueLogic, dl))
            dialogueLogic = null;

        var dObj = zone.GetComponent<DialogueObj>();
        if (ReferenceEquals(dialogueObj, dObj))
            dialogueObj = null;

        var npc = zone.GetComponent<NPC>();
        if (ReferenceEquals(currentNPC, npc))
            currentNPC = null;

        SetWorldInteractionTip(false, "", Vector3.zero);
    }

    /// <summary>
    /// 对话结束时 DialoguePanel 会按规则隐藏 InteractionTip；若人仍站在交互范围内，须手动恢复提示。
    /// </summary>
    public void TryRefreshInteractionTipAfterDialogue()
    {
        EnsureInteractionTipRegistered();

        if (_proximityInteractable != null && _proximityInteractable.IsPlayerInRange())
        {
            _proximityInteractable.ReregisterPlayerTip();
            return;
        }

        if (_activeInteractTrigger != null && _activeInteractTrigger.CompareTag("CanInteractWith"))
            ShowInteractionUGUI(true);
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("entered the trigger");
        if (other.CompareTag("CanInteractWith"))
        {
            _activeInteractTrigger = other;
            dialogueObj = other.GetComponent<DialogueObj>() ?? other.GetComponentInParent<DialogueObj>();
            dialogueLogic = other.GetComponent<DialogueLogicBase>() ?? other.GetComponentInParent<DialogueLogicBase>();
            currentNPC = other.GetComponent<NPC>() ?? other.GetComponentInParent<NPC>();
            currentInteractable = other.GetComponent<InteractableObject>() ?? other.GetComponentInParent<InteractableObject>();
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
            if (_activeInteractTrigger == other)
                _activeInteractTrigger = null;

            ShowInteractionUGUI(false);
            dialogueObj = null;
            dialogueLogic = null;
            currentNPC = null;
            currentInteractable = null;

            // 仍处在 InteractableObject 距离范围内时（例如刚离开别的 NPC 的 Trigger），恢复近距离交互提示
            if (_proximityInteractable != null && _proximityInteractable.IsPlayerInRange())
                _proximityInteractable.ReregisterPlayerTip();
        }
    }

}
