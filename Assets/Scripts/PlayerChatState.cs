using UnityEngine;
using UnityEngine.Events;

public class PlayerChatState : PlayerState
{
    public PlayerChatState(Player player, StateMachine<PlayerState> stateMachine, string animationBoolName) : base(player, stateMachine, animationBoolName)
    {

    }
    private UnityAction<DialogueData> onDialogueEndAction;
    /// <summary>本帧是否走了「Q 光芒 → WeakSoulLightSoothe」路径；此时不应再对 <see cref="Player.dialogueLogic"/> 调 <c>OnDialogueEnd</c>（避免误改 <see cref="WeakSoulDialogueLogic"/> 等状态）。</summary>
    private bool _sessionWasLightSoothe;

    public override void Enter()
    {
        base.Enter();
        player.SetVelocity(0);
        _sessionWasLightSoothe = false;

        onDialogueEndAction = (DialogueData dialogue) =>
        {
            if (player.pendingLightSootheSoul != null)
            {
                player.pendingLightSootheSoul.CompleteLightSoothe();
                player.StartLightSkillCD();
                player.pendingLightSootheSoul = null;
            }
            player.stateMachine.ChangeState(player.idleState);
        };
        
        if (DialogueSystem.instance != null)
        {
            DialogueSystem.instance.onDialogueEnd.AddListener(onDialogueEndAction);

            if (!string.IsNullOrEmpty(player.pendingLightSootheDialogueFile))
            {
                _sessionWasLightSoothe = true;
                string file = player.pendingLightSootheDialogueFile;
                player.pendingLightSootheDialogueFile = null;
                DialogueSystem.instance.StartDialogue(file);
                return;
            }
            
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
        
        if (player.dialogueLogic != null && !_sessionWasLightSoothe)
        {
            DialogueSystem.instance.onChoiceSelected.RemoveListener(OnChoiceSelected);
            player.dialogueLogic.OnDialogueEnd();
        }

        onDialogueEndAction = null;
    }
}
