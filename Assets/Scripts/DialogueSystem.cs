using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Maps <see cref="DialogueNodeData.actorId"/> to a portrait for dialogue UI.
/// Assign <see cref="portraitSprite"/>; optionally <see cref="portraitImage"/> for a dedicated slot (otherwise uses <see cref="DialogueSystem"/> default image).
/// </summary>
[System.Serializable]
public class DialogueActorPortraitEntry
{
    public int actorId;
    public Sprite portraitSprite;
    [Tooltip("Optional. If set, this Image is shown for this actor; if empty, uses DialogueSystem default portrait Image.")]
    public Image portraitImage;
}

[System.Serializable]
public class DialogueNodeData
{
    public int nodeId;
    public int actorId;
    public string dialogueText;
    public int nextNodeIndex = -1;
    public bool isChoiceNode = false;
    public List<DialogueChoiceData> choices = new();
    public string animationTrigger;
}

[System.Serializable]
public class DialogueChoiceData
{
    public string choiceText;
    public int targetNodeIndex;
    public List<DialogueCondition> conditions = new();
}

[System.Serializable]
public class DialogueCondition
{
    public string conditionKey;
    public string expectedValue;
}

[System.Serializable]
public class DialogueData
{
    public string DialogueName;
    public List<DialogueNodeData> nodes;
}

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem instance;
    private static readonly string[] UnifiedActorNames =
    {
        "孟忘",
        "孟婆",
        "无主之魂",
        "渔夫",
        "肉包子",
        "佃户",
        "许秋慈"
    };
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI actorNameUGUI;
    [SerializeField] private TextMeshProUGUI dialogueUGUI;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject choiceButtonPrefab;
    [Header("Actor portraits (立绘)")]
    [Tooltip("Default portrait Image when an entry does not assign its own Image. Keep disabled in scene; runtime sets sprite and toggles active.")]
    [SerializeField] private Image actorPortraitImage;
    [Tooltip("actorId → sprite. When a node uses that actorId, the sprite is shown while they speak.")]
    [SerializeField] private List<DialogueActorPortraitEntry> actorPortraitEntries = new();

    [Header("Dialogue Settings")]
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] public List<string> actors = new();
    [Header("Animation Settings")]
    [SerializeField] private List<Animator> actorAnimators = new List<Animator>();
    [SerializeField] private float animationTriggerDuration = 0.1f;
    [Header("Audio Settings")]
    [SerializeField] private bool enableDialogueAudio = true;
    [SerializeField] [Range(0f, 1f)] private float dialogueVolume = 1f;

    private DialogueData currentDialogue;
    private DialogueNodeData currentNode;
    private string currentDialogueFileName;
    private bool isInDialogue = false;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    private Coroutine animationCoroutine;
    private ConditionManager conditionManager;
    private AudioSource dialogueAudioSource;
    private Dictionary<int, DialogueActorPortraitEntry> actorPortraitLookup = new();

    public UnityEvent<DialogueData> onDialogueEnd = new();
    public UnityEvent<DialogueData> onDialogueStart = new();
    public UnityEvent<DialogueData> onDialogueChoice = new();
    public UnityEvent<DialogueData> onDialogueChoiceEnd = new();
    public UnityEvent<DialogueNodeData> onDialogueNodeChange = new();
    public UnityEvent<int, string> onChoiceSelected = new();
    
    private int lastChoiceIndex = -1;
    private string lastChoiceText = "";

    private void Awake()
    {
        // 新场景的 DialogueSystem 会替换旧实例；若新场景未配置 actorPortraitEntries，则继承上一场景已配置的 Sprite，避免第二场景对白立绘全空。
        if (instance != null && instance != this)
        {
            var old = instance;
            if (actorPortraitEntries == null || actorPortraitEntries.Count == 0)
            {
                if (old.actorPortraitEntries != null && old.actorPortraitEntries.Count > 0)
                {
                    actorPortraitEntries = new List<DialogueActorPortraitEntry>();
                    foreach (var e in old.actorPortraitEntries)
                    {
                        if (e == null || e.portraitSprite == null)
                            continue;
                        actorPortraitEntries.Add(new DialogueActorPortraitEntry
                        {
                            actorId = e.actorId,
                            portraitSprite = e.portraitSprite,
                            portraitImage = null
                        });
                    }
                }
            }

            Destroy(old.gameObject);
        }

        instance = this;

        conditionManager = FindFirstObjectByType<ConditionManager>();
        if (conditionManager == null)
        {
            conditionManager = gameObject.AddComponent<ConditionManager>();
        }

        ResolveDialogueUiReferencesIfNeeded();
        if (choicePanel != null)
            choicePanel.GetComponent<RectTransform>().sizeDelta /= 2f;
        else
            Debug.LogWarning("[DialogueSystem] choicePanel is null after resolve — assign ChoicePanel or use Canvas.prefab layout (ChoicePanel).");

        dialogueAudioSource = gameObject.AddComponent<AudioSource>();
        dialogueAudioSource.playOnAwake = false;
        dialogueAudioSource.loop = false;
        dialogueAudioSource.volume = dialogueVolume;
        ApplyUnifiedActorMapping();

        RebuildActorPortraitLookup();
        HideAllPortraitImages();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildActorPortraitLookup();
    }
