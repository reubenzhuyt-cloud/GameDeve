using UnityEngine;

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
            weakSoul = GetComponent<WeakSoul>();
        }
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
