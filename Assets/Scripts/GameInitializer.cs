using UnityEngine;
using System.Collections.Generic;

public class GameInitializer : MonoBehaviour
{
    public static GameInitializer instance;
    
    [Header("Initial Quest IDs")]
    [SerializeField] private List<string> initialQuestIds = new List<string> 
    { 
        "quest_talk_to_mengpo",
        "quest_explore_bridge"
    };
    
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
    
    private void Start()
    {
        EnsureQuestManager();
        StartInitialQuests();
    }
    
    private void EnsureQuestManager()
    {
        if (QuestManager.instance == null)
        {
            var go = new GameObject("[QuestManager]");
            go.AddComponent<QuestManager>();
        }
    }
    
    private void StartInitialQuests()
    {
        if (QuestManager.instance == null) return;
        
        foreach (string questId in initialQuestIds)
        {
            if (!string.IsNullOrEmpty(questId))
            {
                if (!QuestManager.instance.HasActiveQuest(questId) && !QuestManager.instance.HasCompletedQuest(questId))
                {
                    QuestManager.instance.AcceptQuest(questId);
                    Debug.Log($"[GameInitializer] Started initial quest: {questId}");
                }
            }
        }
    }
}
