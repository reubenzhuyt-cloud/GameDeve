using System.Collections;
using UnityEngine;

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
        if (string.IsNullOrEmpty(player.weakSoulTag))
            return null;

        GameObject[] tagged;
        try
        {
            tagged = GameObject.FindGameObjectsWithTag(player.weakSoulTag);
        }
        catch (UnityException)
        {
            return null;
        }

        Transform pt = player.transform;
        float maxSq = player.lightSootheRange * player.lightSootheRange;
        WeakSoul best = null;
        float bestSq = float.MaxValue;

        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (go == null || !go.activeInHierarchy)
                continue;

            WeakSoul w = go.GetComponent<WeakSoul>();
            if (w == null || w.isDissipated)
                continue;

            float sq = (pt.position - go.transform.position).sqrMagnitude;
            if (sq > maxSq)
                continue;
            if (best == null || sq < bestSq)
            {
                bestSq = sq;
                best = w;
            }
        }

        return best;
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
