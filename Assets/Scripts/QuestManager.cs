using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class ActiveQuest
{
    public QuestData questData;
    public QuestState state;
    public int startDay;
    public int startHour;
    public int startMinute;
    public float elapsedTime;
    
    public ActiveQuest(QuestData data)
    {
        questData = data;
        state = QuestState.InProgress;
        
        if (TimeManager.instance != null)
        {
            startDay = TimeManager.instance.CurrentDay;
            startHour = TimeManager.instance.CurrentHour;
            startMinute = TimeManager.instance.CurrentMinute;
        }
        else
        {
            startDay = 1;
            startHour = 0;
            startMinute = 0;
        }
        
        elapsedTime = 0f;
    }
    
    public bool IsTimeLimited => questData.timeLimitMinutes > 0;
    
    public float RemainingTime
    {
        get
        {
            if (!IsTimeLimited) return -1f;
            float totalLimit = questData.timeLimitMinutes * 60f;
            return Mathf.Max(0f, totalLimit - elapsedTime);
        }
    }
    
    public void UpdateElapsedTime(float deltaTime)
    {
        elapsedTime += deltaTime;
    }
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager instance;
    
    [Header("Quest Database")]
    public List<QuestData> allQuests = new List<QuestData>();
    
    [Header("Active Quests")]
    public int maxActiveQuests = 10;
    
    private Dictionary<string, ActiveQuest> activeQuests = new Dictionary<string, ActiveQuest>();
    private Dictionary<string, QuestState> completedQuests = new Dictionary<string, QuestState>();
    private Dictionary<string, int> questCompletionCount = new Dictionary<string, int>();
    
    public UnityEvent<QuestData> onQuestAccepted;
    public UnityEvent<QuestData> onQuestCompleted;
    public UnityEvent<QuestData> onQuestFailed;
    public UnityEvent<QuestData, QuestObjective> onObjectiveUpdated;
    public UnityEvent<QuestData, QuestObjective> onObjectiveCompleted;
    public UnityEvent<QuestData> onQuestProgressChanged;
    
    public int ActiveQuestCount => activeQuests.Count;
    public IEnumerable<ActiveQuest> ActiveQuestList => activeQuests.Values;
    
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
    }
    
    private void Update()
    {
        UpdateTimeLimitedQuests();
    }
    
    private void UpdateTimeLimitedQuests()
    {
        List<string> expiredQuests = new List<string>();
        
        foreach (var kvp in activeQuests)
        {
            ActiveQuest activeQuest = kvp.Value;
            activeQuest.UpdateElapsedTime(Time.deltaTime);
            
            if (activeQuest.IsTimeLimited && activeQuest.RemainingTime <= 0)
            {
                expiredQuests.Add(kvp.Key);
            }
        }
        
        foreach (string questId in expiredQuests)
        {
            FailQuest(questId);
        }
    }
    
    public bool CanAcceptQuest(string questId)
    {
        if (activeQuests.ContainsKey(questId)) return false;
        if (completedQuests.ContainsKey(questId)) return false;
        if (activeQuests.Count >= maxActiveQuests) return false;
        
        QuestData quest = GetQuestData(questId);
        if (quest == null) return true;
        
        foreach (var prereq in quest.prerequisites)
        {
            QuestState prereqState;
            if (!completedQuests.TryGetValue(prereq.prerequisiteQuestId, out prereqState))
            {
                prereqState = QuestState.NotStarted;
            }
            
            if (prereqState != prereq.requiredState)
            {
                return false;
            }
        }
        
        return true;
    }
    
    public bool AcceptQuest(string questId)
    {
        if (!CanAcceptQuest(questId)) return false;
        
        QuestData quest = GetQuestData(questId);
        if (quest == null)
        {
            quest = CreateRuntimeQuest(questId);
        }
        
        if (quest == null) return false;
        
        quest.ResetAllObjectives();
        
        ActiveQuest activeQuest = new ActiveQuest(quest);
        activeQuests.Add(questId, activeQuest);
        
        onQuestAccepted?.Invoke(quest);
        
        if (!string.IsNullOrEmpty(quest.startDialogueFile) && DialogueSystem.instance != null)
        {
            DialogueSystem.instance.StartDialogue(quest.startDialogueFile);
        }
        
        Debug.Log($"[QuestManager] Accepted quest: {questId}");
        return true;
    }
    
    public bool CompleteQuest(string questId)
    {
        if (!activeQuests.ContainsKey(questId)) return false;
        
        ActiveQuest activeQuest = activeQuests[questId];
        
        if (!activeQuest.questData.AreAllRequiredObjectivesComplete()) return false;
        
        activeQuest.state = QuestState.Completed;
        
        if (completedQuests.ContainsKey(questId))
        {
            completedQuests[questId] = QuestState.Completed;
        }
        else
        {
            completedQuests.Add(questId, QuestState.Completed);
        }
        
        if (questCompletionCount.ContainsKey(questId))
        {
            questCompletionCount[questId]++;
        }
        else
        {
            questCompletionCount[questId] = 1;
        }
        
        GrantRewards(activeQuest.questData.reward);
        
        onQuestCompleted?.Invoke(activeQuest.questData);
        
        if (!string.IsNullOrEmpty(activeQuest.questData.completionDialogueFile) && DialogueSystem.instance != null)
        {
            DialogueSystem.instance.StartDialogue(activeQuest.questData.completionDialogueFile);
        }
        
        activeQuests.Remove(questId);
        
        return true;
    }
    
    public void FailQuest(string questId)
    {
        if (!activeQuests.ContainsKey(questId)) return;
        
        ActiveQuest activeQuest = activeQuests[questId];
        activeQuest.state = QuestState.Failed;
        
        if (completedQuests.ContainsKey(questId))
        {
            completedQuests[questId] = QuestState.Failed;
        }
        else
        {
            completedQuests.Add(questId, QuestState.Failed);
        }
        
        onQuestFailed?.Invoke(activeQuest.questData);
        
        activeQuests.Remove(questId);
    }
    
    public void AbandonQuest(string questId)
    {
        if (activeQuests.ContainsKey(questId))
        {
            activeQuests.Remove(questId);
        }
    }
    
    public void UpdateObjective(string questId, string objectiveId, int amount = 1)
    {
        if (!activeQuests.ContainsKey(questId)) return;
        
        ActiveQuest activeQuest = activeQuests[questId];
        QuestObjective objective = activeQuest.questData.GetObjective(objectiveId);
        
        if (objective == null || objective.IsComplete) return;
        
        objective.AddProgress(amount);
        
        onObjectiveUpdated?.Invoke(activeQuest.questData, objective);
        onQuestProgressChanged?.Invoke(activeQuest.questData);
        
        if (objective.IsComplete)
        {
            onObjectiveCompleted?.Invoke(activeQuest.questData, objective);
            
            if (activeQuest.questData.AreAllRequiredObjectivesComplete())
            {
                CompleteQuest(questId);
            }
        }
    }
    
    public void UpdateObjectiveByTag(string targetTag, int amount = 1)
    {
        foreach (var kvp in activeQuests)
        {
            ActiveQuest activeQuest = kvp.Value;
            
            foreach (var objective in activeQuest.questData.objectives)
            {
                if (objective.targetTag == targetTag && !objective.IsComplete)
                {
                    UpdateObjective(kvp.Key, objective.objectiveId, amount);
                }
            }
        }
    }
    
    private void GrantRewards(QuestReward reward)
    {
        if (reward == null) return;
        
        Debug.Log($"Granting rewards: {reward.experiencePoints} XP, {reward.underworldReputation} Reputation");
    }
    
    public QuestData GetQuestData(string questId)
    {
        return allQuests.Find(q => q.questId == questId);
    }
    
    public ActiveQuest GetActiveQuest(string questId)
    {
        if (activeQuests.TryGetValue(questId, out ActiveQuest quest))
        {
            return quest;
        }
        return null;
    }
    
    public QuestState GetQuestState(string questId)
    {
        if (activeQuests.ContainsKey(questId))
        {
            return activeQuests[questId].state;
        }
        
        if (completedQuests.TryGetValue(questId, out QuestState state))
        {
            return state;
        }
        
        return QuestState.NotStarted;
    }
    
    public List<QuestData> GetQuestsByType(QuestType type)
    {
        List<QuestData> result = new List<QuestData>();
        
        foreach (var kvp in activeQuests)
        {
            if (kvp.Value.questData.questType == type)
            {
                result.Add(kvp.Value.questData);
            }
        }
        
        return result;
    }
    
    public List<QuestData> GetAvailableQuests()
    {
        List<QuestData> available = new List<QuestData>();
        
        foreach (var quest in allQuests)
        {
            if (CanAcceptQuest(quest.questId))
            {
                available.Add(quest);
            }
        }
        
        return available;
    }
    
    public bool HasActiveQuest(string questId)
    {
        return activeQuests.ContainsKey(questId);
    }
    
    public bool HasCompletedQuest(string questId)
    {
        return completedQuests.ContainsKey(questId) && completedQuests[questId] == QuestState.Completed;
    }
    
    public void ForceCompleteQuest(string questId)
    {
        if (!activeQuests.ContainsKey(questId)) return;
        
        ActiveQuest activeQuest = activeQuests[questId];
        
        foreach (var objective in activeQuest.questData.objectives)
        {
            if (!objective.isOptional)
            {
                objective.currentAmount = objective.requiredAmount;
            }
        }
        
        activeQuest.state = QuestState.Completed;
        
        if (completedQuests.ContainsKey(questId))
        {
            completedQuests[questId] = QuestState.Completed;
        }
        else
        {
            completedQuests.Add(questId, QuestState.Completed);
        }
        
        if (questCompletionCount.ContainsKey(questId))
        {
            questCompletionCount[questId]++;
        }
        else
        {
            questCompletionCount[questId] = 1;
        }
        
        GrantRewards(activeQuest.questData.reward);
        onQuestCompleted?.Invoke(activeQuest.questData);
        
        activeQuests.Remove(questId);
        
        Debug.Log($"[QuestManager] Force completed quest: {questId}");
    }
    
    private QuestData CreateRuntimeQuest(string questId)
    {
        QuestData quest = ScriptableObject.CreateInstance<QuestData>();
        quest.questId = questId;
        quest.questName = GetDefaultQuestName(questId);
        quest.description = GetDefaultQuestDescription(questId);
        quest.questType = QuestType.Main;
        
        var objective = new QuestObjective
        {
            objectiveId = $"{questId}_obj_1",
            description = GetDefaultObjectiveDescription(questId),
            requiredAmount = 1
        };
        quest.objectives.Add(objective);
        
        allQuests.Add(quest);
        
        return quest;
    }
    
    private string GetDefaultQuestName(string questId)
    {
        return questId switch
        {
            "quest_understand_world" => "了解这个世界",
            "quest_talk_to_mengpo" => "与孟婆对话",
            "quest_soul_lantern" => "引魂灯",
            "quest_explore_bridge" => "探索奈何桥",
            _ => questId
        };
    }
    
    private string GetDefaultQuestDescription(string questId)
    {
        return questId switch
        {
            "quest_understand_world" => "你刚刚来到阴间，对这个世界一无所知。四处探索，了解这个神秘的地方。",
            "quest_talk_to_mengpo" => "前往奈何桥畔，与孟婆交谈，了解阴间的情况。",
            "quest_soul_lantern" => "使用引魂灯安抚迷失的灵魂。",
            "quest_explore_bridge" => "在奈何桥周围探索，寻找有价值的线索和物品。",
            _ => "完成任务目标。"
        };
    }
    
    private string GetDefaultObjectiveDescription(string questId)
    {
        return questId switch
        {
            "quest_understand_world" => "探索阴间世界",
            "quest_talk_to_mengpo" => "与孟婆对话",
            "quest_soul_lantern" => "安抚游魂",
            "quest_explore_bridge" => "探索奈何桥区域",
            _ => "完成目标"
        };
    }
    
    public void ResetAllQuests()
    {
        activeQuests.Clear();
        completedQuests.Clear();
        questCompletionCount.Clear();
        
        foreach (var quest in allQuests)
        {
            quest.ResetAllObjectives();
        }
    }
}