#endif

    private void ApplyUnifiedActorMapping()
    {
        actors = new List<string>(UnifiedActorNames);
    }

    private static string GetUnifiedActorName(int actorId)
    {
        if (actorId < 0 || actorId >= UnifiedActorNames.Length)
            return "Unknown Actor";
        return UnifiedActorNames[actorId];
    }

    private void RebuildActorPortraitLookup()
    {
        actorPortraitLookup.Clear();
        foreach (var entry in actorPortraitEntries)
        {
            if (entry == null || entry.portraitSprite == null)
                continue;
            actorPortraitLookup[entry.actorId] = entry;
        }
    }

    private void HideAllPortraitImages()
    {
        if (actorPortraitImage != null)
            actorPortraitImage.gameObject.SetActive(false);
        foreach (var entry in actorPortraitEntries)
        {
            if (entry != null && entry.portraitImage != null)
                entry.portraitImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// When only the Canvas root is assigned (or fields are left empty on a shared prefab), bind to
    /// DialoguePanel / ActorName / DialogueText / ChoicePanel under the shared Canvas prefab hierarchy.
    /// </summary>
    private void ResolveDialogueUiReferencesIfNeeded()
    {
        if (dialoguePanel == null)
            return;

        // Common mistake: reference is the whole Canvas — use the DialoguePanel child for show/hide.
        if (dialoguePanel.GetComponent<Canvas>() != null)
        {
            Transform dp = dialoguePanel.transform.Find("DialoguePanel");
            if (dp != null)
                dialoguePanel = dp.gameObject;
        }

        Transform searchRoot = dialoguePanel.transform;

        if (actorNameUGUI == null)
            actorNameUGUI = FindTmpByHierarchyName(searchRoot, "ActorName");
        if (dialogueUGUI == null)
            dialogueUGUI = FindTmpByHierarchyName(searchRoot, "DialogueText");
        if (choicePanel == null)
            choicePanel = FindChildGameObjectByName(searchRoot, "ChoicePanel");

        if (actorPortraitImage == null)
        {
            var portraitGo = FindChildGameObjectByName(searchRoot, "ActorPortrait");
            if (portraitGo != null)
                actorPortraitImage = portraitGo.GetComponent<Image>();
        }
    }

    private static TextMeshProUGUI FindTmpByHierarchyName(Transform root, string objectName)
    {
        if (root == null)
            return null;
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == objectName)
                return tmp;
        }
        return null;
    }

    private static GameObject FindChildGameObjectByName(Transform root, string objectName)
    {
        if (root == null)
            return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject.name == objectName)
                return t.gameObject;
        }
        return null;
    }

    private void Start()
    {
        EnsureUIManagerPanelsRegistered();
    }

    /// <summary>
    /// UIManager may not exist yet in Awake; also call before opening dialogue so registration always runs.
    /// </summary>
    private void EnsureUIManagerPanelsRegistered()
    {
        if (UIManager.instance == null)
            return;
        if (dialoguePanel != null)
            UIManager.instance.RegisterPanel(UIType.DialoguePanel, dialoguePanel, true, true);
        if (choicePanel != null)
            UIManager.instance.RegisterPanel(UIType.ChoicePanel, choicePanel, false, true);
    }

    /// <summary>
    /// <see cref="UIManager.Show(UIType)"/> 可能因未注册、互斥规则直接 return；根 Canvas 上 <see cref="CanvasGroup.alpha"/> 为 0 时子面板全透明。
    /// </summary>
    private void EnsureDialoguePanelShown()
    {
        if (dialoguePanel == null)
            return;

        if (UIManager.instance != null)
            UIManager.instance.Show(UIType.DialoguePanel);
        else
            dialoguePanel.SetActive(true);

        UIManager.EnsureCanvasRootVisible(dialoguePanel);
    }

    public void StartDialogue(string dialogueFileName)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>($"Dialogue/{dialogueFileName}");
        if (jsonFile == null)
        {
            Debug.LogError($"Dialogue file {dialogueFileName} not found in Resources/Dialogue folder");
            return;
        }
        currentDialogueFileName = dialogueFileName;
        StartDialogueFromJSON(jsonFile.text);
    }

    public void StartDialogueFromJSON(string jsonText)
    {
        DialogueData dialogue = JsonUtility.FromJson<DialogueData>(jsonText);
        if (dialogue == null)
        {
            Debug.LogError($"Dialogue JSON cannot be deserialized");
            return;
        }
        if (dialogue.nodes.Count == 0)
        {
            Debug.LogError($"Dialogue nodes list is empty");
            return;
        }

        ResolveDialogueUiReferencesIfNeeded();
        if (dialoguePanel == null || dialogueUGUI == null)
        {
            Debug.LogError("[DialogueSystem] Missing UI: assign Dialogue Panel (Canvas or DialoguePanel) on DialogueSystem so DialogueText / ActorName can be found.");
            return;
        }

        EnsureUIManagerPanelsRegistered();

        currentDialogue = dialogue;
        currentNode = dialogue.nodes[0];
        isInDialogue = true;

        EnsureDialoguePanelShown();

        onDialogueStart.Invoke(currentDialogue);
        ShowCurrentDialogueNode();
    }

    private void ShowCurrentDialogueNode()
    {
        if (currentNode == null)
        {
            Debug.LogError($"Current dialogue node is null");
            return;
        }
        ClearChoiceButtons();
        if (dialogueUGUI != null)
            dialogueUGUI.text = "";

        StopCurrentAnimation();
        StopDialogueAudio();

        onDialogueNodeChange?.Invoke(currentNode);

        if (!string.IsNullOrEmpty(currentNode.animationTrigger))
        {
            //PlayActorAnimation(currentNode.actorId, currentNode.animationTrigger);
        }

        PlayDialogueAudio(currentNode.nodeId);

        if (currentNode.isChoiceNode)
        {
            ShowChoiceNode();
        }
        else
        {
            ShowNormalNode();
        }
    }

    /// <summary>
    /// Shows the portrait for <paramref name="actorId"/> when a mapped sprite exists; hides all portrait slots otherwise.
    /// </summary>
    private void ApplyActorPortraitForLine(int actorId, bool visible)
    {
        if (!visible)
        {
            HideAllPortraitImages();
            return;
        }

        if (dialoguePanel != null && actorPortraitImage == null)
            ResolveDialogueUiReferencesIfNeeded();

        if (!actorPortraitLookup.TryGetValue(actorId, out DialogueActorPortraitEntry entry) || entry == null)
        {
            Sprite res = Resources.Load<Sprite>($"Dialogue/Portraits/Actor{actorId}");
            if (res != null && actorPortraitImage != null)
            {
                HideAllPortraitImages();
                actorPortraitImage.sprite = res;
                actorPortraitImage.preserveAspect = true;
                actorPortraitImage.gameObject.SetActive(true);
                return;
            }

            HideAllPortraitImages();
            return;
        }

        Image target = entry.portraitImage != null ? entry.portraitImage : actorPortraitImage;
        if (target == null)
        {
            HideAllPortraitImages();
            return;
        }

        HideAllPortraitImages();
        target.sprite = entry.portraitSprite;
        target.preserveAspect = true;
        target.gameObject.SetActive(true);
    }

    private void ShowNormalNode()
    {
        if (dialogueUGUI == null)
        {
            Debug.LogError("[DialogueSystem] dialogueUGUI is null — cannot display line.");
            return;
        }

        if (actorNameUGUI != null)
        {
            actorNameUGUI.text = GetUnifiedActorName(currentNode.actorId);
        }

        ApplyActorPortraitForLine(currentNode.actorId, true);
        if (choicePanel != null)
        {
            if (UIManager.instance != null)
                UIManager.instance.Hide(UIType.ChoicePanel);
            else
                choicePanel.SetActive(false);
        }
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        typingCoroutine = StartCoroutine(TypeText(currentNode.dialogueText));
    }

    private void ShowChoiceNode()
    {
        ApplyActorPortraitForLine(0, false);

        if (actorNameUGUI != null)
        {
            actorNameUGUI.text = "Choice";
        }
        if (dialogueUGUI != null)
        {
            dialogueUGUI.text = "";
        }
        if (choicePanel == null || choiceButtonPrefab == null)
        {
            Debug.LogError($"Choice panel or buttons prefab are null");
            return;
        }

        if (UIManager.instance != null)
            UIManager.instance.Show(UIType.ChoicePanel);
        else
            choicePanel.SetActive(true);

        RectTransform buttonRect = choiceButtonPrefab.GetComponent<RectTransform>();
        float buttonHeight = buttonRect.rect.height;
        float spacing = 80f;

        List<DialogueChoiceData> availableChoices = new List<DialogueChoiceData>();
        foreach (var choice in currentNode.choices)
        {
            bool conditionsMet = true;
            foreach (var condition in choice.conditions)
            {
                if (!conditionManager.CheckCondition(condition.conditionKey, condition.expectedValue))
                {
                    conditionsMet = false;
                    break;
                }
            }
            if (conditionsMet)
            {
                availableChoices.Add(choice);
            }
        }

        for (int i = 0; i < availableChoices.Count; i++)
        {
            DialogueChoiceData choice = availableChoices[i];
            int choiceIndex = i;

            GameObject choiceButton = Instantiate(choiceButtonPrefab, choicePanel.transform);
            RectTransform rectTransform = choiceButton.GetComponent<RectTransform>();

            float yPos = i * (buttonHeight + spacing) - 80f;
            rectTransform.anchoredPosition = new Vector2(0f, yPos);

            Button button = choiceButton.GetComponent<Button>();
            TextMeshProUGUI buttonTextUGUI = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonTextUGUI != null)
            {
                buttonTextUGUI.text = choice.choiceText;
            }
            int targetIndex = choice.targetNodeIndex;
            string choiceText = choice.choiceText;
            button.onClick.AddListener(() => OnChoiceSelected(choiceIndex, choiceText, targetIndex));
        }

        RectTransform panelRect = choicePanel.GetComponent<RectTransform>();
        float totalHeight = availableChoices.Count * buttonHeight +
                           (availableChoices.Count + 1) * spacing;
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, totalHeight);
    }

    private void ClearChoiceButtons()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            isTyping = false;
        }
        if (choicePanel == null)
        {
            return;
        }
        foreach (Transform child in choicePanel.transform)
        {
            Destroy(child.gameObject);
        }
        
        if (UIManager.instance != null)
            UIManager.instance.Hide(UIType.ChoicePanel);
        else
            choicePanel.SetActive(false);
    }

    private IEnumerator TypeText(string text)
    {
        if (dialogueUGUI == null)
            yield break;

        isTyping = true;
        dialogueUGUI.text = "";

        foreach (char c in text)
        {
            dialogueUGUI.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    public void ContinueDialogue()
    {
        if (!isInDialogue || currentNode == null || dialogueUGUI == null) return;

        if (isTyping)
        {
            if (typingCoroutine != null)
                StopCoroutine(typingCoroutine);

            dialogueUGUI.text = currentNode.dialogueText;
            isTyping = false;
        }
        else if (!currentNode.isChoiceNode)
        {
            GoToNextNode(currentNode.nextNodeIndex);
        }
    }

    private void OnChoiceSelected(int choiceIndex, string choiceText, int targetNodeIndex)
    {
        lastChoiceIndex = choiceIndex;
        lastChoiceText = choiceText;
        onChoiceSelected?.Invoke(choiceIndex, choiceText);
        GoToNextNode(targetNodeIndex);
    }

    private void GoToNextNode(int nodeIndex)
    {
        StopCurrentAnimation();
        StopDialogueAudio();

        if (nodeIndex < 0)
        {
            EndDialogue();
            return;
        }

        if (currentDialogue == null || nodeIndex >= currentDialogue.nodes.Count)
        {
            Debug.LogError($"Invalid node index: {nodeIndex}");
            EndDialogue();
            return;
        }

        currentNode = currentDialogue.nodes[nodeIndex];
        ShowCurrentDialogueNode();
    }

    public void EndDialogue()
    {
        isInDialogue = false;

        StopCurrentAnimation();
        StopDialogueAudio();
        ApplyActorPortraitForLine(0, false);

        if (dialoguePanel != null)
        {
            if (UIManager.instance != null)
                UIManager.instance.Hide(UIType.DialoguePanel);
            else
                dialoguePanel.SetActive(false);
        }

        ClearChoiceButtons();

        onDialogueEnd?.Invoke(currentDialogue);

        currentDialogue = null;
        currentNode = null;
        currentDialogueFileName = null;
    }

    public bool IsInDialogue()
    {
        return isInDialogue;
    }
    
    public int GetLastChoiceIndex()
    {
        return lastChoiceIndex;
    }
    
    public string GetLastChoiceText()
    {
        return lastChoiceText;
    }
    
    public void ClearLastChoice()
    {
        lastChoiceIndex = -1;
        lastChoiceText = "";
    }

    private void PlayActorAnimation(int actorId, string triggerName)
    {
        if (actorId < 0 || actorId >= actorAnimators.Count || actorAnimators[actorId] == null)
        {
            Debug.LogError($"未找到actorId: {actorId} 对应的Animator");
            return;
        }

        StopCurrentAnimation();

        animationCoroutine = StartCoroutine(PlayOneShotAnimation(actorId, triggerName));
    }

    private IEnumerator PlayOneShotAnimation(int actorId, string triggerName)
    {
        Animator animator = actorAnimators[actorId];

        animator.SetBool(triggerName, true);
        yield return null;

        animator.SetBool(triggerName, false);

        yield return new WaitForSeconds(animationTriggerDuration);

        animationCoroutine = null;
    }

    private void StopCurrentAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
    }

    /// <summary>
    /// Resources/Dialogue 里可用子路径（如 <c>人物对话文件夹/孟忘和渔夫</c>），语音常放在
    /// <c>Resources/Audio/Dialogue/</c> 根目录并用「仅文件名」命名（<c>孟忘和渔夫_node0</c>）。
    /// 先按完整路径找，再按最后一级文件名找。
    /// </summary>
    private static string GetDialogueResourceBaseName(string dialoguePath)
    {
        if (string.IsNullOrEmpty(dialoguePath))
            return dialoguePath;
        int i = dialoguePath.LastIndexOf('/');
        return i >= 0 ? dialoguePath.Substring(i + 1) : dialoguePath;
    }

    private void PlayDialogueAudio(int nodeId)
    {
        if (!enableDialogueAudio || string.IsNullOrEmpty(currentDialogueFileName))
            return;

        string suffix = $"_node{nodeId}";
        string fullKey = $"{currentDialogueFileName}{suffix}";
        string baseKey = $"{GetDialogueResourceBaseName(currentDialogueFileName)}{suffix}";

        AudioClip audioClip = Resources.Load<AudioClip>($"Audio/Dialogue/{fullKey}");
        if (audioClip == null && baseKey != fullKey)
            audioClip = Resources.Load<AudioClip>($"Audio/Dialogue/{baseKey}");

        if (audioClip != null)
        {
            dialogueAudioSource.clip = audioClip;
            dialogueAudioSource.Play();
        }
        else
        {
            Debug.LogWarning(
                $"[DialogueSystem] Dialogue audio not found. Tried Resources paths: Audio/Dialogue/{fullKey}, Audio/Dialogue/{baseKey}");
        }
    }

    private void StopDialogueAudio()
    {
        if (dialogueAudioSource != null && dialogueAudioSource.isPlaying)
        {
            dialogueAudioSource.Stop();
            dialogueAudioSource.clip = null;
        }
    }

    public void SetDialogueVolume(float volume)
    {
        dialogueVolume = Mathf.Clamp01(volume);
        if (dialogueAudioSource != null)
        {
            dialogueAudioSource.volume = dialogueVolume;
        }
    }

    public void SetDialogueAudioEnabled(bool enabled)
    {
        enableDialogueAudio = enabled;
        if (!enabled)
        {
            StopDialogueAudio();
        }
    }

    private void Update()
    {
        if (isInDialogue)
        {
            if (Input.GetMouseButtonDown(0))
            {
                ContinueDialogue();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EndDialogue();
            }
        }
    }
}