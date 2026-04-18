using System.Collections;
using UnityEngine;

/// <summary>
/// 与常规可交互物相同：靠近后出现「按 F」提示，按 F 后将目标 <see cref="SpriteRenderer"/> 的图像替换为拖入的 Sprite。
/// 推荐：物体带 <c>CanInteractWith</c> Tag、<see cref="BoxCollider2D"/> Is Trigger；勾选 <see cref="useTriggerOnly"/> 时仅用触发器提示（与孟婆/渔夫一致）。
/// </summary>
public class SpiritSpriteSwapOnInteract : InteractableObject
{
    [Header("Sprite 替换")]
    [Tooltip("要改图的 SpriteRenderer（例如游魂身上的渲染器）")]
    [SerializeField] private SpriteRenderer spiritRenderer;
    [Tooltip("按下 F 后替换成的 Sprite")]
    [SerializeField] private Sprite replacementSprite;

    [Header("行为")]
    [Tooltip("为 true 时不在 Update 里做距离检测，只靠带 CanInteractWith 的 Trigger（与场景里其他交互一致）")]
    [SerializeField] private bool useTriggerOnly = true;
    [Tooltip("替换一次后关闭碰撞并隐藏 F 提示")]
    [SerializeField] private bool disableAfterUse = true;

    protected override void Awake()
    {
        base.Awake();
        if (spiritRenderer == null)
            spiritRenderer = GetComponent<SpriteRenderer>()
                ?? GetComponentInChildren<SpriteRenderer>(true);
    }

    protected override void Update()
    {
        if (!useTriggerOnly)
            base.Update();
    }

    /// <summary>不在基类里要求 DialogueObj；按 F 只换图。</summary>
    public override void Interact()
    {
        if (!isInteractable)
            return;

        if (spiritRenderer != null && replacementSprite != null)
            spiritRenderer.sprite = replacementSprite;

        StartCoroutine(ReturnToIdleNextFrame());
    }

    private IEnumerator ReturnToIdleNextFrame()
    {
        yield return null;

        var player = Object.FindFirstObjectByType<Player>();
        if (player != null && player.stateMachine != null
            && player.stateMachine.currentState is PlayerChatState)
        {
            player.stateMachine.ChangeState(player.idleState);
        }

        if (!disableAfterUse)
            yield break;

        SetInteractable(false);

        var col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        if (player != null)
        {
            if (player.currentInteractable != null && ReferenceEquals(player.currentInteractable, this))
                player.currentInteractable = null;
            player.UnregisterProximityInteractable(this);
            player.SetWorldInteractionTip(false, "", Vector3.zero);
        }
    }
}
