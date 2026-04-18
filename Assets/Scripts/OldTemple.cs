using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家进入触发区后显示「正在传送」GameplayTip，2 秒后切换到指定场景。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class OldTemple : MonoBehaviour
{
    [Header("Target Scene")]
    [Tooltip("Build Settings 里已添加的场景名")]
    [SerializeField] private string targetSceneName = "";

    [Header("Tip")]
    [SerializeField] private string teleportTipText = "正在传送";
    [SerializeField] private float delaySeconds = 2f;
    [Tooltip("ShowTip 持续时间，应 ≥ delaySeconds，避免提示提前消失")]
    [SerializeField] private float tipDurationSeconds = 2.5f;
    [SerializeField] private bool cancelWhenExit = true;

    private Coroutine _routine;
    private bool _loading;

    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null)
            c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_loading || _routine != null)
            return;
        if (!other.CompareTag("Player"))
            return;

        _routine = StartCoroutine(TeleportRoutine());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!cancelWhenExit || !other.CompareTag("Player"))
            return;
        if (_loading || _routine == null)
            return;

        StopCoroutine(_routine);
        _routine = null;
        UIManager.HideTipNow();
    }

    private IEnumerator TeleportRoutine()
    {
        float tipDur = Mathf.Max(tipDurationSeconds, delaySeconds + 0.1f);
        UIManager.ShowTip(teleportTipText, tipDur);
        yield return new WaitForSecondsRealtime(delaySeconds);

        _routine = null;
        _loading = true;
        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[OldTemple] targetSceneName 未设置。", this);
            _loading = false;
            return;
        }

        if (SceneTransition.instance != null)
            SceneTransition.instance.TransitionToScene(targetSceneName);
        else
            SceneManager.LoadScene(targetSceneName);
    }
}
