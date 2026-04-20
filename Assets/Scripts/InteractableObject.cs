using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] protected float interactionRange = 2f;
    [Tooltip("未设置时自动使用名为 Player 的层，否则 OverlapCircle 永远检测不到玩家")]
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected bool isInteractable = true;

    [Header("UI（与 Player 上 F 提示一致）")]
    [SerializeField] protected string proximityPrompt = "按 F 对话";
    [SerializeField] protected Vector3 interactionTipOffset = new Vector3(0f, 0.5f, 0f);

    public string ProximityPromptText => proximityPrompt;
    public Vector3 InteractionTipWorldAnchor => transform.position + interactionTipOffset;
    
    [Header("Dialogue")]
    [SerializeField] protected DialogueObj dialogueObj;
    [SerializeField] protected DialogueLogicBase dialogueLogic;
    
    [Header("Visual")]
    [SerializeField] protected GameObject interactionIndicator;
    
    protected bool playerInRange = false;
    protected Transform playerTransform;
    protected bool isTalking = false;
    
    protected virtual void Awake()
    {
        if (dialogueObj == null)
            dialogueObj = GetComponent<DialogueObj>() ?? GetComponentInChildren<DialogueObj>(true);
        if (dialogueLogic == null)
            dialogueLogic = GetComponent<DialogueLogicBase>() ?? GetComponentInChildren<DialogueLogicBase>(true);

        if (playerLayer.value == 0)
        {
            int pl = LayerMask.NameToLayer("Player");
            if (pl >= 0)
                playerLayer = 1 << pl;
        }

        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
        }
    }
    
    protected virtual void Update()
    {
        CheckPlayerInRange();
        UpdateInteractionIndicator();
    }
    
    protected virtual void CheckPlayerInRange()
    {
        if (!isInteractable) return;

        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        bool hasPlayer = TryOverlapPlayer(out _);

        bool wasInRange = playerInRange;
        playerInRange = hasPlayer;
        
        if (playerInRange && !wasInRange)
        {
            OnPlayerEnterRange();
        }
        else if (!playerInRange && wasInRange)
        {
            OnPlayerExitRange();
        }
    }
    
    protected virtual void OnPlayerEnterRange()
    {
        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(true);
        }

        var player = playerTransform != null ? playerTransform.GetComponent<Player>() : null;
        if (player != null)
            player.RegisterProximityInteractable(this, proximityPrompt, transform.position + interactionTipOffset);
    }

    protected virtual void OnPlayerExitRange()
    {
        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
        }

        var player = playerTransform != null ? playerTransform.GetComponent<Player>() : null;
        player?.UnregisterProximityInteractable(this);
    }

    /// <summary>离开别的物体的 Trigger 后若仍在本物体距离内，由 <see cref="Player"/> 调用以恢复 F 提示。</summary>
    public void ReregisterPlayerTip()
    {
        var player = playerTransform != null ? playerTransform.GetComponent<Player>() : null;
        if (player != null && playerInRange)
            player.RegisterProximityInteractable(this, proximityPrompt, transform.position + interactionTipOffset);
    }
    
    protected virtual void UpdateInteractionIndicator()
    {
    }
    
    public virtual void Interact()
    {
        if (!isInteractable) return;
        
        if (dialogueLogic != null && dialogueObj == null)
        {
            dialogueObj = dialogueLogic.GetComponent<DialogueObj>();
        }
        
        if (dialogueObj == null && dialogueLogic == null) return;
        
        isTalking = true;
        
        if (DialogueSystem.instance != null)
        {
            DialogueSystem.instance.onDialogueEnd.AddListener(OnDialogueEnd);
            DialogueSystem.instance.onChoiceSelected.AddListener(OnChoiceSelected);
            
            string dialogueFileName;
            if (dialogueLogic != null)
            {
                dialogueLogic.OnDialogueStart();
                dialogueFileName = dialogueLogic.GetDialogueFileName();
            }
            else
            {
                dialogueFileName = dialogueObj.currentDialogue();
            }
            
            DialogueSystem.instance.StartDialogue(dialogueFileName);
        }
    }
    
    protected virtual void OnChoiceSelected(int choiceIndex, string choiceText)
    {
        if (dialogueLogic != null)
        {
            dialogueLogic.OnChoiceSelected(choiceIndex, choiceText);
        }
    }
    
    protected virtual void OnDialogueEnd(DialogueData dialogue)
    {
        isTalking = false;
        
        if (dialogueLogic != null)
        {
            dialogueLogic.OnDialogueEnd();
        }
        
        if (DialogueSystem.instance != null)
        {
            DialogueSystem.instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
            DialogueSystem.instance.onChoiceSelected.RemoveListener(OnChoiceSelected);
        }
    }
    
    public bool IsPlayerInRange()
    {
        return playerInRange;
    }
    
    public virtual void SetInteractable(bool value)
    {
        isInteractable = value;
        if (!value && interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
        }
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    /// <summary>
    /// 先按 <see cref="playerLayer"/> 检测；部分场景玩家未放在「Player」物理层会导致扫不到，再全层扫并用 <see cref="Player"/> 组件判定。
    /// </summary>
    private bool TryOverlapPlayer(out Collider2D[] hits)
    {
        hits = Physics2D.OverlapCircleAll(transform.position, interactionRange, playerLayer);
        if (hits != null && hits.Length > 0 && AnyColliderHasPlayer(hits))
            return true;

        hits = Physics2D.OverlapCircleAll(transform.position, interactionRange);
        return hits != null && hits.Length > 0 && AnyColliderHasPlayer(hits);
    }

    private static bool AnyColliderHasPlayer(Collider2D[] colliders)
    {
        if (colliders == null)
            return false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c != null && c.GetComponentInParent<Player>() != null)
                return true;
        }

        return false;
    }
}
