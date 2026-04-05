using UnityEngine;
using System.Collections.Generic;

public abstract class DialogueLogicBase : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] protected string objectId;
    [SerializeField] protected DialogueObj dialogueObj;
    
    protected int currentState = 0;
    protected int lastChoiceIndex = -1;
    
    public string ObjectId => objectId;
    public int CurrentState => currentState;
    
    protected virtual void Awake()
    {
        if (dialogueObj == null)
        {
            dialogueObj = GetComponent<DialogueObj>();
        }
        if (string.IsNullOrEmpty(objectId))
        {
            objectId = gameObject.name;
        }
    }
    
    protected virtual void Start()
    {
        LoadState();
    }
    
    protected virtual void LoadState()
    {
        currentState = DialogueStateTracker.Instance.GetState(objectId);
        lastChoiceIndex = DialogueStateTracker.Instance.GetLastChoice(objectId);
    }
    
    protected virtual void SaveState()
    {
        DialogueStateTracker.Instance.SetState(objectId, currentState);
        DialogueStateTracker.Instance.SetLastChoice(objectId, lastChoiceIndex);
    }
    
    public virtual string GetDialogueFileName()
    {
        return GetDialogueForState(currentState);
    }
    
    protected abstract string GetDialogueForState(int state);
    
    public virtual void OnDialogueStart()
    {
    }
    
    public virtual void OnDialogueEnd()
    {
        SaveState();
    }
    
    public virtual void OnChoiceSelected(int choiceIndex, string choiceText)
    {
        lastChoiceIndex = choiceIndex;
        ProcessChoice(choiceIndex, choiceText);
        SaveState();
    }
    
    protected abstract void ProcessChoice(int choiceIndex, string choiceText);
    
    public virtual void AdvanceState(int newState)
    {
        currentState = newState;
        SaveState();
    }
    
    public virtual void ResetState()
    {
        currentState = 0;
        lastChoiceIndex = -1;
        DialogueStateTracker.Instance.ResetObjectState(objectId);
    }
    
    public bool HasInteractedBefore()
    {
        return currentState > 0 || lastChoiceIndex >= 0;
    }
}
