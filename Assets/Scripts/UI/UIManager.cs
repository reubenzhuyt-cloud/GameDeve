using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public enum UIType
{
    None = 0,
    DialoguePanel = 1,
    ChoicePanel = 2,
    InteractionTip = 3,
    QuestPanel = 4,
    QuestTracker = 5,
    SpecialAlert = 6,
    SoulLanternUI = 7,
    TimeUI = 8
}

[System.Serializable]
public class UIPanel
{
    public UIType type;
    public GameObject gameObject;
    public CanvasGroup canvasGroup;
    public bool useCanvasGroup = true;
    public bool startHidden = true;
    public bool isExclusive = false;
}

[System.Serializable]
public class UIExclusiveRule
{
    public UIType exclusiveUI;
    public List<UIType> hideWhenActive = new List<UIType>();
    public List<UIType> blockWhenActive = new List<UIType>();
}

public class UIManager : MonoBehaviour
{
    public static UIManager instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private List<UIPanel> uiPanels = new List<UIPanel>();

    [Header("Exclusive Rules")]
    [SerializeField] private List<UIExclusiveRule> exclusiveRules = new List<UIExclusiveRule>();

    [Header("Settings")]
    [SerializeField] private float defaultFadeSpeed = 5f;

    private Dictionary<UIType, UIPanel> panelDict = new Dictionary<UIType, UIPanel>();
    private Dictionary<UIType, Coroutine> fadeCoroutines = new Dictionary<UIType, Coroutine>();
    private Dictionary<UIType, UIExclusiveRule> exclusiveRuleDict = new Dictionary<UIType, UIExclusiveRule>();
    private HashSet<UIType> activeExclusiveUIs = new HashSet<UIType>();

