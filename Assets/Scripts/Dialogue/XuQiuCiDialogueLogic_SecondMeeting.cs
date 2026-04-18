using UnityEngine;

/// <summary>
/// 许秋慈 · 场景二（后续剧情）：对白文件《孟忘和肉包子的第二次》。
/// 与 <see cref="XuQiuCiDialogueLogic_FirstMeeting"/> 为两个场景各用一份脚本，存档用不同 <see cref="DialogueLogicBase.objectId"/>。
/// 同物体：Collider2D Trigger、Tag「CanInteractWith」、<see cref="DialogueObj"/>、本脚本。
/// </summary>
public class XuQiuCiDialogueLogic_SecondMeeting : DialogueLogicBase
{
    public enum XuQiuCiSecondState
    {
        Initial = 0,
        TalkedOnce = 1,
    }

    [Header("Dialogue JSON")]
    [SerializeField] private string dialogueFile = "人物对话文件夹/孟忘和肉包子的第二次";

    [Header("Quest（可选）")]
    [SerializeField] private string completeOnFirstTalkQuestId;

    public XuQiuCiSecondState CurrentStateEnum => (XuQiuCiSecondState)currentState;

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(objectId))
            objectId = "XuQiuCi_SecondMeeting";
        base.Awake();
    }

    protected override string GetDialogueForState(int state)
    {
        return dialogueFile;
    }

    protected override void ProcessChoice(int choiceIndex, string choiceText)
    {
    }

    public override void OnDialogueEnd()
    {
        if (currentState == (int)XuQiuCiSecondState.Initial)
        {
            TryCompleteFirstTalkQuest();
            currentState = (int)XuQiuCiSecondState.TalkedOnce;
        }

        base.OnDialogueEnd();
    }

    private void TryCompleteFirstTalkQuest()
    {
        if (QuestManager.instance == null || string.IsNullOrEmpty(completeOnFirstTalkQuestId))
            return;
        if (QuestManager.instance.HasActiveQuest(completeOnFirstTalkQuestId))
            QuestManager.instance.ForceCompleteQuest(completeOnFirstTalkQuestId);
    }
}
