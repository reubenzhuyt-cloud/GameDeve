using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 许秋慈 · 场景二（后续剧情）：对白文件《孟忘和肉包子的第二次》。
/// 与 <see cref="XuQiuCiDialogueLogic_FirstMeeting"/> 为两个场景各用一份脚本，存档用不同 <see cref="DialogueLogicBase.objectId"/>。
/// 交互请在场景里自行配置（子物体 Trigger + Tag「CanInteractWith」亦可）：本脚本只绑定对白资源，不自动加碰撞体，避免与场景重复挂载冲突。
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

    [Header("场景切换")]
    [Tooltip("《第二次》对白首次播完（存档从 Initial→TalkedOnce）后进入的场景")]
    [SerializeField] private string nextSceneAfterSecondMeeting = "BossFight";

    public XuQiuCiSecondState CurrentStateEnum => (XuQiuCiSecondState)currentState;

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(objectId))
            objectId = "XuQiuCi_SecondMeeting";

        BindDialogueObjResource(dialogueFile);

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        WarnLineNpcInteractSetupIfIncomplete(nameof(XuQiuCiDialogueLogic_SecondMeeting));
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
        bool firstCompletion = currentState == (int)XuQiuCiSecondState.Initial;

        if (firstCompletion)
        {
            TryCompleteFirstTalkQuest();
            currentState = (int)XuQiuCiSecondState.TalkedOnce;
        }

        base.OnDialogueEnd();

        if (firstCompletion && !string.IsNullOrEmpty(nextSceneAfterSecondMeeting))
        {
            if (SceneTransition.instance != null)
                SceneTransition.instance.TransitionToScene(nextSceneAfterSecondMeeting);
            else
                SceneManager.LoadScene(nextSceneAfterSecondMeeting);
        }
    }

    private void TryCompleteFirstTalkQuest()
    {
        if (QuestManager.instance == null || string.IsNullOrEmpty(completeOnFirstTalkQuestId))
            return;
        if (QuestManager.instance.HasActiveQuest(completeOnFirstTalkQuestId))
            QuestManager.instance.ForceCompleteQuest(completeOnFirstTalkQuestId);
    }
}
