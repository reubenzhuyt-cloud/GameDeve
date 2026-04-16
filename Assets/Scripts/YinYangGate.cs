using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家进入触发区后：显示「正在进入白云浦-倒计时3s」提示，
/// 保持 3 秒后自动切换场景。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class YinYangGate : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string targetSceneName = "BaiYunPu_Day";

    [Header("Tip")]
    [SerializeField] private string enteringTipPrefix = "正在进入白云浦-倒计时";
    [SerializeField] private int countdownSeconds = 3;
    [SerializeField] private bool cancelWhenExit = true;

    private Coroutine enteringRoutine;
    private bool isSwitching;

    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null)
            c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isSwitching || enteringRoutine != null)
            return;
        if (!other.CompareTag("Player"))
            return;

        enteringRoutine = StartCoroutine(EnteringRoutine());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!cancelWhenExit || !other.CompareTag("Player"))
            return;
        if (isSwitching || enteringRoutine == null)
            return;

        StopCoroutine(enteringRoutine);
        enteringRoutine = null;
        UIManager.HideTipNow();
    }

    private IEnumerator EnteringRoutine()
    {
        int sec = Mathf.Max(1, countdownSeconds);
        for (int i = sec; i >= 1; i--)
        {
            UIManager.ShowTip($"{enteringTipPrefix}{i}s", 1.05f);
            yield return new WaitForSecondsRealtime(1f);
        }

        isSwitching = true;
        enteringRoutine = null;
        SwitchScene();
    }

    private void SwitchScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[YinYangGate] targetSceneName 为空。", this);
            isSwitching = false;
            return;
        }

        if (SceneTransition.instance != null)
            SceneTransition.instance.TransitionToScene(targetSceneName);
        else
            SceneManager.LoadScene(targetSceneName);
    }
}
