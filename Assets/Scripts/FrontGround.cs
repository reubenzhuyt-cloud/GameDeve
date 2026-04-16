using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 前景/中景视差：跟随相机水平偏移。可选「水平无限拼接」——启动时按条带宽度复制铺满视野，
/// 之后每帧视差结束若某条带整段超出相机一侧，则将锚点平移到另一侧，形成无缝循环。
/// MidGround / FrontGround 可共用本脚本。
/// </summary>
public class FrontGround : MonoBehaviour
{
    [Header("Parallax")]
    public Transform mainCamera;
    [Tooltip("视差系数，可正可负（与相机位移同向或反向）")]
    public float speed = 0.95f;

    [Header("Horizontal infinite strip")]
    [SerializeField] private bool enableInfiniteHorizontal = true;
    [Tooltip(">0 时使用该世界宽度作为条带间距，不自动量 Renderer")]
    [SerializeField] private float tileWidthWorldOverride;
    [Tooltip("相对相机可视宽度多铺的条带数量（防止缝）")]
    [SerializeField] private float extraStripPadding = 2f;
    [Tooltip("判定「整段离开视野」时与相机边界的额外边距（世界单位）")]
    [SerializeField] private float wrapMargin = 0.5f;
    [SerializeField] private bool drawStripGizmos;

    private Vector3 mainCameraPosition;
    private Vector3 frontGroundPosition;

    private readonly List<Transform> segmentRoots = new List<Transform>();
    private readonly List<float> segmentAnchorWorldX = new List<float>();
    private float stripWorldWidth = 1f;
    private Camera cachedCam;

    private void Start()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[FrontGround] mainCamera 未赋值。", this);
            enabled = false;
            return;
        }

        cachedCam = mainCamera.GetComponent<Camera>();
        if (cachedCam == null)
        {
            Debug.LogError("[FrontGround] mainCamera 上需要 Camera 组件。", this);
            enabled = false;
            return;
        }

        mainCameraPosition = mainCamera.position;
        frontGroundPosition = transform.position;

        if (enableInfiniteHorizontal)
            BuildHorizontalStrips();
    }

    private void BuildHorizontalStrips()
    {
        segmentRoots.Clear();
        segmentAnchorWorldX.Clear();

        stripWorldWidth = tileWidthWorldOverride > 0.001f
            ? tileWidthWorldOverride
            : ComputeWorldStripSpanX(transform);

        if (stripWorldWidth < 0.01f)
        {
            Debug.LogWarning("[FrontGround] 条带宽度过小，关闭无限拼接。", this);
            enableInfiniteHorizontal = false;
            return;
        }

        int count = ComputeStripCountForCamera(cachedCam, stripWorldWidth, extraStripPadding);
        segmentRoots.Add(transform);
        segmentAnchorWorldX.Add(transform.position.x);

        for (int i = 1; i < count; i++)
        {
            GameObject dup = Instantiate(gameObject, transform.parent);
            dup.name = $"{gameObject.name}_strip{i}";
            dup.SetActive(false);
            FrontGround fg = dup.GetComponent<FrontGround>();
            if (fg != null)
                DestroyImmediate(fg);

            Vector3 p = transform.position;
            p.x += stripWorldWidth * i;
            dup.transform.position = p;

            dup.SetActive(true);

            segmentRoots.Add(dup.transform);
            segmentAnchorWorldX.Add(p.x);
        }

        frontGroundPosition = transform.position;
    }

    private void Update()
    {
        if (mainCamera == null)
            return;

        if (!enableInfiniteHorizontal || segmentRoots.Count == 0)
        {
            ApplyParallaxSingle();
            return;
        }

        Vector3 camDelta = (mainCamera.position - mainCameraPosition) * speed;
        float dx = camDelta.x;

        for (int i = 0; i < segmentRoots.Count; i++)
        {
            Transform seg = segmentRoots[i];
            if (seg == null)
                continue;

            float ax = segmentAnchorWorldX[i];
            Vector3 p = seg.position;
            p.x = ax + dx;
            p.y = frontGroundPosition.y;
            p.z = seg.position.z;
            seg.position = p;
        }

        ApplyHorizontalWrap();
    }

    private void ApplyParallaxSingle()
    {
        Vector3 newPosition = frontGroundPosition + (mainCamera.position - mainCameraPosition) * speed;
        transform.position = new Vector3(newPosition.x, frontGroundPosition.y, transform.position.z);
    }

    private void ApplyHorizontalWrap()
    {
        if (cachedCam == null || segmentRoots.Count == 0)
            return;

        float halfW = cachedCam.orthographicSize * cachedCam.aspect;
        float cx = mainCamera.position.x;
        float left = cx - halfW - wrapMargin;
        float right = cx + halfW + wrapMargin;
        float shift = stripWorldWidth * segmentRoots.Count;

        for (int i = 0; i < segmentRoots.Count; i++)
        {
            Transform seg = segmentRoots[i];
            if (seg == null)
                continue;

            Bounds b = ComputeWorldRenderBounds(seg);
            if (b.max.x < left)
                segmentAnchorWorldX[i] += shift;
            else if (b.min.x > right)
                segmentAnchorWorldX[i] -= shift;
        }
    }

    /// <summary>条带在 X 方向上的世界宽度（用于平铺间距）。</summary>
    public static float ComputeWorldStripSpanX(Transform root)
    {
        Bounds b = ComputeWorldRenderBounds(root);
        float w = b.size.x;
        return w > 0.001f ? w : 1f;
    }

    public static Bounds ComputeWorldRenderBounds(Transform root)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>();
        if (rs == null || rs.Length == 0)
            return new Bounds(root.position, Vector3.zero);

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++)
            b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>可视宽度内至少铺满所需条带数 + 额外缓冲。</summary>
    public static int ComputeStripCountForCamera(Camera cam, float stripWorldWidth, float extraPaddingTiles)
    {
        if (cam == null || stripWorldWidth < 0.001f)
            return 2;

        float halfW = cam.orthographicSize * cam.aspect;
        float viewW = halfW * 2f;
        int need = Mathf.CeilToInt(viewW / stripWorldWidth);
        int pad = Mathf.CeilToInt(extraPaddingTiles);
        return Mathf.Max(2, need + pad);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawStripGizmos || !enableInfiniteHorizontal)
            return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        foreach (Transform t in segmentRoots)
        {
            if (t == null)
                continue;
            Bounds b = ComputeWorldRenderBounds(t);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
