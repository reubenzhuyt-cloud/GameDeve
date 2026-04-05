using UnityEngine;
using System.Collections.Generic;

public class DialogueObj : MonoBehaviour
{
    [SerializeField] private List<string> dialogueFileName = new();
    public int currentDialogueIndex = 0;
    public string currentDialogue()
    {
        return dialogueFileName[currentDialogueIndex];
    }
    
    public void SetDialogueFileNames(List<string> fileNames)
    {
        dialogueFileName = fileNames;
    }
    
    public List<string> GetDialogueFileNames()
    {
        return dialogueFileName;
    }
}
