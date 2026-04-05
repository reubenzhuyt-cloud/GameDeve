using UnityEngine;

public class MengPoDialogueLogic : DialogueLogicBase
{
    public enum MengPoState
    {
        Initial = 0,
        Considering = 1,
        Accepted = 2,
        MissionInProgress = 3
    }
    
    [Header("MengPo Dialogue Files")]
    [SerializeField] private string initialDialogue = "MengPoDialogue";
    [SerializeField] private string consideringDialogue = "MengPoConsidering";
    [SerializeField] private string acceptedDialogue = "MengPoAccepted";
    [SerializeField] private string missionDialogue = "MengPoMission";
    
    [Header("Quest Integration")]
    [SerializeField] private string completeQuestId = "quest_talk_to_mengpo";
    [SerializeField] private string nextQuestId = "quest_soul_lantern";
    
    public MengPoState CurrentMengPoState => (MengPoState)currentState;
    
    protected override string GetDialogueForState(int state)
    {
        switch ((MengPoState)state)
        {
            case MengPoState.Initial:
                return initialDialogue;
            case MengPoState.Considering:
                return consideringDialogue;
            case MengPoState.Accepted:
                return acceptedDialogue;
            case MengPoState.MissionInProgress:
                return missionDialogue;
            default:
                return initialDialogue;
        }
    }
    
    protected override void ProcessChoice(int choiceIndex, string choiceText)
    {
        MengPoState currentMengPoState = (MengPoState)currentState;
        
        switch (currentMengPoState)
        {
            case MengPoState.Initial:
                ProcessInitialChoice(choiceIndex, choiceText);
                break;
            case MengPoState.Considering:
                ProcessConsideringChoice(choiceIndex, choiceText);
                break;
            case MengPoState.Accepted:
                ProcessAcceptedChoice(choiceIndex, choiceText);
                break;
            case MengPoState.MissionInProgress:
                ProcessMissionChoice(choiceIndex, choiceText);
                break;
        }
    }
    
    private void ProcessInitialChoice(int choiceIndex, string choiceText)
    {
        if (choiceIndex == 0)
        {
            currentState = (int)MengPoState.Accepted;
            CompleteTalkToMengPoQuest();
            StartNextQuest();
            Debug.Log("[MengPo] Player accepted the mission");
        }
        else if (choiceIndex == 1)
        {
            currentState = (int)MengPoState.Considering;
            Debug.Log("[MengPo] Player is considering");
        }
    }
    
    private void ProcessConsideringChoice(int choiceIndex, string choiceText)
    {
        if (choiceIndex == 0)
        {
            currentState = (int)MengPoState.Accepted;
            CompleteTalkToMengPoQuest();
            StartNextQuest();
            Debug.Log("[MengPo] Player accepted after considering");
        }
        else if (choiceIndex == 1)
        {
            currentState = (int)MengPoState.Considering;
            Debug.Log("[MengPo] Player still considering");
        }
    }
    
    private void ProcessAcceptedChoice(int choiceIndex, string choiceText)
    {
        currentState = (int)MengPoState.MissionInProgress;
        Debug.Log("[MengPo] Mission started");
    }
    
    private void ProcessMissionChoice(int choiceIndex, string choiceText)
    {
        Debug.Log("[MengPo] Continuing mission dialogue");
    }
    
    private void CompleteTalkToMengPoQuest()
    {
        if (QuestManager.instance != null && !string.IsNullOrEmpty(completeQuestId))
        {
            if (QuestManager.instance.HasActiveQuest(completeQuestId))
            {
                QuestManager.instance.ForceCompleteQuest(completeQuestId);
                Debug.Log($"[MengPo] Completed quest: {completeQuestId}");
            }
        }
    }
    
    private void StartNextQuest()
    {
        if (QuestManager.instance != null && !string.IsNullOrEmpty(nextQuestId))
        {
            if (QuestManager.instance.CanAcceptQuest(nextQuestId))
            {
                QuestManager.instance.AcceptQuest(nextQuestId);
                Debug.Log($"[MengPo] Started next quest: {nextQuestId}");
            }
        }
    }
    
    public override void OnDialogueEnd()
    {
        base.OnDialogueEnd();
        
        if (currentState == (int)MengPoState.Accepted)
        {
            currentState = (int)MengPoState.MissionInProgress;
            SaveState();
        }
    }
    
    public void ForceSetState(MengPoState state)
    {
        currentState = (int)state;
        SaveState();
    }
}
