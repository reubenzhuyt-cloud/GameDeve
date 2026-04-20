using UnityEngine;

/// <summary>
/// 各状态对应 Resources/Dialogue 下独立 JSON；若缺失则在 <see cref="GetDialogueFileName"/> 中回退到初次对白文件名。
/// </summary>
public class WeakSoulDialogueLogic : DialogueLogicBase
{
    public enum WeakSoulState
    {
        Initial = 0,
        TalkedOnce = 1,
        BeingSoothed = 2,
        Soothed = 3
    }
    
    [Header("Weak Soul Dialogue Files")]
    [SerializeField] private string initialDialogue = "WeakSoulDialogue";
    [SerializeField] private string talkedOnceDialogue = "WeakSoulTalkedOnce";
    [SerializeField] private string beingSoothedDialogue = "WeakSoulBeingSoothed";
    [SerializeField] private string soothedDialogue = "WeakSoulSoothed";
    
    [Header("Soul Reference")]
    [SerializeField] private WeakSoul weakSoul;
    
    public WeakSoulState CurrentSoulState => (WeakSoulState)currentState;
    
    protected override void Awake()
    {
        base.Awake();
        if (weakSoul == null)
        {
            weakSoul = GetComponent<WeakSoul>()
                ?? GetComponentInChildren<WeakSoul>(true)
                ?? GetComponentInParent<WeakSoul>();
        }
    }

    public override string GetDialogueFileName()
    {
        string candidate = GetDialogueForState(currentState);
        if (Resources.Load<TextAsset>($"Dialogue/{candidate}") != null)
            return candidate;
        if (candidate != initialDialogue)
        {
            Debug.LogWarning($"[WeakSoulDialogueLogic] 未找到 Resources/Dialogue/{candidate}.json，回退为 {initialDialogue}。", this);
            return initialDialogue;
        }

        return candidate;
    }

    protected override string GetDialogueForState(int state)
    {
        switch ((WeakSoulState)state)
        {
            case WeakSoulState.Initial:
                return initialDialogue;
            case WeakSoulState.TalkedOnce:
                return talkedOnceDialogue;
            case WeakSoulState.BeingSoothed:
                return beingSoothedDialogue;
            case WeakSoulState.Soothed:
                return soothedDialogue;
            default:
                return initialDialogue;
        }
    }
    
    protected override void ProcessChoice(int choiceIndex, string choiceText)
    {
        if (currentState == (int)WeakSoulState.Initial)
        {
            currentState = (int)WeakSoulState.TalkedOnce;
            SaveState();
        }
    }
    
    public override void OnDialogueEnd()
    {
        base.OnDialogueEnd();
        
        if (currentState == (int)WeakSoulState.Initial)
        {
            currentState = (int)WeakSoulState.TalkedOnce;
            SaveState();
        }
    }
    
    public void SetBeingSoothed()
    {
        if (currentState < (int)WeakSoulState.BeingSoothed)
        {
            currentState = (int)WeakSoulState.BeingSoothed;
            SaveState();
        }
    }
    
    public void SetSoothed()
    {
        currentState = (int)WeakSoulState.Soothed;
        SaveState();
    }
}
