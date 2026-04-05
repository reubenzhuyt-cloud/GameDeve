using UnityEngine;
using System.Collections.Generic;

public class DialogueStateTracker : MonoBehaviour
{
    public static DialogueStateTracker instance;
    
    private Dictionary<string, int> dialogueStates = new Dictionary<string, int>();
    private Dictionary<string, int> dialogueChoiceHistory = new Dictionary<string, int>();
    
    public static DialogueStateTracker Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[DialogueStateTracker]");
                instance = go.AddComponent<DialogueStateTracker>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    public int GetState(string objectId)
    {
        if (dialogueStates.TryGetValue(objectId, out int state))
        {
            return state;
        }
        return 0;
    }
    
    public void SetState(string objectId, int state)
    {
        dialogueStates[objectId] = state;
    }
    
    public int GetLastChoice(string objectId)
    {
        if (dialogueChoiceHistory.TryGetValue(objectId, out int choice))
        {
            return choice;
        }
        return -1;
    }
    
    public void SetLastChoice(string objectId, int choiceIndex)
    {
        dialogueChoiceHistory[objectId] = choiceIndex;
    }
    
    public void ResetObjectState(string objectId)
    {
        dialogueStates.Remove(objectId);
        dialogueChoiceHistory.Remove(objectId);
    }
    
    public void ResetAllStates()
    {
        dialogueStates.Clear();
        dialogueChoiceHistory.Clear();
    }
    
    public bool HasInteractedBefore(string objectId)
    {
        return GetState(objectId) > 0 || GetLastChoice(objectId) >= 0;
    }
}
