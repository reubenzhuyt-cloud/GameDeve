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

    /// <summary>
    /// If the list is empty (e.g. logic script added DialogueObj at runtime), set a single Resources/Dialogue file key (no .json).
    /// </summary>
    public void EnsureSingleDialogue(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return;
        if (dialogueFileName == null)
            dialogueFileName = new List<string>();
        if (dialogueFileName.Count == 0)
            dialogueFileName.Add(fileName);
    }
}
