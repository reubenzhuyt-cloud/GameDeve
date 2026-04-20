using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Timed tip UI. Only assign intro text here; motion uses built-in defaults.
/// </summary>
public class GameplayTipUI : MonoBehaviour
{
    private const float EnterSeconds = 0.35f;
    private const float ExitSeconds = 0.35f;
    private static readonly Vector2 HiddenOffset = new Vector2(100f, 0);

    [Header("Optional refs")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI textMeshPro;
    [SerializeField] private Text uiText;

    [Header("Intro (filled in Inspector)")]
    [SerializeField] private bool playIntroWhenSceneReady = true;
    [SerializeField] private float introDuration = 10f;
    [TextArea(2, 8)]
    [SerializeField] private string introMessage = "";

    private RectTransform animatedRect;
    private CanvasGroup canvasGroup;
    private Vector2 posShown;
    private Vector2 posHidden;
    private Vector3 scaleShown = Vector3.one;
    private Vector3 scaleHidden = Vector3.one;

    private Coroutine showRoutine;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        animatedRect = panelRoot.GetComponent<RectTransform>();
        canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (textMeshPro == null && uiText == null)
        {
            textMeshPro = GetComponentInChildren<TextMeshProUGUI>(true);
            if (textMeshPro == null)
                uiText = GetComponentInChildren<Text>(true);
        }

        if (animatedRect != null)
        {
            posShown = animatedRect.anchoredPosition;
            scaleShown = animatedRect.localScale;
            posHidden = posShown + HiddenOffset;
            scaleHidden = scaleShown;
            ApplyRectState(posHidden, scaleHidden);
        }

        canvasGroup.alpha = 0f;
        panelRoot.SetActive(false);
    }

    internal void PlayIntroFromManager()
    {
        if (!playIntroWhenSceneReady || introDuration <= 0f || string.IsNullOrEmpty(introMessage))
            return;
        Show(introMessage, introDuration);
    }

    internal void Show(string content, float duration)
    {
        if (duration <= 0f)
        {
            HideNow();
            return;
        }

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(ShowRoutine(content, duration));
    }

    internal void HideNow()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (animatedRect != null)
            ApplyRectState(posHidden, scaleHidden);
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private IEnumerator ShowRoutine(string content, float duration)
    {
        ApplyText(content);
        ApplyRectState(posHidden, scaleHidden);
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        panelRoot.SetActive(true);

        yield return TransitionVisual(posHidden, scaleHidden, 0f, posShown, scaleShown, 1f, EnterSeconds);
        yield return new WaitForSecondsRealtime(duration);
        yield return TransitionVisual(posShown, scaleShown, 1f, posHidden, scaleHidden, 0f, ExitSeconds);

        if (panelRoot != null)
            panelRoot.SetActive(false);
        showRoutine = null;
    }

    private IEnumerator TransitionVisual(
        Vector2 fromPos, Vector3 fromScale, float fromAlpha,
        Vector2 toPos, Vector3 toScale, float toAlpha,
        float seconds)
    {
        if (seconds <= 0f)
        {
            ApplyRectState(toPos, toScale);
            ApplyAlpha(toAlpha);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float k = Mathf.Clamp01(t);
            k = k * k * (3f - 2f * k);
            if (animatedRect != null)
            {
                animatedRect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, k);
                animatedRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, k);
            }
            float a = Mathf.LerpUnclamped(fromAlpha, toAlpha, k);
            ApplyAlpha(a);
            yield return null;
        }

        ApplyRectState(toPos, toScale);
        ApplyAlpha(toAlpha);
    }

    private void ApplyAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }

    private void ApplyRectState(Vector2 pos, Vector3 scale)
    {
        if (animatedRect == null)
            return;
        animatedRect.anchoredPosition = pos;
        animatedRect.localScale = scale;
    }

    private void ApplyText(string content)
    {
        if (textMeshPro != null)
            textMeshPro.text = content;
        else if (uiText != null)
            uiText.text = content;
    }

    private void OnDisable()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
    }
}
