using UnityEngine;

/// <summary>
/// 渔夫对话：与 <see cref="MengPoDialogueLogic"/> / 孟婆同款场景配置。
/// <list type="bullet">
/// <item><b>孟婆式（触发器）</b>：物体 Tag = <c>CanInteractWith</c>，<see cref="BoxCollider2D"/> 勾选 Is Trigger，<see cref="Rigidbody2D"/> Body Type = Kinematic（与孟婆一致）。</item>
/// <item><b>或</b>：挂 <see cref="InteractableObject"/>，用距离检测显示「按 F 对话」。</item>
/// </list>
/// 本脚本会在运行时补一个 <see cref="DialogueObj"/>（若缺失），便于 <see cref="Player"/> 的 F 提示锚点与孟婆一致。
/// Resources：<c>Resources/Dialogue/人物对话文件夹/孟忘和渔夫.json</c> → 填 <c>人物对话文件夹/孟忘和渔夫</c>。
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

        EnsureLineNpcInteractSupport(dialogueFile);

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        WarnLineNpcInteractSetupIfIncomplete(nameof(FishermanDialogueLogic));
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
