using TMPro;
using UnityEngine;

/// <summary>Score popup view wrapper for reliable text assignment.</summary>
public sealed class Match3ScoreTextView : MonoBehaviour
{
    [SerializeField] private TextMeshPro tmp3d;
    [SerializeField] private TMP_Text tmpAny;

    private void Awake()
    {
        ResolveTargetsIfNeeded();
    }

    private void ResolveTargetsIfNeeded()
    {
        // Prefer 3D TextMeshPro (MeshRenderer). Fallback to any TMP_Text (UGUI or 3D).
        if (tmp3d == null)
            tmp3d = GetComponentInChildren<TextMeshPro>(true);
        if (tmpAny == null)
            tmpAny = GetComponentInChildren<TMP_Text>(true);
    }

    public bool SetScoreText(string text)
    {
        // Do not rely on Awake timing; score popup may be written while inactive.
        ResolveTargetsIfNeeded();

        if (tmp3d != null)
        {
            tmp3d.text = text;
            EnsurePopupVisible(tmp3d, tmpAny);
            return true;
        }

        if (tmpAny != null)
        {
            tmpAny.text = text;
            EnsurePopupVisible(tmp3d, tmpAny);
            return true;
        }

        return false;
    }

    public void SetTextColor(Color color)
    {
        ResolveTargetsIfNeeded();
        if (tmp3d != null)
            tmp3d.color = color;
        if (tmpAny != null)
            tmpAny.color = color;
    }

    private static void EnsurePopupVisible(TextMeshPro tmp3d, TMP_Text tmpAny)
    {
        if (tmp3d != null)
        {
            var c = tmp3d.color;
            if (c.a < 0.05f)
                c.a = 1f;
            tmp3d.color = c;
            var mr = tmp3d.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = true;
                mr.sortingOrder = 500;
            }
        }

        if (tmpAny != null)
        {
            tmpAny.alpha = 1f;
            var c = tmpAny.color;
            c.a = 1f;
            tmpAny.color = c;
        }
    }
}
