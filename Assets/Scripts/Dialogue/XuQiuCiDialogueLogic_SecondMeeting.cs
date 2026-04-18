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

    [Header("Debug")]
    [Tooltip("勾选后在 Console 输出 2D 触发与 InteractableObject 距离检测边沿，用于排查 Press F / 碰撞")]
    [SerializeField] private bool debugXuQiuCi2Interact = true;

    public XuQiuCiSecondState CurrentStateEnum => (XuQiuCiSecondState)currentState;

    private InteractableObject _interactable;
    private bool _lastProximityInRange;
    private bool _proximityInitialized;

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

        _interactable = GetComponent<InteractableObject>();
        if (debugXuQiuCi2Interact)
        {
            var colHere = GetComponent<Collider2D>();
            var colChild = colHere == null ? GetComponentInChildren<Collider2D>(true) : null;
            if (colHere == null && colChild != null)
            {
                Debug.LogWarning(
                    "[XuQiuCi2] Collider2D 在子物体上时，Unity 不会把 OnTriggerEnter2D 发到本脚本所在物体；触发日志请依赖子物体上的 Collider，或把 Collider 与脚本挂同一物体。",
                    this);
            }
        }
    }

    private void Update()
    {
        if (!debugXuQiuCi2Interact || _interactable == null)
            return;

        bool inRange = _interactable.IsPlayerInRange();
        if (!_proximityInitialized)
        {
            _proximityInitialized = true;
            _lastProximityInRange = inRange;
            return;
        }

        if (inRange == _lastProximityInRange)
            return;

        _lastProximityInRange = inRange;
        Debug.Log(
            $"[XuQiuCi2] InteractableObject 距离圈 ({_interactable.name}): {(inRange ? "进入" : "离开")} interactionRange，玩家是否在圈内={inRange}",
            this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!debugXuQiuCi2Interact)
            return;
        if (!other.GetComponentInParent<Player>())
            return;

        Debug.Log(
            $"[XuQiuCi2] OnTriggerEnter2D ← {other.name} (tag={other.tag}, layer={LayerMask.LayerToName(other.gameObject.layer)})，本物体 Collider={(GetComponent<Collider2D>() != null ? "同物体" : "无/在子物体")}",
            this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!debugXuQiuCi2Interact)
            return;
        if (!other.GetComponentInParent<Player>())
            return;

        Debug.Log($"[XuQiuCi2] OnTriggerExit2D ← {other.name}", this);
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
