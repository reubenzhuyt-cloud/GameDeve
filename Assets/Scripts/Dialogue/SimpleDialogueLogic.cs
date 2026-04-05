using UnityEngine;

public class SimpleDialogueLogic : DialogueLogicBase
{
    [Header("Simple Dialogue Settings")]
    [SerializeField] private string firstTimeDialogue;
    [SerializeField] private string subsequentDialogue;
    
    protected override string GetDialogueForState(int state)
    {
        if (state == 0 && !string.IsNullOrEmpty(firstTimeDialogue))
        {
            return firstTimeDialogue;
        }
        return subsequentDialogue;
    }
    
    protected override void ProcessChoice(int choiceIndex, string choiceText)
    {
        if (currentState == 0)
        {
            currentState = 1;
            SaveState();
        }
    }
    
    public override void OnDialogueEnd()
    {
        base.OnDialogueEnd();
        
        if (currentState == 0)
        {
            currentState = 1;
            SaveState();
        }
    }
}
