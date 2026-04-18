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

    /// <summary>
    /// 与渔夫 NPC 同款：补 <see cref="DialogueObj"/> 与 Resources 路径、无碰撞体时加 Trigger + Kinematic Rigidbody2D，
    /// 便于 Player 触发器与 F 提示锚点。
    /// 请在 <see cref="Awake"/> 里、在 <c>base.Awake()</c> 之前调用（并先设好 <see cref="objectId"/>）。
    /// </summary>
    protected void EnsureLineNpcInteractSupport(string dialogueResourcePath)
    {
        if (string.IsNullOrEmpty(dialogueResourcePath))
            return;

        if (dialogueObj == null)
            dialogueObj = GetComponent<DialogueObj>();
        if (dialogueObj == null)
            dialogueObj = gameObject.AddComponent<DialogueObj>();
        dialogueObj.EnsureSingleDialogue(dialogueResourcePath);

        if (GetComponent<Collider2D>() != null)
            return;

        var box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(3f, 4f);

        if (GetComponent<Rigidbody2D>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }
    }

    /// <summary>若未使用 <see cref="InteractableObject"/>，提示 Tag/Trigger 是否与孟婆、渔夫一致。</summary>
    protected void WarnLineNpcInteractSetupIfIncomplete(string ownerScriptName)
    {
        bool hasProximity = GetComponent<InteractableObject>() != null
            || GetComponentInChildren<InteractableObject>(true) != null;
        if (hasProximity)
            return;

        if (!CompareTag("CanInteractWith"))
        {
            Debug.LogWarning(
                $"[{ownerScriptName}] {gameObject.name}: 未检测到 InteractableObject。请把 Tag 设为 CanInteractWith（与孟婆/渔夫相同），否则可能无按 F 提示。",
                this);
        }

        var col = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>(true);
        if (col == null)
            Debug.LogWarning($"[{ownerScriptName}] {gameObject.name}: 缺少 Collider2D，无法触发交互。", this);
        else if (!col.isTrigger)
            Debug.LogWarning($"[{ownerScriptName}] {gameObject.name}: Collider2D 应勾选 Is Trigger。", this);
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
