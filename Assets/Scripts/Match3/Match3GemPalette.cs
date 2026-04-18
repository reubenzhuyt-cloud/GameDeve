using UnityEngine;

/// <summary>棋盘类型 1～5 与「红绿黑黄白」展示色（得分、飞弹等统一用）。</summary>
public static class Match3GemPalette
{
    /// <summary>类型 1=红 … 5=白。</summary>
    public static Color GetAccentColorForGemType(int gemType)
    {
        return gemType switch
        {
            1 => new Color(0.95f, 0.2f, 0.2f),
            2 => new Color(0.2f, 0.85f, 0.25f),
            3 => new Color(0.12f, 0.12f, 0.12f),
            4 => new Color(1f, 0.92f, 0.15f),
            5 => new Color(0.95f, 0.95f, 0.95f),
            _ => Color.white
        };
    }
}
