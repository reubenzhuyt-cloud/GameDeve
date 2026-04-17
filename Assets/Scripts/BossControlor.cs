using UnityEngine;

public sealed class BossControlor : MonoBehaviour
{
    [SerializeField] private Match3Manager match3Manager;

    private void OnEnable()
    {
        if (match3Manager != null)
            match3Manager.OnEliminationDamage += HandleEliminationDamage;
    }

    private void OnDisable()
    {
        if (match3Manager != null)
            match3Manager.OnEliminationDamage -= HandleEliminationDamage;
    }

    private void HandleEliminationDamage(int damage, int colorType)
    {
        Debug.Log($"[BossControlor] damage={damage}, colorType={colorType}");
    }
}
