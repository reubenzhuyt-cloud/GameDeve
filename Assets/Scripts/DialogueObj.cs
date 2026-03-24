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
}
