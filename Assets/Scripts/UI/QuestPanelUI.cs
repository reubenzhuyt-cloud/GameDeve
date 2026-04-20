using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class QuestPanelUI : MonoBehaviour
{
    public static QuestPanelUI instance;
    
    [Header("Panel References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform questListContainer;
    [SerializeField] private GameObject questItemPrefab;
    
    [Header("Detail References")]
    [SerializeField] private TextMeshProUGUI questTitleText;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private TextMeshProUGUI questObjectivesText;
    [SerializeField] private TextMeshProUGUI questRewardText;
    
    [Header("Settings")]
    [SerializeField] private int maxDescriptionLength = 100;
    
    private List<GameObject> questItems = new List<GameObject>();
    private ActiveQuest selectedQuest;
    private bool isOpen = false;
    
    public bool IsOpen => isOpen;
    public ActiveQuest SelectedQuest => selectedQuest;
    
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
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // New CanvasGroup defaults to alpha 1 — hide immediately so bootstrap does not flash one frame.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
        SetupTextOverflow();
    }
    
    private void SetupTextOverflow()
    {
        if (questTitleText != null)
        {
            questTitleText.overflowMode = TextOverflowModes.Ellipsis;
            questTitleText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (questDescriptionText != null)
        {
            questDescriptionText.overflowMode = TextOverflowModes.Ellipsis;
            questDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (questObjectivesText != null)
        {
            questObjectivesText.overflowMode = TextOverflowModes.Ellipsis;
            questObjectivesText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (questRewardText != null)
        {
            questRewardText.overflowMode = TextOverflowModes.Ellipsis;
            questRewardText.textWrappingMode = TextWrappingModes.Normal;
        }
    }
    
    private void Start()
    {
        ClosePanel();
        RegisterQuestManagerEvents();
    }
    
    private void OnEnable()
    {
        RegisterQuestManagerEvents();
    }
    
    private void OnDisable()
    {
        UnregisterQuestManagerEvents();
    }
    
    private void RegisterQuestManagerEvents()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.onQuestAccepted.RemoveListener(OnQuestAccepted);
            QuestManager.instance.onQuestCompleted.RemoveListener(OnQuestCompleted);
            QuestManager.instance.onQuestProgressChanged.RemoveListener(OnQuestProgressChanged);
            
            QuestManager.instance.onQuestAccepted.AddListener(OnQuestAccepted);
            QuestManager.instance.onQuestCompleted.AddListener(OnQuestCompleted);
            QuestManager.instance.onQuestProgressChanged.AddListener(OnQuestProgressChanged);
        }
    }
    
    private void UnregisterQuestManagerEvents()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.onQuestAccepted.RemoveListener(OnQuestAccepted);
            QuestManager.instance.onQuestCompleted.RemoveListener(OnQuestCompleted);
            QuestManager.instance.onQuestProgressChanged.RemoveListener(OnQuestProgressChanged);
        }
    }
    
    private void OnDestroy()
    {
        UnregisterQuestManagerEvents();
    }
    
    public void TogglePanel()
    {
        if (isOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }
    
    public void OpenPanel()
    {
        if (UIManager.IsUIBlocked(UIType.QuestPanel))
        {
            return;
        }

        if (DialogueSystem.instance != null && DialogueSystem.instance.IsInDialogue())
            return;
        
        isOpen = true;

        if (UIManager.instance != null && UIManager.instance.IsPanelRegistered(UIType.QuestPanel))
            UIManager.instance.Show(UIType.QuestPanel, true);
        else
        {
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        RefreshQuestList();
        Time.timeScale = 0f;
        
        if (QuestTrackerUI.instance != null)
        {
            QuestTrackerUI.instance.OnQuestPanelOpened();
        }
    }
    
    public void ClosePanel()
    {
        isOpen = false;
        Time.timeScale = 1f;
        
        if (QuestTrackerUI.instance != null)
        {
            QuestTrackerUI.instance.OnQuestPanelClosed();
        }

        if (UIManager.instance != null && UIManager.instance.IsPanelRegistered(UIType.QuestPanel))
            UIManager.instance.Hide(UIType.QuestPanel, true);
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
    
    public void RefreshQuestList()
    {
        ClearQuestList();
        
        if (QuestManager.instance == null) return;
        
        foreach (var activeQuest in QuestManager.instance.ActiveQuestList)
        {
            CreateQuestItem(activeQuest);
        }
        
        if (questItems.Count > 0 && selectedQuest == null)
        {
            var firstItem = questItems[0].GetComponent<QuestItemUI>();
            if (firstItem != null)
            {
                SelectQuest(firstItem.QuestData);
            }
        }
    }
    
    private void ClearQuestList()
    {
        foreach (var item in questItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        questItems.Clear();
    }
    
    private void CreateQuestItem(ActiveQuest activeQuest)
    {
        if (questListContainer == null || questItemPrefab == null) return;
        
        GameObject item = Instantiate(questItemPrefab, questListContainer);
        QuestItemUI itemUI = item.GetComponent<QuestItemUI>();
        
        if (itemUI != null)
        {
            itemUI.Setup(activeQuest, this);
        }
        
        questItems.Add(item);
    }
    
    public void SelectQuest(ActiveQuest quest)
    {
        selectedQuest = quest;
        UpdateQuestDetail();
        
        foreach (var item in questItems)
        {
            QuestItemUI itemUI = item.GetComponent<QuestItemUI>();
            if (itemUI != null)
            {
                itemUI.SetSelected(itemUI.QuestData == quest);
            }
        }
    }
    
    private void UpdateQuestDetail()
    {
        if (selectedQuest == null || selectedQuest.questData == null)
        {
            ClearDetail();
            return;
        }
        
        QuestData data = selectedQuest.questData;
        
        if (questTitleText != null)
        {
            questTitleText.text = data.questName;
        }
        
        if (questDescriptionText != null)
        {
            string desc = data.description;
            if (desc.Length > maxDescriptionLength)
            {
                desc = desc.Substring(0, maxDescriptionLength) + "...";
            }
            questDescriptionText.text = desc;
        }
        
        if (questObjectivesText != null)
        {
            string objectivesStr = "";
            foreach (var objective in data.objectives)
            {
                string status = objective.IsComplete ? "[完成]" : "[进行中]";
                objectivesStr += $"{status} {objective.description} ({objective.currentAmount}/{objective.requiredAmount})\n";
            }
            questObjectivesText.text = objectivesStr;
        }
        
        if (questRewardText != null && data.reward != null)
        {
            string rewardStr = "";
            if (data.reward.experiencePoints > 0)
            {
                rewardStr += $"经验值: {data.reward.experiencePoints}\n";
            }
            if (data.reward.underworldReputation != 0)
            {
                rewardStr += $"声望: {data.reward.underworldReputation}\n";
            }
            questRewardText.text = rewardStr;
        }
    }
    
    private void ClearDetail()
    {
        if (questTitleText != null) questTitleText.text = "";
        if (questDescriptionText != null) questDescriptionText.text = "";
        if (questObjectivesText != null) questObjectivesText.text = "";
        if (questRewardText != null) questRewardText.text = "";
    }
    
    private void OnQuestAccepted(QuestData quest)
    {
        if (isOpen)
        {
            RefreshQuestList();
        }
    }
    
    private void OnQuestCompleted(QuestData quest)
    {
        if (selectedQuest != null && selectedQuest.questData == quest)
        {
            selectedQuest = null;
        }
        
        if (isOpen)
        {
            RefreshQuestList();
        }
    }
    
    private void OnQuestProgressChanged(QuestData quest)
    {
        if (isOpen && selectedQuest != null && selectedQuest.questData == quest)
        {
            UpdateQuestDetail();
        }
    }
}
