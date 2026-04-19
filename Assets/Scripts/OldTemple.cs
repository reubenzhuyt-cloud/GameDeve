using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 玩家进入触发区后：可选在拖入的 Panel 上通过子物体 TipText 显示「正在前往下个场景」与倒计时，
/// Panel 从右侧偏移一整格宽度滑入，结束后滑回再切场景；未拖 Panel 时沿用 GameplayTip。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class OldTemple : MonoBehaviour
{
    [Header("Target Scene")]
    [Tooltip("Build Settings 里已添加的场景名")]
    [SerializeField] private string targetSceneName = "";

    [Header("Countdown Panel（可选）")]
    [Tooltip("拖入含 TipText（TMP）子物体的 Panel 根节点 RectTransform")]
    [SerializeField] private RectTransform countdownPanel;
    [Tooltip("留空则在 Panel 子级中按名称 TipText 查找")]
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private string countdownPrefix = "正在前往下个场景 ";
    [SerializeField] private float slideDuration = 0.35f;

    [Header("Timing")]
    [SerializeField] private float delaySeconds = 1f;
    [SerializeField] private bool cancelWhenExit = true;

    [Header("Fallback：未拖 Panel 时用 GameplayTip")]
    [SerializeField] private string teleportTipText = "正在传送";
    [Tooltip("ShowTip 持续时间，应 ≥ delaySeconds")]
    [SerializeField] private float tipDurationSeconds = 1.1f;

    private Coroutine _routine;
    private bool _loading;
    private Vector2 _panelAnchoredShown;

    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null)
            c.isTrigger = true;
    }

    private void Awake()
    {
        if (countdownPanel != null)
            _panelAnchoredShown = countdownPanel.anchoredPosition;
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
        ResetCountdownPanelVisual();
        UIManager.HideTipNow();
    }

    private void ResetCountdownPanelVisual()
    {
        if (countdownPanel == null)
            return;
        countdownPanel.anchoredPosition = _panelAnchoredShown;
        countdownPanel.gameObject.SetActive(false);
    }

    private void ResolveTipText()
    {
        if (tipText != null || countdownPanel == null)
            return;

        foreach (var tmp in countdownPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == "TipText")
            {
                tipText = tmp;
                return;
            }
        }

        tipText = countdownPanel.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static float GetPanelSlideWidth(RectTransform rt)
    {
        float w = Mathf.Abs(rt.rect.width);
        if (w < 1f)
            w = Mathf.Abs(rt.sizeDelta.x);
        if (w < 1f)
            w = 400f;
        return w;
    }

    private IEnumerator SlideAnchored(RectTransform rt, Vector2 from, Vector2 to, float seconds)
    {
        if (seconds <= 0f)
        {
            rt.anchoredPosition = to;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float k = Mathf.Clamp01(t);
            k = k * k * (3f - 2f * k);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }

        rt.anchoredPosition = to;
    }

    private IEnumerator TeleportRoutine()
    {
        if (countdownPanel == null)
        {
            float tipDur = Mathf.Max(tipDurationSeconds, delaySeconds + 0.1f);
            UIManager.ShowTip(teleportTipText, tipDur);
            yield return new WaitForSecondsRealtime(delaySeconds);
        }
        else
        {
            ResolveTipText();

            RectTransform rt = countdownPanel;
            float width = GetPanelSlideWidth(rt);
            Vector2 shown = _panelAnchoredShown;
            Vector2 hiddenRight = shown + new Vector2(width, 0f);

            rt.gameObject.SetActive(true);
            rt.anchoredPosition = hiddenRight;

            yield return SlideAnchored(rt, hiddenRight, shown, slideDuration);

            float remaining = delaySeconds;
            while (remaining > 0f)
            {
                if (tipText != null)
                    tipText.text = $"{countdownPrefix}{Mathf.Max(0f, remaining):F1}";
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (tipText != null)
                tipText.text = $"{countdownPrefix}0.0";

            yield return SlideAnchored(rt, shown, hiddenRight, slideDuration);

            rt.anchoredPosition = shown;
            rt.gameObject.SetActive(false);
        }

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
