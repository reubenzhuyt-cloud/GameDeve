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
            DialogueSystem.instance.StartDialogue(player.dialogueObj.currentDialogue());
        }
        else
        {
            Debug.LogError("DialogueSystem.instance is null");
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

        onDialogueEndAction = null;
    }
}
