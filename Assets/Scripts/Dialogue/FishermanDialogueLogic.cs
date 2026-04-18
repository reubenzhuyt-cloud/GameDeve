using UnityEngine;

/// <summary>
/// 渔夫对话：与 <see cref="MengPoDialogueLogic"/> 类似，按状态选 JSON（默认复用同一段），可选首次交谈完成任务。
/// 场景挂载：可用 Trigger+CanInteractWith，或（推荐）同物体挂 <see cref="InteractableObject"/> + 本脚本 + <see cref="DialogueObj"/>；
/// 未设置 <c>playerLayer</c> 时 <see cref="InteractableObject"/> 会自动使用名为 Player 的层。
/// 靠近后显示「按 F 对话」，按 F 进入 <see cref="PlayerChatState"/>。
/// Resources 路径：<c>Resources/Dialogue/人物对话文件夹/孟忘和渔夫.json</c> → 填 <c>人物对话文件夹/孟忘和渔夫</c>。
/// </summary>
public class FishermanDialogueLogic : DialogueLogicBase
{
    public enum FishermanState
    {
        Initial = 0,
        TalkedOnce = 1,
    }

    [Header("Dialogue JSON（Resources/Dialogue 下相对路径，无后缀）")]
    [SerializeField] private string dialogueFile = "人物对话文件夹/孟忘和渔夫";

    [Header("Quest（可选）")]
    [Tooltip("首次对话结束时若该任务处于进行中则强制完成；空则忽略")]
    [SerializeField] private string completeOnFirstTalkQuestId;

    public FishermanState CurrentFishermanState => (FishermanState)currentState;

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(objectId))
            objectId = "NPC_Fisherman";
        base.Awake();
    }

    protected override string GetDialogueForState(int state)
    {
        return dialogueFile;
    }

    protected override void ProcessChoice(int choiceIndex, string choiceText)
    {
        // 《孟忘和渔夫》为线性对白，选项在 JSON 内跳转；此处可接任务/好感等扩展
    }

    public override void OnDialogueEnd()
    {
        if (currentState == (int)FishermanState.Initial)
        {
            TryCompleteFirstTalkQuest();
            currentState = (int)FishermanState.TalkedOnce;
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
