using UnityEngine;
using System.Collections.Generic;

public enum QuestType
{
    Main,
    Side,
    Hidden
}

public enum QuestState
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}

[System.Serializable]
public class QuestObjective
{
    public string objectiveId;
    public string description;
    public int requiredAmount = 1;
    public int currentAmount = 0;
    public bool isOptional = false;
    public string targetTag;
    public string targetScene;
    public Vector3 targetPosition;
    
    public bool IsComplete => currentAmount >= requiredAmount;
    public float Progress => (float)currentAmount / requiredAmount;
    
    public void Reset()
    {
        currentAmount = 0;
    }
    
    public void AddProgress(int amount = 1)
    {
        currentAmount = Mathf.Min(currentAmount + amount, requiredAmount);
    }
}

[System.Serializable]
public class QuestReward
{
    public int experiencePoints;
    public int underworldReputation;
    public List<string> itemIds = new List<string>();
    public string unlockDialogueId;
}

[System.Serializable]
public class QuestPrerequisite
{
    public string prerequisiteQuestId;
    public QuestState requiredState = QuestState.Completed;
}

[CreateAssetMenu(fileName = "NewQuest", menuName = "Game/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Basic Info")]
    public string questId;
    public string questName;
    [TextArea(3, 6)]
    public string description;
    [TextArea(3, 6)]
    public string completionDescription;
    
    [Header("Type")]
    public QuestType questType = QuestType.Main;
    public int chapter = 1;
    
    [Header("Objectives")]
    public List<QuestObjective> objectives = new List<QuestObjective>();
    
    [Header("Rewards")]
    public QuestReward reward;
    
    [Header("Prerequisites")]
    public List<QuestPrerequisite> prerequisites = new List<QuestPrerequisite>();
    
    [Header("Dialogue")]
    public string startDialogueFile;
    public string inProgressDialogueFile;
    public string completionDialogueFile;
    
    [Header("Settings")]
    public bool autoStart = false;
    public bool isRepeatable = false;
    public bool showInLog = true;
    public int timeLimitMinutes = -1;
    
    public bool AreAllRequiredObjectivesComplete()
    {
        foreach (var objective in objectives)
        {
            if (!objective.isOptional && !objective.IsComplete)
                return false;
        }
        return true;
    }
    
    public float GetOverallProgress()
    {
        if (objectives.Count == 0) return 0f;
        
        float totalProgress = 0f;
        int requiredCount = 0;
        
        foreach (var objective in objectives)
        {
            if (!objective.isOptional)
            {
                totalProgress += objective.Progress;
                requiredCount++;
            }
        }
        
        return requiredCount > 0 ? totalProgress / requiredCount : 0f;
    }
    
    public QuestObjective GetObjective(string objectiveId)
    {
        return objectives.Find(o => o.objectiveId == objectiveId);
    }
    
    public void ResetAllObjectives()
    {
        foreach (var objective in objectives)
        {
            objective.Reset();
        }
    }
}
