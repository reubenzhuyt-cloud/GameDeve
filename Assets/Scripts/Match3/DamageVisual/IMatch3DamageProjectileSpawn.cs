using UnityEngine;

/// <summary>由 Match3 在消除等时机调用，在棋盘局部坐标生成飞向 Boss 的伤害表现体。</summary>
public interface IMatch3DamageProjectileSpawn
{
    /// <param name="referenceScaleForSize">用于决定飞弹相对大小的参考缩放（如得分飘字 scale）</param>
    /// <param name="tint">飞弹精灵着色（与消除颜色一致）</param>
    void SpawnOrbAtBoardLocal(Vector3 boardLocalSpawnPosition, float referenceScaleForSize, Color tint);
}
