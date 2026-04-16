using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 条带数量 / 宽度公式校验（不依赖场景物体）。
/// </summary>
public class FrontGroundStripTests
{
    [Test]
    public void ComputeStripCount_CoversViewPlusPadding()
    {
        var go = new GameObject("cam", typeof(Camera));
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.aspect = 16f / 9f;

        float viewW = cam.orthographicSize * 2f * cam.aspect;
        float stripW = viewW / 4f;

        int n = FrontGround.ComputeStripCountForCamera(cam, stripW, 2f);
        Assert.GreaterOrEqual(n, 2);
        Assert.GreaterOrEqual(n * stripW, viewW, "总宽度应至少覆盖可视宽度");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ComputeStripCount_MinimumIsTwo()
    {
        var go = new GameObject("cam", typeof(Camera));
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        cam.aspect = 2f;

        int n = FrontGround.ComputeStripCountForCamera(cam, 9999f, 0f);
        Assert.AreEqual(2, n);

        Object.DestroyImmediate(go);
    }
}
