using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestTrackerUI : MonoBehaviour
{
    public static QuestTrackerUI instance;
    
    [Header("UI References")]
    [SerializeField] private GameObject trackerPanel;
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private TextMeshProUGUI questTypeText;
    
    [Header("Settings")]
    [SerializeField] private int maxNameLength = 12;
    [SerializeField] private int maxObjectiveLength = 20;
    
    private ActiveQuest trackedQuest;
    
    public ActiveQuest TrackedQuest => trackedQuest;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }
    }
    
    private void Start()
    {
        FindTextComponentsIfNeeded();
        HideTracker();
        RegisterEvents();
    }
    
    private void FindTextComponentsIfNeeded()
    {
        if (trackerPanel == null)
        {
            trackerPanel = gameObject;
        }
        
        if (questNameText == null)
        {
            var found = trackerPanel.transform.Find("QuestNameText");
            if (found != null)
            {
                questNameText = found.GetComponent<TextMeshProUGUI>();
            }
        }
        
        if (objectiveText == null)
        {
            var found = trackerPanel.transform.Find("ObjectiveText");
            if (found != null)
            {
                objectiveText = found.GetComponent<TextMeshProUGUI>();
            }
        }
        
        if (questTypeText == null)
        {
            var found = trackerPanel.transform.Find("QuestTypeText");
            if (found != null)
            {
                questTypeText = found.GetComponent<TextMeshProUGUI>();
            }
        }
        
        SetupTextOverflow();
        
        Debug.Log($"[QuestTrackerUI] Text components - Name: {(questNameText != null ? "found" : "null")}, Objective: {(objectiveText != null ? "found" : "null")}, Type: {(questTypeText != null ? "found" : "null")}");
    }
    
    private void SetupTextOverflow()
    {
        if (questNameText != null)
        {
            questNameText.overflowMode = TextOverflowModes.Ellipsis;
            questNameText.enableWordWrapping = false;
        }
        
        if (objectiveText != null)
        {
            objectiveText.overflowMode = TextOverflowModes.Ellipsis;
            objectiveText.enableWordWrapping = false;
        }
        
        if (questTypeText != null)
        {
            questTypeText.overflowMode = TextOverflowModes.Ellipsis;
            questTypeText.enableWordWrapping = false;
        }
    }
    
    private void OnDestroy()
    {
        UnregisterEvents();
    }
    
    private void RegisterEvents()
    {
        if (DialogueSystem.instance != null)
        {
            DialogueSystem.instance.onDialogueStart.AddListener(OnDialogueStart);
            DialogueSystem.instance.onDialogueEnd.AddListener(OnDialogueEnd);
        }
    }
    
    private void UnregisterEvents()
    {
        if (DialogueSystem.instance != null)
        {
            DialogueSystem.instance.onDialogueStart.RemoveListener(OnDialogueStart);
            DialogueSystem.instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
    }
    
    private void OnDialogueStart(DialogueData data)
    {
        HideTracker();
    }
    
    private void OnDialogueEnd(DialogueData data)
    {
        UpdateTrackerDisplay();
    }
    
    public void OnQuestPanelOpened()
    {
        HideTracker();
    }
    
    public void OnQuestPanelClosed()
    {
        Debug.Log($"[QuestTrackerUI] OnQuestPanelClosed called, trackedQuest: {(trackedQuest != null ? trackedQuest.questData?.questName : "null")}");
        
        if (trackedQuest != null && trackedQuest.questData != null)
        {
            if (DialogueSystem.instance != null && DialogueSystem.instance.IsInDialogue())
            {
                Debug.Log("[QuestTrackerUI] OnQuestPanelClosed: In dialogue, not showing tracker");
                return;
            }
            
            UpdateTrackerTexts();
            
            if (trackerPanel != null)
            {
                trackerPanel.SetActive(true);
                Debug.Log("[QuestTrackerUI] Tracker shown directly from OnQuestPanelClosed");
            }
        }
    }
    
    public void SetTrackedQuest(ActiveQuest quest)
    {
        if (trackedQuest == quest)
        {
            ClearTrackedQuest();
            return;
        }
        
        trackedQuest = quest;
        Debug.Log($"[QuestTrackerUI] SetTrackedQuest: {quest?.questData?.questName}");
        UpdateTrackerDisplay();
        
        if (QuestPanelUI.instance != null)
        {
            QuestPanelUI.instance.RefreshQuestList();
        }
    }
    
    public void ClearTrackedQuest()
    {
        trackedQuest = null;
        HideTracker();
        
        if (QuestPanelUI.instance != null)
        {
            QuestPanelUI.instance.RefreshQuestList();
        }
    }
    
    public bool IsTracking(ActiveQuest quest)
    {
        return trackedQuest == quest;
    }
    
    private void UpdateTrackerDisplay()
    {
        Debug.Log($"[QuestTrackerUI] UpdateTrackerDisplay - trackedQuest: {(trackedQuest != null ? "not null" : "null")}, questData: {(trackedQuest?.questData != null ? "not null" : "null")}");
        
        if (trackedQuest == null || trackedQuest.questData == null)
        {
            HideTracker();
            return;
        }
        
        UpdateTrackerTexts();
        ShowTracker();
    }
    
    private void UpdateTrackerTexts()
    {
        if (trackedQuest == null || trackedQuest.questData == null)
        {
            Debug.LogWarning("[QuestTrackerUI] UpdateTrackerTexts: trackedQuest or questData is null");
            return;
        }
        
        QuestData data = trackedQuest.questData;
        
        Debug.Log($"[QuestTrackerUI] Updating texts - Name: {data.questName}, Type: {data.questType}");
        
        if (questNameText != null)
        {
            string displayName = data.questName;
            if (displayName.Length > maxNameLength)
            {
                displayName = displayName.Substring(0, maxNameLength) + "...";
            }
            questNameText.text = displayName;
            Debug.Log($"[QuestTrackerUI] questNameText set to: {displayName}");
        }
        else
        {
            Debug.LogWarning("[QuestTrackerUI] questNameText is null!");
        }
        
        if (objectiveText != null)
        {
            if (data.objectives != null && data.objectives.Count > 0)
            {
                var obj = data.objectives[0];
                string status = obj.IsComplete ? "[完成]" : "[进行中]";
                string objDesc = obj.description;
                if (objDesc.Length > maxObjectiveLength)
                {
                    objDesc = objDesc.Substring(0, maxObjectiveLength) + "...";
                }
                objectiveText.text = $"{status} {objDesc} ({obj.currentAmount}/{obj.requiredAmount})";
                Debug.Log($"[QuestTrackerUI] objectiveText set to: {objectiveText.text}");
            }
            else
            {
                objectiveText.text = "无目标";
            }
        }
        else
        {
            Debug.LogWarning("[QuestTrackerUI] objectiveText is null!");
        }
        
        if (questTypeText != null)
        {
            questTypeText.text = data.questType switch
            {
                QuestType.Main => "[主线]",
                QuestType.Side => "[支线]",
                QuestType.Hidden => "[隐藏]",
                _ => ""
            };
            Debug.Log($"[QuestTrackerUI] questTypeText set to: {questTypeText.text}");
        }
        else
        {
            Debug.LogWarning("[QuestTrackerUI] questTypeText is null!");
        }
    }
    
    public void ShowTracker()
    {
        Debug.Log($"[QuestTrackerUI] ShowTracker called - trackedQuest: {(trackedQuest != null ? "not null" : "null")}");
        
        if (trackedQuest == null)
        {
            Debug.Log("[QuestTrackerUI] ShowTracker: trackedQuest is null, returning");
            return;
        }
        
        if (DialogueSystem.instance != null && DialogueSystem.instance.IsInDialogue())
        {
            Debug.Log("[QuestTrackerUI] ShowTracker: In dialogue, returning");
            return;
        }
        
        if (QuestPanelUI.instance != null && QuestPanelUI.instance.IsOpen)
        {
            Debug.Log("[QuestTrackerUI] ShowTracker: QuestPanel is open, returning");
            return;
        }
        
        if (trackerPanel != null)
        {
            trackerPanel.SetActive(true);
            Debug.Log("[QuestTrackerUI] Tracker shown");
        }
    }
    
    public void HideTracker()
    {
        if (trackerPanel != null)
        {
            trackerPanel.SetActive(false);
        }
    }
    
    public void RefreshTracker()
    {
        if (trackedQuest != null)
        {
            UpdateTrackerTexts();
        }
    }
}
