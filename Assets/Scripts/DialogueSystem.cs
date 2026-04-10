using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

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
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI actorNameUGUI;
    [SerializeField] private TextMeshProUGUI dialogueUGUI;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject choiceButtonPrefab;
    [Header("Dialogue Settings")]
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] public List<string> actors;
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

    public UnityEvent<DialogueData> onDialogueEnd = new();
    public UnityEvent<DialogueData> onDialogueStart = new();
    public UnityEvent<DialogueData> onDialogueChoice = new();
    public UnityEvent<DialogueData> onDialogueChoiceEnd = new();
    public UnityEvent<DialogueNodeData> onDialogueNodeChange = new();
    public UnityEvent<int, string> onChoiceSelected = new();
    
    private int lastChoiceIndex = -1;
    private string lastChoiceText = "";
    private DialogueChoiceData lastSelectedChoice = null;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        conditionManager = FindFirstObjectByType<ConditionManager>();
        if (conditionManager == null)
        {
            conditionManager = gameObject.AddComponent<ConditionManager>();
        }
        choicePanel.GetComponent<RectTransform>().sizeDelta /= 2f;
        
        dialogueAudioSource = gameObject.AddComponent<AudioSource>();
        dialogueAudioSource.playOnAwake = false;
        dialogueAudioSource.loop = false;
        dialogueAudioSource.volume = dialogueVolume;
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

        EnsureUIManagerPanelsRegistered();

        currentDialogue = dialogue;
        currentNode = dialogue.nodes[0];
        isInDialogue = true;

        if (UIManager.instance != null)
            UIManager.instance.Show(UIType.DialoguePanel);
        else if (dialoguePanel != null)
            dialoguePanel.SetActive(true);
            
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

    private void ShowNormalNode()
    {
        if (actorNameUGUI != null)
        {
            if (currentNode.actorId >= actors.Count)
            {
                actorNameUGUI.text = "Unknown Actor";
            }
            else
            {
                actorNameUGUI.text = actors[currentNode.actorId];
            }
        }
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
        if (!isInDialogue || currentNode == null) return;

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

    private void PlayDialogueAudio(int nodeId)
    {
        if (!enableDialogueAudio || string.IsNullOrEmpty(currentDialogueFileName))
            return;

        string audioFileName = $"{currentDialogueFileName}_node{nodeId}";
        AudioClip audioClip = Resources.Load<AudioClip>($"Audio/Dialogue/{audioFileName}");
        
        if (audioClip != null)
        {
            dialogueAudioSource.clip = audioClip;
            dialogueAudioSource.Play();
        }
        else
        {
            Debug.LogWarning($"Dialogue audio not found: {audioFileName}");
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