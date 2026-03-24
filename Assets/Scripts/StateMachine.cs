using Unity.VisualScripting;
using UnityEngine;

public class StateMachine<T> where T : EntityState
{

    public T currentState { get; private set; }
    public T previousState;
    public void Initialize(T state)
    {
        currentState = state;
        currentState.Enter();
    }
    public void ChangeState(T state)
    {
        previousState = currentState;
        currentState.Exit();
        currentState = state;
        currentState.Enter();
    }
}
