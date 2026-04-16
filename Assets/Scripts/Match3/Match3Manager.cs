using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 三消表现层：数组逻辑在 <see cref="Match3BoardSimulator"/>。
/// 拖动：拖动块略放大、目标格棋子缩小且<strong>不位移</strong>；松手后若可消则 0.1s 交换动画 → 清消除格并停顿 → 再下落补块并停顿；不可消则原路缩回。
/// 测试用原子交换仍用 <see cref="TryCommitSwap"/>（<see cref="Match3BoardSimulator.TrySwap"/>）。
/// </summary>
public sealed class Match3Manager : MonoBehaviour
{
    [Header("Prefabs (5 colors, index 0 = type 1)")]
    [SerializeField] private GameObject[] gemPrefabs = new GameObject[5];

    [Header("Board")]
    [SerializeField] private Transform boardRoot;
    [SerializeField] private Transform poolRoot;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Camera boardCamera;

    [Header("Input")]
    [SerializeField] private Collider2D boardCollider;
    [Tooltip("松手时沿交换轴投影超过该值（×cellSize）才尝试交换")]
    [SerializeField] private float dragCommitThreshold = 0.22f;
    [Tooltip("拖动时拖动块沿轴最大拉出距离（×cellSize）")]
    [SerializeField] private float dragMaxPull = 0.45f;
    [SerializeField] private float dragSelectedScale = 1.12f;
    [SerializeField] private float dragNeighborScale = 0.82f;

    [Header("Sequence (seconds)")]
    [SerializeField] private float swapAnimDuration = 0.1f;
    [SerializeField] private float afterClearPause = 0.2f;
    [SerializeField] private float afterSettlePause = 0.2f;
    [SerializeField] private float revertAnimDuration = 0.12f;

    [Header("Elimination visual")]
    [Tooltip("消除粒子预制体（对象池预热 + 播放结束回收）")]
    [SerializeField] private GameObject matchClearVfxPrefab;
    [SerializeField] private int matchClearVfxPoolSize = 12;
    [SerializeField] private float eliminationShrinkDuration = 0.18f;
    [Tooltip("回收等待 = 估算播放时长 + 该值；并受「最长回收等待」限制")]
    [SerializeField] private float matchVfxReleasePadding = 0.08f;
    [SerializeField] private float matchVfxRecycleMaxSeconds = 8f;

    [Header("Gravity visual (after each sim step)")]
    [SerializeField] private float gravityPassDuration = 0.09f;
    [SerializeField] private float newGemFallDuration = 0.14f;
    [Tooltip("下落起点相对格心向上偏移 ≈ cellSize × 因子 × (越靠上越大)")]
    [SerializeField] private float gravityFallHeightFactor = 0.55f;

    private Match3BoardSimulator _sim;
    private Match3GemPool _pool;
    private Match3VfxPool _matchVfxPool;
    private Match3GemView[,] _cells;

    private bool _pointerDown;
    private bool _inputLocked;
    private int _startRow;
    private int _startCol;
    private Match3GemView _startGem;
    private Match3GemView _neighborGem;
    private int _neighborRow = -1;
    private int _neighborCol = -1;
    private Vector2 _lastValidLocalDuringDrag;
    private Coroutine _runningRoutine;

    public Match3BoardSimulator Simulator => _sim;

    private void Awake()
    {
        _sim = new Match3BoardSimulator();
        _cells = new Match3GemView[Match3BoardSimulator.Size, Match3BoardSimulator.Size];

        if (boardRoot == null)
        {
            var t = transform.Find("BoardRoot");
            if (t == null)
            {
                var go = new GameObject("BoardRoot");
                go.transform.SetParent(transform, false);
                boardRoot = go.transform;
            }
            else
                boardRoot = t;
        }

        if (poolRoot == null)
        {
            var go = new GameObject("PoolRoot");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            poolRoot = go.transform;
        }

        if (boardCamera == null)
            boardCamera = Camera.main;

        EnsureBoardCollider();
        ValidatePrefabs();
        _pool = new Match3GemPool(poolRoot, gemPrefabs);
        if (matchClearVfxPrefab != null)
            _matchVfxPool = new Match3VfxPool(matchClearVfxPrefab, poolRoot, matchClearVfxPoolSize);
    }

    private void OnDisable()
    {
        if (_runningRoutine != null)
        {
            StopCoroutine(_runningRoutine);
            _runningRoutine = null;
        }
    }

    private void Start()
    {
        _sim.RandomInitNoMatches();
        SyncFromGrid();
    }

