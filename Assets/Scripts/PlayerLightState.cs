using System.Collections;
using UnityEngine;

/// <summary>
/// Q 光芒：范围内找 <see cref="WeakSoul"/>。2D 项目用 XY 距离（忽略 Z），避免与精灵 Z 不一致时永远判超距。
/// 标签可在自身、父级或子级物体上。
/// </summary>
public class PlayerLightState : PlayerState
{
    private Coroutine lightRoutine;

    public PlayerLightState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName)
        : base(player, stateMachine, animationBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        if (lightRoutine != null)
            player.StopCoroutine(lightRoutine);

        float fxDuration = Mathf.Max(0.01f, player.lightSkillEffectDuration);

        WeakSoul soul = FindClosestWeakSoulInRange();
        if (soul != null)
        {
            if (EffectManager.instance != null)
                EffectManager.instance.Play(fxDuration);

            player.pendingLightSootheSoul = soul;
            player.pendingLightSootheDialogueFile = player.lightSootheDialogueFile;
            stateMachine.ChangeState(player.chatState);
            return;
        }

        lightRoutine = player.StartCoroutine(LightPlayRoutine());
    }

    private WeakSoul FindClosestWeakSoulInRange()
    {
        Transform pt = player.transform;
        float maxSq = player.lightSootheRange * player.lightSootheRange;
        WeakSoul best = null;
        float bestSq = float.MaxValue;

        void Consider(WeakSoul w, Vector3 worldPos)
        {
            if (w == null || w.isDissipated)
                return;
            float sq = SqrDistanceXY(pt.position, worldPos);
            if (sq > maxSq)
                return;
            if (best == null || sq < bestSq)
            {
                bestSq = sq;
                best = w;
            }
        }

        var all = Object.FindObjectsByType<WeakSoul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bool useTag = !string.IsNullOrEmpty(player.weakSoulTag);

        if (useTag)
        {
            GameObject[] tagged;
            try
            {
                tagged = GameObject.FindGameObjectsWithTag(player.weakSoulTag);
            }
            catch (UnityException)
            {
                tagged = System.Array.Empty<GameObject>();
            }

            for (int i = 0; i < tagged.Length; i++)
            {
                GameObject go = tagged[i];
                if (go == null || !go.activeInHierarchy)
                    continue;

                WeakSoul w = ResolveWeakSoul(go);
                Consider(w, w != null ? w.transform.position : go.transform.position);
            }
        }

        for (int i = 0; i < all.Length; i++)
        {
            WeakSoul w = all[i];
            if (w == null)
                continue;
            if (useTag && !WeakSoulMatchesTagFilter(w, player.weakSoulTag))
                continue;
            Consider(w, w.transform.position);
        }

        // Tag 配了但层级/拼写不一致时，上面会全灭；最后不按标签再试一次（便于排查）
        if (best == null && useTag)
        {
            Debug.LogWarning(
                "[PlayerLightState] 在 lightSootheRange 内未找到带标签 \"" + player.weakSoulTag +
                "\" 的 WeakSoul；已临时忽略标签再搜一次。请确认 Tag 挂在灵魂根/父/子之一，或与 Player.weakSoulTag 一致。");
            for (int i = 0; i < all.Length; i++)
            {
                WeakSoul w = all[i];
                if (w == null || w.isDissipated)
                    continue;
                Consider(w, w.transform.position);
            }
        }

        return best;
    }

    private static float SqrDistanceXY(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    /// <summary>标签物体上或子层级上的 <see cref="WeakSoul"/>。</summary>
    private static WeakSoul ResolveWeakSoul(GameObject go)
    {
        if (go == null)
            return null;
        WeakSoul w = go.GetComponent<WeakSoul>();
        if (w != null)
            return w;
        w = go.GetComponentInChildren<WeakSoul>(true);
        if (w != null)
            return w;
        return go.GetComponentInParent<WeakSoul>();
    }

    /// <summary>在灵魂根物体及其所有父、子节点上是否存在指定 Tag。</summary>
    private static bool WeakSoulMatchesTagFilter(WeakSoul w, string tag)
    {
        if (w == null || string.IsNullOrEmpty(tag))
            return true;

        for (Transform t = w.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(tag))
                return true;
        }

        foreach (Transform t in w.GetComponentsInChildren<Transform>(true))
        {
            if (t.CompareTag(tag))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 协程：播固定时长粒子，结束后回 Idle；再次进入本状态会先 Stop 上一次，可重复释放。
    /// </summary>
    private IEnumerator LightPlayRoutine()
    {
        float duration = Mathf.Max(0.01f, player.lightSkillEffectDuration);

        if (EffectManager.instance != null)
            EffectManager.instance.Play(duration);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        lightRoutine = null;
        stateMachine.ChangeState(player.idleState);
    }

    public override void Update()
    {
        base.Update();

        if (player.XInput != 0)
        {
            player.SetVelocity(player.XInput * player.moveSpeed);
            player.FlipCheck();
        }
        else
        {
            player.SetVelocity(0);
        }
    }

    public override void Exit()
    {
        base.Exit();

        if (lightRoutine != null)
        {
            player.StopCoroutine(lightRoutine);
            lightRoutine = null;
        }

        // 不 Stop 粒子：交给 EffectManager 协程按时长自然结束，且时间用 unscaled，对话暂停时间缩放也不打断
    }
}
