using UnityEngine;

/// <summary>
/// 许秋慈 · 场景一（首次相遇）：对白文件《孟忘和肉包子第一次》。
/// 与 <see cref="XuQiuCiDialogueLogic_SecondMeeting"/> 分场景各挂一份，<see cref="DialogueLogicBase.objectId"/> 默认不同，存档互不覆盖。
/// 同物体：Collider2D Trigger、Tag「CanInteractWith」、<see cref="DialogueObj"/>、本脚本；玩家靠近后 UI 提示，按 F 对话。
/// </summary>
public class XuQiuCiDialogueLogic_FirstMeeting : DialogueLogicBase
{
    public enum XuQiuCiFirstState
    {
        Initial = 0,
        TalkedOnce = 1,
    }

    [Header("Dialogue JSON")]
    [SerializeField] private string dialogueFile = "人物对话文件夹/孟忘和肉包子第一次";

    [Header("Quest（可选）")]
    [SerializeField] private string completeOnFirstTalkQuestId;

    public XuQiuCiFirstState CurrentStateEnum => (XuQiuCiFirstState)currentState;

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(objectId))
            objectId = "XuQiuCi_FirstMeeting";
        base.Awake();
    }

    protected override string GetDialogueForState(int state)
    {
        return dialogueFile;
    }

    protected override void ProcessChoice(int choiceIndex, string choiceText)
    {
        // 分支由 JSON 的 choices 驱动；可在此接任务/标记
    }

    public override void OnDialogueEnd()
    {
        if (currentState == (int)XuQiuCiFirstState.Initial)
        {
            TryCompleteFirstTalkQuest();
            currentState = (int)XuQiuCiFirstState.TalkedOnce;
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
