using UnityEngine;
using UnityEngine.UI;

/// <summary>挂在每个宝石 prefab 根节点；类型 1–5 与 <see cref="Match3BoardSimulator"/> 一致。</summary>
[DisallowMultipleComponent]
public sealed class Match3GemView : MonoBehaviour
{
    [SerializeField] private int gemType = 1;

    public int GemType => gemType;

    public void Setup(int type)
    {
        gemType = Mathf.Clamp(type, Match3BoardSimulator.MinType, Match3BoardSimulator.MaxType);
    }

    /// <summary>用于消除粒子等特效与方块同色；优先 SpriteRenderer，其次 UGUI Image。</summary>
    public Color GetDisplayColor()
    {
        var sr = GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
            return sr.color;

        var img = GetComponentInChildren<Image>(true);
        if (img != null)
            return img.color;

        return Color.white;
    }
}
