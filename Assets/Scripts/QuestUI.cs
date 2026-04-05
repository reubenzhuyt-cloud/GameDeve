using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class QuestUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject questPanel;
    public Transform questListContainer;
    public GameObject questItemPrefab;
    public TextMeshProUGUI questDetailTitle;
    public TextMeshProUGUI questDetailDescription;
    public Transform objectiveListContainer;
    public GameObject objectiveItemPrefab;
    public Button toggleButton;
    
    [Header("Settings")]
    public bool showCompletedQuests = false;
    
    private List<GameObject> questItems = new List<GameObject>();
    private List<GameObject> objectiveItems = new List<GameObject>();
    private QuestData selectedQuest;
    
    private void Start()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(TogglePanel);
        
        if (QuestManager.instance != null)
        {
            QuestManager.instance.onQuestAccepted.AddListener(OnQuestAccepted);
            QuestManager.instance.onQuestCompleted.AddListener(OnQuestCompleted);
            QuestManager.instance.onQuestProgressChanged.AddListener(OnQuestProgressChanged);
        }
        
        RefreshQuestList();
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            TogglePanel();
        }
    }
    
    public void TogglePanel()
    {
        if (questPanel != null)
        {
            questPanel.SetActive(!questPanel.activeSelf);
            
            if (questPanel.activeSelf)
            {
                RefreshQuestList();
            }
        }
    }
    
    public void RefreshQuestList()
    {
        ClearQuestItems();
        
        if (QuestManager.instance == null) return;
        
        foreach (var activeQuest in QuestManager.instance.ActiveQuestList)
        {
            CreateQuestItem(activeQuest.questData);
        }
    }
    
    private void CreateQuestItem(QuestData quest)
    {
        if (questItemPrefab == null || questListContainer == null) return;
        
        GameObject item = Instantiate(questItemPrefab, questListContainer);
        questItems.Add(item);
        
        TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0)
        {
            texts[0].text = quest.questName;
        }
        
        if (texts.Length > 1)
        {
            texts[1].text = quest.questType.ToString();
        }
        
        Button button = item.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => SelectQuest(quest));
        }
    }
    
    public void SelectQuest(QuestData quest)
    {
        selectedQuest = quest;
        UpdateQuestDetail();
    }
    
    private void UpdateQuestDetail()
    {
        if (selectedQuest == null) return;
        
        if (questDetailTitle != null)
            questDetailTitle.text = selectedQuest.questName;
        
        if (questDetailDescription != null)
            questDetailDescription.text = selectedQuest.description;
        
        ClearObjectiveItems();
        
        foreach (var objective in selectedQuest.objectives)
        {
            CreateObjectiveItem(objective);
        }
    }
    
    private void CreateObjectiveItem(QuestObjective objective)
    {
        if (objectiveItemPrefab == null || objectiveListContainer == null) return;
        
        GameObject item = Instantiate(objectiveItemPrefab, objectiveListContainer);
        objectiveItems.Add(item);
        
        TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0)
        {
            string status = objective.IsComplete ? "<color=green>✓</color>" : "○";
            texts[0].text = $"{status} {objective.description}";
        }
        
        if (texts.Length > 1)
        {
            texts[1].text = $"{objective.currentAmount}/{objective.requiredAmount}";
        }
    }
    
    private void ClearQuestItems()
    {
        foreach (var item in questItems)
        {
            if (item != null) Destroy(item);
        }
        questItems.Clear();
    }
    
    private void ClearObjectiveItems()
    {
        foreach (var item in objectiveItems)
        {
            if (item != null) Destroy(item);
        }
        objectiveItems.Clear();
    }
    
    private void OnQuestAccepted(QuestData quest)
    {
        RefreshQuestList();
    }
    
    private void OnQuestCompleted(QuestData quest)
    {
        RefreshQuestList();
        
        if (selectedQuest == quest)
        {
            selectedQuest = null;
            if (questDetailTitle != null) questDetailTitle.text = "";
            if (questDetailDescription != null) questDetailDescription.text = "";
            ClearObjectiveItems();
        }
    }
    
    private void OnQuestProgressChanged(QuestData quest)
    {
        if (selectedQuest == quest)
        {
            UpdateQuestDetail();
        }
    }
    
    private void OnDestroy()
    {
        if (QuestManager.instance != null)
        {
            QuestManager.instance.onQuestAccepted.RemoveListener(OnQuestAccepted);
            QuestManager.instance.onQuestCompleted.RemoveListener(OnQuestCompleted);
            QuestManager.instance.onQuestProgressChanged.RemoveListener(OnQuestProgressChanged);
        }
    }
}