    private void Update()
    {
        if (boardCamera == null || _inputLocked)
            return;

        if (Input.GetMouseButtonDown(0))
            TryBeginPointer();

        if (Input.GetMouseButton(0) && _pointerDown)
            UpdateDrag();

        if (Input.GetMouseButtonUp(0) && _pointerDown)
            EndPointer();
    }

    private void TryBeginPointer()
    {
        if (!TryGetPointerBoardLocal(out Vector2 local))
            return;

        if (!TryGetCellFromLocal(local, out int row, out int col))
            return;

        if (_sim.GetCell(row, col) == 0 || _sim.IsCellLocked(row, col))
            return;

        var gem = _cells[row, col];
        if (gem == null)
            return;

        _pointerDown = true;
        _startRow = row;
        _startCol = col;
        _startGem = gem;
        _neighborGem = null;
        _neighborRow = -1;
        _neighborCol = -1;
        _lastValidLocalDuringDrag = local;
        gem.transform.SetAsLastSibling();
        ApplyDragScalesOnly();
    }

    private void UpdateDrag()
    {
        if (_startGem == null)
            return;

        if (TryGetPointerBoardLocal(out Vector2 local))
            _lastValidLocalDuringDrag = local;

        Vector2 center = CellCenterLocal(_startRow, _startCol);
        Vector2 delta = _lastValidLocalDuringDrag - center;

        if (!TryResolveNeighborFromDelta(delta, out int nr, out int nc))
        {
            PlaceCell(_startGem.transform, _startRow, _startCol);
            _startGem.transform.localScale = Vector3.one * dragSelectedScale;
            ClearNeighborDragVisual();
            return;
        }

        if (_neighborRow != nr || _neighborCol != nc)
        {
            ClearNeighborDragVisual();
            _neighborRow = nr;
            _neighborCol = nc;
            _neighborGem = _cells[nr, nc];
        }

        Vector2 axis = new Vector2(nc - _startCol, nr - _startRow);
        float pull = Vector2.Dot(delta, axis.normalized);
        pull = Mathf.Clamp(pull, 0f, dragMaxPull * cellSize);
        Vector2 offset = axis.normalized * pull;

        _startGem.transform.localPosition = CellLocalPosition(_startRow, _startCol) + (Vector3)offset;
        _startGem.transform.localScale = Vector3.one * dragSelectedScale;

        if (_neighborGem != null)
        {
            PlaceCell(_neighborGem.transform, _neighborRow, _neighborCol);
            _neighborGem.transform.localScale = Vector3.one * dragNeighborScale;
        }
    }

    private void ClearNeighborDragVisual()
    {
        if (_neighborGem != null && _neighborRow >= 0 && _neighborCol >= 0)
        {
            PlaceCell(_neighborGem.transform, _neighborRow, _neighborCol);
            _neighborGem.transform.localScale = Vector3.one;
        }

        _neighborGem = null;
        _neighborRow = -1;
        _neighborCol = -1;
    }

    private void EndPointer()
    {
        if (!_pointerDown)
            return;

        _pointerDown = false;

        if (_startGem == null)
            return;

        var gemStart = _startGem;
        var gemNeighbor = _neighborGem;
        int sr = _startRow;
        int sc = _startCol;
        int nr = _neighborRow;
        int nc = _neighborCol;

        _startGem = null;
        _neighborGem = null;
        _neighborRow = -1;
        _neighborCol = -1;

        Vector2 releaseLocal = _lastValidLocalDuringDrag;
        if (TryGetPointerBoardLocal(out Vector2 fresh))
            releaseLocal = fresh;

        Vector2 center = CellCenterLocal(sr, sc);
        Vector2 delta = releaseLocal - center;

        bool wantsSwap = TryResolveNeighborFromDeltaFor(sr, sc, delta, out int tr, out int tc) &&
                         Vector2.Dot(delta, CellCenterLocal(tr, tc) - center) >= dragCommitThreshold * cellSize;

        if (!wantsSwap || gemNeighbor == null || !_sim.WouldMatchAfterSwap(sr, sc, tr, tc))
        {
            if (_runningRoutine != null)
                StopCoroutine(_runningRoutine);
            _runningRoutine = StartCoroutine(CoRevertInvalid(gemStart, gemNeighbor, sr, sc));
            return;
        }

        if (_runningRoutine != null)
            StopCoroutine(_runningRoutine);
        _runningRoutine = StartCoroutine(CoValidSwapSequence(sr, sc, tr, tc, gemStart, gemNeighbor));
    }

