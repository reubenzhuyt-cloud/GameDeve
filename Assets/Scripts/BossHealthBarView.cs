using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 血条：两张 <see cref="Image"/>（建议 Filled / Horizontal），一张立即反映真实血量，另一张在受伤后延迟再缓降到目标。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHealthBarView : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("立刻随当前血量变化的填充条（Image Type = Filled）。")]
    [SerializeField] private Image immediateFill;

    [Tooltip("延迟后缓慢降到与 immediate 一致，形成缓降条。")]
    [SerializeField] private Image delayedFill;

    [Tooltip("如「100/100」血量文字。")]
    [SerializeField] private TMP_Text hpText;

    [Header("Delayed bar")]
    [Tooltip("受伤后延迟多久再开始缓降（秒）。")]
    [SerializeField] private float delayBeforeDelayedLerpSeconds = 1f;

    [Tooltip("缓降条每秒在归一化填充上向目标靠近的速度（0~1 区间）。")]
    [SerializeField] private float delayedFillLerpPerSecond = 0.85f;

    private Coroutine _delayedRoutine;

    private void OnDisable()
    {
        if (_delayedRoutine != null)
        {
            StopCoroutine(_delayedRoutine);
            _delayedRoutine = null;
        }
    }

    /// <summary>开局或切阶段：两条与文字一致，并清掉缓降协程。</summary>
    public void SetHealthFullSync(int current, int maxHp)
    {
        if (_delayedRoutine != null)
        {
            StopCoroutine(_delayedRoutine);
            _delayedRoutine = null;
        }

        float n = Normalized(current, maxHp);
        if (immediateFill != null)
            immediateFill.fillAmount = n;
        if (delayedFill != null)
            delayedFill.fillAmount = n;
        SetText(current, maxHp);
    }

    /// <summary>受伤后：立即条立刻变；缓降条在 <see cref="delayBeforeDelayedLerpSeconds"/> 后再移向目标。</summary>
    public void OnHealthChangedAfterDamage(int current, int maxHp)
    {
        float n = Normalized(current, maxHp);
        if (immediateFill != null)
            immediateFill.fillAmount = n;
        SetText(current, maxHp);

        if (_delayedRoutine != null)
            StopCoroutine(_delayedRoutine);
        _delayedRoutine = StartCoroutine(CoDelayedBarCatchUp(n));
    }

    private IEnumerator CoDelayedBarCatchUp(float targetFill)
    {
        if (delayedFill == null)
            yield break;

        yield return new WaitForSeconds(delayBeforeDelayedLerpSeconds);

        float speed = Mathf.Max(0.01f, delayedFillLerpPerSecond);
        while (Mathf.Abs(delayedFill.fillAmount - targetFill) > 0.002f)
        {
            delayedFill.fillAmount = Mathf.MoveTowards(delayedFill.fillAmount, targetFill, speed * Time.deltaTime);
            yield return null;
        }

        delayedFill.fillAmount = targetFill;
        _delayedRoutine = null;
    }

    private static void SetText(TMP_Text text, int current, int maxHp)
    {
        if (text == null)
            return;
        text.text = $"{current}/{maxHp}";
    }

    private void SetText(int current, int maxHp) => SetText(hpText, current, maxHp);

    private static float Normalized(int current, int maxHp)
    {
        if (maxHp <= 0)
            return 0f;
        return Mathf.Clamp01((float)current / maxHp);
    }
}
