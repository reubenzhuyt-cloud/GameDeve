using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] protected float interactionRange = 2f;
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected bool isInteractable = true;
    
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
        {
            dialogueObj = GetComponent<DialogueObj>();
        }
        if (dialogueLogic == null)
        {
            dialogueLogic = GetComponent<DialogueLogicBase>();
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
        
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRange, playerLayer);
        bool wasInRange = playerInRange;
        playerInRange = hits.Length > 0;
        
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
    }
    
    protected virtual void OnPlayerExitRange()
    {
        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
        }
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
}