    private IEnumerator CoRevertInvalid(Match3GemView gemStart, Match3GemView gemNeighbor, int sr, int sc)
    {
        _inputLocked = true;
        int nr = 0, nc = 0;
        bool haveNeighborCell = gemNeighbor != null && TryFindGemCell(gemNeighbor, out nr, out nc);

        Vector3 from = gemStart.transform.localPosition;
        Vector3 to = CellLocalPosition(sr, sc);
        Vector3 s0 = gemStart.transform.localScale;
        Vector3 sN = gemNeighbor != null ? gemNeighbor.transform.localScale : Vector3.one;
        float dur = Mathf.Max(0.01f, revertAnimDuration);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            gemStart.transform.localPosition = Vector3.Lerp(from, to, u);
            gemStart.transform.localScale = Vector3.Lerp(s0, Vector3.one, u);
            if (gemNeighbor != null)
                gemNeighbor.transform.localScale = Vector3.Lerp(sN, Vector3.one, u);
            yield return null;
        }

        PlaceCell(gemStart.transform, sr, sc);
        gemStart.transform.localScale = Vector3.one;
        if (haveNeighborCell)
        {
            PlaceCell(gemNeighbor.transform, nr, nc);
            gemNeighbor.transform.localScale = Vector3.one;
        }

