using UnityEngine;
using UnityEngine.Events;

public class PlayerChatState : PlayerState
{
    public PlayerChatState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName) : base(player, stateMachine, animationBoolName)
    {

    }
    private UnityAction<DialogueData> onDialogueEndAction;

    public override void Enter()
    {
        base.Enter();
        player.SetVelocity(0);

        onDialogueEndAction = (DialogueData dialogue) =>
        {
            player.stateMachine.ChangeState(player.idleState);
        };
        
        if (DialogueSystem.instance != null)
        {
            DialogueSystem.instance.onDialogueEnd.AddListener(onDialogueEndAction);
            
            if (player.currentNPC != null)
            {
                player.currentNPC.Interact();
            }
            else if (player.currentInteractable != null)
            {
                player.currentInteractable.Interact();
            }
            else if (player.dialogueLogic != null)
            {
                DialogueSystem.instance.onChoiceSelected.AddListener(OnChoiceSelected);
                player.dialogueLogic.OnDialogueStart();
                DialogueSystem.instance.StartDialogue(player.dialogueLogic.GetDialogueFileName());
            }
            else if (player.dialogueObj != null)
            {
                DialogueSystem.instance.StartDialogue(player.dialogueObj.currentDialogue());
            }
            else
            {
                Debug.LogError("No interactable object found");
                player.stateMachine.ChangeState(player.idleState);
            }
        }
        else
        {
            Debug.LogError("DialogueSystem.instance is null");
        }

    }
    
    private void OnChoiceSelected(int choiceIndex, string choiceText)
    {
        if (player.dialogueLogic != null)
        {
            player.dialogueLogic.OnChoiceSelected(choiceIndex, choiceText);
        }
    }
    
    public override void Update()
    {

        base.Update();
    }
    public override void Exit()
    {
        base.Exit();

        DialogueSystem.instance.onDialogueEnd.RemoveListener(onDialogueEndAction);
        
        if (player.dialogueLogic != null)
        {
            DialogueSystem.instance.onChoiceSelected.RemoveListener(OnChoiceSelected);
            player.dialogueLogic.OnDialogueEnd();
        }

        onDialogueEndAction = null;
    }
}
