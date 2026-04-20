using UnityEngine;

/// <summary>
/// 佃户对话：与 <see cref="FishermanDialogueLogic"/> / 孟婆同款场景配置。
/// <list type="bullet">
/// <item><b>孟婆式（触发器）</b>：物体 Tag = <c>CanInteractWith</c>，<see cref="BoxCollider2D"/> 勾选 Is Trigger，<see cref="Rigidbody2D"/> Body Type = Kinematic。</item>
/// <item><b>或</b>：挂 <see cref="InteractableObject"/>，用距离检测显示「按 F 对话」。</item>
/// </list>
/// 运行时补 <see cref="DialogueObj"/> 与 Trigger/RB（见 <see cref="DialogueLogicBase.EnsureLineNpcInteractSupport"/>）。
/// Resources：<c>Resources/Dialogue/人物对话文件夹/孟忘和佃户.json</c> → 填 <c>人物对话文件夹/孟忘和佃户</c>。
/// </summary>
public class DianHuDialogueLogic : DialogueLogicBase
{
    public enum DianHuState
    {
        Initial = 0,
        TalkedOnce = 1,
    }

    [Header("Dialogue JSON（Resources/Dialogue 下相对路径，无后缀）")]
    [SerializeField] private string dialogueFile = "人物对话文件夹/孟忘和佃户";

    [Header("Quest（可选）")]
    [Tooltip("首次对话结束时若该任务处于进行中则强制完成；空则忽略")]
    [SerializeField] private string completeOnFirstTalkQuestId;

    public DianHuState CurrentDianHuState => (DianHuState)currentState;

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(objectId))
            objectId = "NPC_DianHu";

        EnsureLineNpcInteractSupport(dialogueFile);

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        WarnLineNpcInteractSetupIfIncomplete(nameof(DianHuDialogueLogic));
    }

    protected override string GetDialogueForState(int state)
    {
        return dialogueFile;
    }

    protected override void ProcessChoice(int choiceIndex, string choiceText)
    {
        // 《孟忘和佃户》分支由 JSON 驱动；可在此接任务/标记
    }

    public override void OnDialogueEnd()
    {
        if (currentState == (int)DianHuState.Initial)
        {
            TryCompleteFirstTalkQuest();
            currentState = (int)DianHuState.TalkedOnce;
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