        _inputLocked = false;
        _runningRoutine = null;
    }

    private bool TryFindGemCell(Match3GemView g, out int row, out int col)
    {
        for (row = 0; row < Match3BoardSimulator.Size; row++)
        {
            for (col = 0; col < Match3BoardSimulator.Size; col++)
            {
                if (_cells[row, col] == g)
                    return true;
            }
        }

        row = col = 0;
        return false;
    }

    private IEnumerator CoValidSwapSequence(int sr, int sc, int tr, int tc, Match3GemView gemA, Match3GemView gemB)
    {
        _inputLocked = true;

        if (!_sim.ApplySwapOnly(sr, sc, tr, tc))
        {
            _inputLocked = false;
            _runningRoutine = null;
            yield break;
        }

        (_cells[sr, sc], _cells[tr, tc]) = (_cells[tr, tc], _cells[sr, sc]);
        gemA.Setup(_sim.GetCell(tr, tc));
        gemB.Setup(_sim.GetCell(sr, sc));

        Vector3 a0 = gemA.transform.localPosition;
        Vector3 b0 = gemB.transform.localPosition;
        Vector3 a1 = CellLocalPosition(tr, tc);
        Vector3 b1 = CellLocalPosition(sr, sc);
        gemA.transform.localScale = Vector3.one;
        gemB.transform.localScale = Vector3.one;

        float swapDur = Mathf.Max(0.01f, swapAnimDuration);
        for (float t = 0f; t < swapDur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / swapDur));
            gemA.transform.localPosition = Vector3.Lerp(a0, a1, u);
            gemB.transform.localPosition = Vector3.Lerp(b0, b1, u);
            yield return null;
        }

        PlaceCell(gemA.transform, tr, tc);
        PlaceCell(gemB.transform, sr, sc);
        gemA.transform.localScale = Vector3.one;
        gemB.transform.localScale = Vector3.one;

        while (_sim.HasAnyMatch())
        {
            var matches = _sim.CollectMatches();
            if (matches.Count == 0)
                break;

            yield return CoEliminationShrinkAndParticles(matches);
            yield return new WaitForSeconds(Mathf.Max(0f, afterClearPause));

            yield return CoSettlePhysicsAndRefillWithVisual();
            yield return new WaitForSeconds(Mathf.Max(0f, afterSettlePause));
        }

        _inputLocked = false;
        _runningRoutine = null;
    }

    private IEnumerator CoEliminationShrinkAndParticles(HashSet<(int r, int c)> matches)
    {
        foreach (var (r, c) in matches)
        {
            if (_sim.IsCellLocked(r, c))
                continue;
            var gem = _cells[r, c];
            Color tint = gem != null ? gem.GetDisplayColor() : Color.white;
            SpawnMatchClearVfx(r, c, tint);
        }

        float dur = Mathf.Max(0.02f, eliminationShrinkDuration);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            foreach (var (r, c) in matches)
            {
                if (_sim.IsCellLocked(r, c))
                    continue;
                var v = _cells[r, c];
                if (v == null) continue;
                v.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0f, u);
            }

            yield return null;
        }

        foreach (var (r, c) in matches)
        {
            if (_sim.IsCellLocked(r, c))
                continue;
            var v = _cells[r, c];
            if (v == null) continue;
            _pool.Release(v.gameObject);
            _cells[r, c] = null;
        }

        _sim.ClearMatchCells(matches);
    }

    private void SpawnMatchClearVfx(int row, int col, Color tint)
    {
        if (_matchVfxPool == null)
            return;

        var fx = _matchVfxPool.Get();
        fx.transform.SetParent(boardRoot, false);
        fx.transform.localPosition = CellLocalPosition(row, col);
        fx.transform.localRotation = Quaternion.identity;
        ApplyParticleSystemsTint(fx.transform, tint);

        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);

        float wait = Mathf.Min(
            Match3VfxPool.EstimateMaxPlayDuration(fx) + matchVfxReleasePadding,
            Mathf.Max(0.1f, matchVfxRecycleMaxSeconds));
        StartCoroutine(CoReleaseMatchVfx(fx, wait));
    }

    private IEnumerator CoReleaseMatchVfx(GameObject instance, float waitSeconds)
    {
        yield return new WaitForSeconds(waitSeconds);
        if (_matchVfxPool != null && instance != null)
            _matchVfxPool.Release(instance);
    }

    /// <summary>将粒子主模块 Start Color 设为 tint（含子物体上的 ParticleSystem）。</summary>
    private static void ApplyParticleSystemsTint(Transform root, Color tint)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(tint);
        }
    }

    private IEnumerator CoSettlePhysicsAndRefillWithVisual()
    {
        while (true)
        {
            bool moved;
            do
            {
                bool v = _sim.StepVerticalGravity();
                bool h = _sim.StepHorizontalCompact();
                moved = v || h;
                if (!moved)
                    break;

                SyncFromGrid();
                yield return CoGravityPassVisual(gravityPassDuration);
            } while (moved);

            if (!_sim.HasAnyEmpty())
                break;

            _sim.RefillEmptyCells();
            SyncFromGrid();
            yield return CoGravityPassVisual(newGemFallDuration);
        }
    }

    private IEnumerator CoGravityPassVisual(float duration)
    {
        var items = new List<(Transform tr, Vector3 start, Vector3 end)>();
        for (int r = 0; r < Match3BoardSimulator.Size; r++)
        {
            for (int c = 0; c < Match3BoardSimulator.Size; c++)
            {
                var v = _cells[r, c];
                if (v == null) continue;

                Transform tr = v.transform;
                Vector3 end = CellLocalPosition(r, c);
                float rowT = (Match3BoardSimulator.Size - r) / (float)Match3BoardSimulator.Size;
                float lift = cellSize * gravityFallHeightFactor * (rowT + 0.12f);
                Vector3 start = end + Vector3.up * lift;
                items.Add((tr, start, end));
                tr.localPosition = start;
            }
        }

        float dur = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            foreach (var (tr, start, end) in items)
                tr.localPosition = Vector3.Lerp(start, end, u);
            yield return null;
        }

        foreach (var (tr, start, end) in items)
            tr.localPosition = end;
    }

    /// <summary>与输入/UI 无关：原子 TrySwap + Sync（供测试或跳过动画的路径）。</summary>
    public bool TryCommitSwap(int rowA, int colA, int rowB, int colB)
    {
        if (!_sim.TrySwap(rowA, colA, rowB, colB))
            return false;
        SyncFromGrid();
        return true;
    }

    private void ApplyDragScalesOnly()
    {
        if (_startGem != null)
            _startGem.transform.localScale = Vector3.one * dragSelectedScale;
    }

    private bool TryResolveNeighborFromDelta(Vector2 delta, out int nr, out int nc) =>
        TryResolveNeighborFromDeltaFor(_startRow, _startCol, delta, out nr, out nc);

    private bool TryResolveNeighborFromDeltaFor(int sr, int sc, Vector2 delta, out int nr, out int nc)
    {
        nr = sr;
        nc = sc;
        float ax = Mathf.Abs(delta.x);
        float ay = Mathf.Abs(delta.y);
        if (ax < 1e-4f && ay < 1e-4f)
            return false;

        if (ax >= ay)
        {
            nc = sc + (delta.x > 0f ? 1 : -1);
            nr = sr;
        }
        else
        {
            nr = sr + (delta.y > 0f ? -1 : 1);
            nc = sc;
        }

        if (nc < 0 || nc >= Match3BoardSimulator.Size || nr < 0 || nr >= Match3BoardSimulator.Size)
            return false;
        if (!IsAdjacent(sr, sc, nr, nc))
            return false;
        if (_sim.IsCellLocked(nr, nc))
            return false;
        if (_sim.GetCell(nr, nc) == 0)
            return false;
        return true;
    }

    private bool TryGetPointerBoardLocal(out Vector2 local)
    {
        local = default;
        var world = boardCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        if (boardCollider != null && !boardCollider.OverlapPoint(world))
            return false;
        local = (Vector2)boardRoot.InverseTransformPoint(world);
        return true;
    }

    private Vector2 CellCenterLocal(int row, int col) => (Vector2)CellLocalPosition(row, col);

    public void SyncFromGrid()
    {
        for (int r = 0; r < Match3BoardSimulator.Size; r++)
        {
            for (int c = 0; c < Match3BoardSimulator.Size; c++)
            {
                int t = _sim.Grid[r, c];
                var existing = _cells[r, c];

                if (t == 0)
                {
                    if (existing != null)
                    {
                        _pool.Release(existing.gameObject);
                        _cells[r, c] = null;
                    }

                    continue;
                }

                if (existing != null && existing.GemType == t)
                {
                    PlaceCell(existing.transform, r, c);
                    continue;
                }

                if (existing != null)
                {
                    _pool.Release(existing.gameObject);
                    _cells[r, c] = null;
                }

                var go = _pool.Get(t);
                if (go == null)
                {
                    Debug.LogError($"[Match3] Missing prefab for gem type {t}.");
                    continue;
                }

                go.transform.SetParent(boardRoot, false);
                var view = go.GetComponent<Match3GemView>();
                _cells[r, c] = view;
                PlaceCell(go.transform, r, c);
            }
        }
    }

    private void PlaceCell(Transform tr, int row, int col)
    {
        tr.localPosition = CellLocalPosition(row, col);
        tr.localRotation = Quaternion.identity;
        tr.localScale = Vector3.one;
    }

    private Vector3 CellLocalPosition(int row, int col)
    {
        float half = Match3BoardSimulator.Size * cellSize * 0.5f;
        float x = col * cellSize + cellSize * 0.5f - half;
        float y = (Match3BoardSimulator.Size - 1 - row) * cellSize + cellSize * 0.5f - half;
        return new Vector3(x, y, 0f);
    }

    private bool TryGetCellFromLocal(Vector2 local, out int row, out int col)
    {
        float half = Match3BoardSimulator.Size * cellSize * 0.5f;
        col = Mathf.FloorToInt((local.x + half) / cellSize);
        row = Match3BoardSimulator.Size - 1 - Mathf.FloorToInt((local.y + half) / cellSize);

        if (col < 0 || col >= Match3BoardSimulator.Size || row < 0 || row >= Match3BoardSimulator.Size)
        {
            row = 0;
            col = 0;
            return false;
        }

        return true;
    }

    private static bool IsAdjacent(int r1, int c1, int r2, int c2)
    {
        return Mathf.Abs(r1 - r2) + Mathf.Abs(c1 - c2) == 1;
    }

    private void EnsureBoardCollider()
    {
        if (boardCollider != null) return;

        var target = boardRoot != null ? boardRoot.gameObject : gameObject;
        boardCollider = target.GetComponent<Collider2D>();
        if (boardCollider == null)
            boardCollider = target.AddComponent<BoxCollider2D>();

        if (boardCollider is BoxCollider2D box)
        {
            float s = Match3BoardSimulator.Size * cellSize;
            box.size = new Vector2(s, s);
            box.offset = Vector2.zero;
        }
    }

    private void ValidatePrefabs()
    {
        if (gemPrefabs == null || gemPrefabs.Length < Match3BoardSimulator.MaxType)
            Debug.LogWarning($"[Match3] Assign {Match3BoardSimulator.MaxType} gem prefabs in Match3Manager.");

        for (int i = 0; i < gemPrefabs.Length && i < Match3BoardSimulator.MaxType; i++)
        {
            if (gemPrefabs[i] == null) continue;
            if (gemPrefabs[i].GetComponent<Match3GemView>() == null)
                Debug.LogWarning($"[Match3] Gem prefab at index {i} should have a Match3GemView component.");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var root = boardRoot != null ? boardRoot : transform;
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        for (int r = 0; r < Match3BoardSimulator.Size; r++)
        {
            for (int c = 0; c < Match3BoardSimulator.Size; c++)
            {
                float half = Match3BoardSimulator.Size * cellSize * 0.5f;
                float x = c * cellSize + cellSize * 0.5f - half;
                float y = (Match3BoardSimulator.Size - 1 - r) * cellSize + cellSize * 0.5f - half;
                var local = new Vector3(x, y, 0f);
                var world = root.TransformPoint(local);
                Gizmos.DrawWireCube(world, new Vector3(cellSize * 0.95f, cellSize * 0.95f, 0.1f));
            }
        }
    }
#endif
}
