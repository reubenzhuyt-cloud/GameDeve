using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpecialAlertUI : MonoBehaviour
{
    public static SpecialAlertUI instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI alertText;
    [SerializeField] private Image backgroundImage;

    [Header("Settings")]
    [SerializeField] private float defaultDuration = 2f;
    [SerializeField] private Color normalColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
    [SerializeField] private float fadeSpeed = 2f;

    private Coroutine currentCoroutine;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        instance = this;
        
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (alertText == null)
        {
            var child = transform.GetChild(0);
            if (child != null)
            {
                alertText = child.GetComponent<TextMeshProUGUI>();
            }
        }

        gameObject.SetActive(false);
    }

    public void Show(string content, float duration = 0f)
    {
        if (duration <= 0f)
        {
            duration = defaultDuration;
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(ShowCoroutine(content, duration));
    }

    public void Hide()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator ShowCoroutine(string content, float duration)
    {
        gameObject.SetActive(true);

        if (alertText != null)
        {
            alertText.text = content;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = normalColor;
        }

        canvasGroup.alpha = 0f;

        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(duration);

        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
        canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
        currentCoroutine = null;
    }

    public static void ShowAlert(string content, float duration = 0f)
    {
        if (instance != null)
        {
            instance.Show(content, duration);
        }
        else
        {
            Debug.LogWarning("[SpecialAlertUI] Instance not found. Make sure SpecialAlertUI is in the scene.");
        }
    }

    public static void HideAlert()
    {
        if (instance != null)
        {
            instance.Hide();
        }
    }
}