    public UnityEvent<UIType> onUIShown = new UnityEvent<UIType>();
    public UnityEvent<UIType> onUIHidden = new UnityEvent<UIType>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeExclusiveRules();
        InitializePanels();
    }

    private void InitializeExclusiveRules()
    {
        exclusiveRuleDict.Clear();
        foreach (var rule in exclusiveRules)
        {
            exclusiveRuleDict[rule.exclusiveUI] = rule;
        }

        if (!exclusiveRuleDict.ContainsKey(UIType.DialoguePanel))
        {
            var dialogueRule = new UIExclusiveRule
            {
                exclusiveUI = UIType.DialoguePanel,
                hideWhenActive = new List<UIType> { UIType.InteractionTip },
                blockWhenActive = new List<UIType> { UIType.QuestPanel }
            };
            exclusiveRuleDict[UIType.DialoguePanel] = dialogueRule;
        }
    }

    private void InitializePanels()
    {
        panelDict.Clear();

        foreach (var panel in uiPanels)
        {
            if (panel.gameObject == null)
            {
                Debug.LogWarning($"[UIManager] Panel {panel.type} has no GameObject assigned.");
                continue;
            }

            if (panel.canvasGroup == null)
            {
                panel.canvasGroup = panel.gameObject.GetComponent<CanvasGroup>();
            }

            panelDict[panel.type] = panel;

            if (panel.startHidden)
            {
                HideImmediate(panel);
            }
        }
    }

    public void RegisterPanel(UIType type, GameObject obj, bool useCanvasGroup = true, bool startHidden = true, bool isExclusive = false)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[UIManager] Cannot register null GameObject for {type}");
            return;
        }

        var panel = new UIPanel
        {
            type = type,
            gameObject = obj,
            canvasGroup = obj.GetComponent<CanvasGroup>(),
            useCanvasGroup = useCanvasGroup,
            startHidden = startHidden,
            isExclusive = isExclusive
        };

        panelDict[type] = panel;

        if (startHidden)
        {
            HideImmediate(panel);
        }
    }

    public void UnregisterPanel(UIType type)
    {
        if (panelDict.ContainsKey(type))
        {
            panelDict.Remove(type);
        }
    }

    public void Show(UIType type, bool instant = true)
    {
        if (!panelDict.TryGetValue(type, out var panel))
        {
            Debug.LogWarning($"[UIManager] Panel {type} not found.");
            return;
        }

        if (IsBlockedByExclusiveUI(type))
        {
            Debug.Log($"[UIManager] {type} is blocked by exclusive UI.");
            return;
        }

        ApplyExclusiveRulesOnShow(type);

        if (instant)
        {
            ShowImmediate(panel);
        }
        else
        {
            ShowFade(panel);
        }

        onUIShown?.Invoke(type);
    }

    public void Hide(UIType type, bool instant = true)
    {
        if (!panelDict.TryGetValue(type, out var panel))
        {
            Debug.LogWarning($"[UIManager] Panel {type} not found.");
            return;
        }

        if (instant)
        {
            HideImmediate(panel);
        }
        else
        {
            HideFade(panel);
        }

        ApplyExclusiveRulesOnHide(type);
        onUIHidden?.Invoke(type);
    }

    public void Toggle(UIType type, bool instant = true)
    {
        if (IsVisible(type))
        {
            Hide(type, instant);
        }
        else
        {
            Show(type, instant);
        }
    }

    public bool IsVisible(UIType type)
    {
        if (!panelDict.TryGetValue(type, out var panel))
        {
            return false;
        }

        if (panel.useCanvasGroup && panel.canvasGroup != null)
        {
            return panel.canvasGroup.alpha > 0.01f;
        }

        return panel.gameObject.activeSelf;
    }

    public bool IsPanelRegistered(UIType type)
    {
        return panelDict.ContainsKey(type);
    }

    public GameObject GetPanel(UIType type)
    {
        if (panelDict.TryGetValue(type, out var panel))
        {
            return panel.gameObject;
        }
        return null;
    }

    public T GetPanelComponent<T>(UIType type) where T : Component
    {
        var panel = GetPanel(type);
        if (panel != null)
        {
            return panel.GetComponent<T>();
        }
        return null;
    }

    public bool IsBlockedByExclusiveUI(UIType type)
    {
        foreach (var exclusiveType in activeExclusiveUIs)
        {
            if (exclusiveRuleDict.TryGetValue(exclusiveType, out var rule))
            {
                if (rule.blockWhenActive.Contains(type))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool HasActiveExclusiveUI()
    {
        return activeExclusiveUIs.Count > 0;
    }

    private void ApplyExclusiveRulesOnShow(UIType type)
    {
        if (exclusiveRuleDict.TryGetValue(type, out var rule))
        {
            activeExclusiveUIs.Add(type);

            foreach (var hideType in rule.hideWhenActive)
            {
                if (IsVisible(hideType))
                {
                    Hide(hideType);
                }
            }
        }
    }

    private void ApplyExclusiveRulesOnHide(UIType type)
    {
        activeExclusiveUIs.Remove(type);
    }

    private void ShowImmediate(UIPanel panel)
    {
        if (panel.useCanvasGroup && panel.canvasGroup != null)
        {
            panel.canvasGroup.alpha = 1f;
            panel.canvasGroup.interactable = true;
            panel.canvasGroup.blocksRaycasts = true;
        }

        panel.gameObject.SetActive(true);
    }

    private void HideImmediate(UIPanel panel)
    {
        if (panel.useCanvasGroup && panel.canvasGroup != null)
        {
            panel.canvasGroup.alpha = 0f;
            panel.canvasGroup.interactable = false;
            panel.canvasGroup.blocksRaycasts = false;
        }

        panel.gameObject.SetActive(false);
    }

    private void ShowFade(UIPanel panel)
    {
        if (fadeCoroutines.ContainsKey(panel.type))
        {
            if (fadeCoroutines[panel.type] != null)
            {
                StopCoroutine(fadeCoroutines[panel.type]);
            }
        }

        fadeCoroutines[panel.type] = StartCoroutine(FadeCoroutine(panel, true));
    }

    private void HideFade(UIPanel panel)
    {
        if (fadeCoroutines.ContainsKey(panel.type))
        {
            if (fadeCoroutines[panel.type] != null)
            {
                StopCoroutine(fadeCoroutines[panel.type]);
            }
        }

        fadeCoroutines[panel.type] = StartCoroutine(FadeCoroutine(panel, false));
    }

    private System.Collections.IEnumerator FadeCoroutine(UIPanel panel, bool show)
    {
        if (panel.canvasGroup == null)
        {
            panel.gameObject.SetActive(show);
            yield break;
        }

        if (show)
        {
            panel.gameObject.SetActive(true);
            panel.canvasGroup.interactable = true;
            panel.canvasGroup.blocksRaycasts = true;
        }

        float targetAlpha = show ? 1f : 0f;
        float startAlpha = panel.canvasGroup.alpha;

        float elapsed = 0f;
        float duration = 1f / defaultFadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            panel.canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        panel.canvasGroup.alpha = targetAlpha;

        if (!show)
        {
            panel.canvasGroup.interactable = false;
            panel.canvasGroup.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

        fadeCoroutines[panel.type] = null;
    }

    public void HideAll()
    {
        foreach (var kvp in panelDict)
        {
            HideImmediate(kvp.Value);
        }
        activeExclusiveUIs.Clear();
    }

    public void ShowAll()
    {
        foreach (var kvp in panelDict)
        {
            ShowImmediate(kvp.Value);
        }
    }

    #region Static Methods

    public static void ShowUI(UIType type, bool instant = true)
    {
        if (instance != null)
        {
            instance.Show(type, instant);
        }
        else
        {
            Debug.LogWarning("[UIManager] Instance not found. Make sure UIManager is in the scene.");
        }
    }

    public static void HideUI(UIType type, bool instant = true)
    {
        if (instance != null)
        {
            instance.Hide(type, instant);
        }
    }

    public static void ToggleUI(UIType type, bool instant = true)
    {
        if (instance != null)
        {
            instance.Toggle(type, instant);
        }
    }

    public static bool IsUIVisible(UIType type)
    {
        if (instance != null)
        {
            return instance.IsVisible(type);
        }
        return false;
    }

    public static void RegisterUI(UIType type, GameObject obj, bool useCanvasGroup = true, bool startHidden = true, bool isExclusive = false)
    {
        if (instance != null)
        {
            instance.RegisterPanel(type, obj, useCanvasGroup, startHidden, isExclusive);
        }
    }

    public static void UnregisterUI(UIType type)
    {
        if (instance != null)
        {
            instance.UnregisterPanel(type);
        }
    }

    public static bool IsUIBlocked(UIType type)
    {
        if (instance != null)
        {
            return instance.IsBlockedByExclusiveUI(type);
        }
        return false;
    }

    public static bool HasExclusiveUI()
    {
        if (instance != null)
        {
            return instance.HasActiveExclusiveUI();
        }
        return false;
    }

    #endregion
}
