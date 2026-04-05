using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class ScheduleEntry
{
    public string timePhase;
    public Vector3 position;
    public string animationState;
    public bool isAvailable = true;
}

public class NPC : Entity
{
    [Header("NPC Info")]
    public string npcName;
    public string npcId;
    
    [Header("Dialogue")]
    public DialogueObj dialogueObj;
    public DialogueLogicBase dialogueLogic;
    public float interactionRange = 2f;
    public LayerMask playerLayer;
    
    [Header("Schedule")]
    public List<ScheduleEntry> schedule = new List<ScheduleEntry>();
    protected int currentScheduleIndex = 0;
    
    [Header("Relationship")]
    [Range(-100, 100)]
    public int relationshipValue = 0;
    public int RelationshipValue => relationshipValue;
    
    [Header("State")]
    public bool isInteractable = true;
    public bool isTalking = false;
    
    protected bool playerInRange = false;
    protected Transform playerTransform;
    
    public UnityEvent onPlayerEnterRange;
    public UnityEvent onPlayerExitRange;
    public UnityEvent<int> onRelationshipChange;
    public UnityEvent onInteractionStart;
    public UnityEvent onInteractionEnd;
    
    public override void Awake()
    {
        base.Awake();
        if (dialogueObj == null)
        {
            dialogueObj = GetComponent<DialogueObj>();
        }
        if (dialogueLogic == null)
        {
            dialogueLogic = GetComponent<DialogueLogicBase>();
        }
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
    }
    
    public override void Start()
    {
        base.Start();
        UpdateSchedule();
    }
    
    public override void Update()
    {
        base.Update();
        CheckPlayerInRange();
        UpdateSchedule();
    }
    
    protected void CheckPlayerInRange()
    {
        if (!isInteractable) return;
        
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRange, playerLayer);
        bool wasInRange = playerInRange;
        playerInRange = hits.Length > 0;
        
        if (playerInRange && !wasInRange)
        {
            onPlayerEnterRange?.Invoke();
        }
        else if (!playerInRange && wasInRange)
        {
            onPlayerExitRange?.Invoke();
        }
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
        onInteractionStart?.Invoke();
        
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
        onInteractionEnd?.Invoke();
        
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
    
    public virtual void ChangeRelationship(int amount)
    {
        int oldValue = relationshipValue;
        relationshipValue = Mathf.Clamp(relationshipValue + amount, -100, 100);
        
        if (oldValue != relationshipValue)
        {
            onRelationshipChange?.Invoke(relationshipValue);
        }
    }
    
    public virtual void UpdateSchedule()
    {
        if (TimeManager.instance == null || schedule.Count == 0) return;
        
        string currentTimePhase = TimeManager.instance.CurrentTimePhase.ToString();
        
        for (int i = 0; i < schedule.Count; i++)
        {
            if (schedule[i].timePhase == currentTimePhase && i != currentScheduleIndex)
            {
                currentScheduleIndex = i;
                ApplyScheduleEntry(schedule[i]);
                break;
            }
        }
    }
    
    protected virtual void ApplyScheduleEntry(ScheduleEntry entry)
    {
        if (!entry.isAvailable) return;
        
        transform.position = entry.position;
        
        if (animator != null && !string.IsNullOrEmpty(entry.animationState))
        {
            animator.Play(entry.animationState);
        }
    }
    
    public virtual void SetInteractable(bool value)
    {
        isInteractable = value;
    }
    
    public bool IsPlayerInRange()
    {
        return playerInRange;
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
