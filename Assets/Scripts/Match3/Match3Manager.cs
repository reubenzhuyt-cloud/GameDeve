using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 三消表现层：数组逻辑在 <see cref="Match3BoardSimulator"/>。
/// 拖动：邻格交换轴由两格中心连线决定（上下左右均正确）；棋子随指针偏移；按下即放大选中块，再可随拉力增至 dragSelectedScaleMax；邻格缩小带死区。松手可消则交换序列；不可消则弹回。
/// 测试用原子交换仍用 <see cref="TryCommitSwap"/>（<see cref="Match3BoardSimulator.TrySwap"/>）。
/// </summary>
public sealed class Match3Manager : MonoBehaviour
{
    /// <summary>玩家第一次<strong>成功</strong>交换（可消且已提交）后触发一次；用于开场台词等。</summary>
    public event Action OnBattleStarted;

    /// <summary>已成功提交的可消交换次数（无效拖放与弹回不计）。</summary>
    public int SuccessfulPlayerMoveCount { get; private set; }

    /// <summary>是否已发生过至少一次成功移动（等价于 <see cref="SuccessfulPlayerMoveCount"/> &gt; 0）。</summary>
    public bool BattleHasStarted => SuccessfulPlayerMoveCount > 0;

    public event Action<int, int> OnEliminationDamage;

    /// <summary>
    /// 一次消除批次中，某颜色至少被消去 1 格时触发（类型 1–5）。用于护盾「各颜色至少消 1」等统计。
    /// </summary>
    public event Action<int> OnMatchEliminatedGemType;

    /// <summary>
    /// 冻结技能：当<strong>成功</strong>操作次数满足 <c>count % 6 == 3</c> 时触发，<b>整场最多 2 次</b>（通常第 3、9 手）。
    /// 随机取 2 个 2×2 区域，将区域内格子全部改为冻结。参数为（触发时的成功操作次数，本次写入的格子数，重叠区域会去重）。
    /// </summary>
    public event Action<int, int> OnFreezeSkillTriggered;

    [Header("Prefabs (5 colors, index 0 = type 1)")]
    [SerializeField] private GameObject[] gemPrefabs = new GameObject[5];
    [SerializeField] private GameObject freezePrefab;

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
    [Tooltip("仅作用于邻格缩小：拉力小于该距离（×cellSize）时邻格保持原大小")]
    [SerializeField] private float dragScaleDeadZone = 0.06f;
    [Tooltip("按下并拖动时选中块立即使用的缩放（基础放大）")]
    [SerializeField] private float dragSelectedScale = 1.12f;
    [Tooltip("沿拉力拉到最大时选中块的缩放（应 ≥ 基础；相等则距离不再放大）")]
    [SerializeField] private float dragSelectedScaleMax = 1.18f;
    [Tooltip("达到最大拉力时相邻格棋子的缩放（受死区影响）")]
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
    [Tooltip("冻结块被清除时的粒子预制体（可选）")]
    [SerializeField] private GameObject freezeClearVfxPrefab;
    [SerializeField] private int freezeClearVfxPoolSize = 6;
    [SerializeField] private float eliminationShrinkDuration = 0.18f;
    [Tooltip("回收等待 = 估算播放时长 + 该值；并受「最长回收等待」限制")]
    [SerializeField] private float matchVfxReleasePadding = 0.08f;
    [SerializeField] private float matchVfxRecycleMaxSeconds = 8f;
    [Header("Elimination score (linear)")]
    [Tooltip("该颜色在一次消除中合计格数 = 3 时的得分")]
    [SerializeField] private int eliminationScoreAt3 = 3;
    [Tooltip("该颜色在一次消除中合计格数 = 8 时的得分；3~8 之间线性插值，>8 按同斜率外推")]
    [SerializeField] private int eliminationScoreAt8 = 18;
    [SerializeField] private GameObject scoreTextPrefab;
    [SerializeField] private int scoreTextPoolSize = 16;
    [SerializeField] private float scoreTextLifetime = 0.9f;
    [SerializeField] private float scoreTextScaleFactor = 0.2f;

    [Header("Damage fly swarm (optional)")]
    [SerializeField] private Match3DamageSwarmManager damageSwarm;

    [Header("Gravity visual (after each sim step)")]
    [SerializeField] private float gravityPassDuration = 0.09f;
    [SerializeField] private float newGemFallDuration = 0.14f;
    [Tooltip("仅新补块：从目标格心向上偏移 ≈ cellSize × 该因子再落到格心（盘面内已有棋子从当前位置落到目标）")]
    [SerializeField] private float gravityFallHeightFactor = 0.55f;

    private Match3BoardSimulator _sim;
    private Match3GemPool _pool;
    private Match3VfxPool _matchVfxPool;
    private Match3VfxPool _freezeVfxPool;
    private readonly Queue<GameObject> _freezeViewPool = new Queue<GameObject>();
    private readonly Queue<Match3ScoreTextView> _scoreTextPool = new Queue<Match3ScoreTextView>();
    private readonly int[] _colorScores = new int[Match3BoardSimulator.MaxType + 1];
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

    private int _freezeSkillReleaseCount;

    public Match3BoardSimulator Simulator => _sim;

    private void RegisterSuccessfulPlayerMove()
    {
        SuccessfulPlayerMoveCount++;
        Debug.Log($"[Match3] 成功操作次数={SuccessfulPlayerMoveCount}");
        if (SuccessfulPlayerMoveCount == 1)
            OnBattleStarted?.Invoke();
        TryReleaseFreezeSkillIfDue();
    }

    /// <summary>成功操作次数 <c>% 6 == 3</c> 时，随机 2 个 2×2 区域全部置冻并触发 <see cref="OnFreezeSkillTriggered"/>；整场最多 2 次。</summary>
    private void TryReleaseFreezeSkillIfDue()
    {
        if (_freezeSkillReleaseCount >= 2)
            return;

        int m = SuccessfulPlayerMoveCount;
        if (m % 6 != 3)
            return;

        int n = Match3BoardSimulator.Size;
        if (n < 2)
            return;

        int span = n - 1;
        int numTops = span * span;
        if (numTops <= 0)
            return;

        int a = UnityEngine.Random.Range(0, numTops);
        int b = UnityEngine.Random.Range(0, numTops);
        while (b == a && numTops > 1)
            b = UnityEngine.Random.Range(0, numTops);

        static void IndexToTopLeft(int index, int span, out int br, out int bc)
        {
            br = index / span;
            bc = index % span;
        }

        var cells = new HashSet<(int r, int c)>();
        void Add2x2(int topIndex)
        {
            IndexToTopLeft(topIndex, span, out int br, out int bc);
            for (int dr = 0; dr < 2; dr++)
            {
                for (int dc = 0; dc < 2; dc++)
                    cells.Add((br + dr, bc + dc));
            }
        }

        Add2x2(a);
        Add2x2(b);

        int frozen = 0;
        foreach (var (r, c) in cells)
        {
            SetFreezeAt(c, r);
            frozen++;
        }

        _freezeSkillReleaseCount++;
        Debug.Log($"[Match3] 冻结技能触发 ({_freezeSkillReleaseCount}/2) 成功操作次数={m} 2×2区域数=2 去重后冻结格子数={frozen}");
        OnFreezeSkillTriggered?.Invoke(m, frozen);
    }

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
        if (freezeClearVfxPrefab != null)
            _freezeVfxPool = new Match3VfxPool(freezeClearVfxPrefab, poolRoot, freezeClearVfxPoolSize);
        PrewarmScoreTextPool();
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
        gem.transform.localScale = Vector3.one * dragSelectedScale;
    }

    private void UpdateDrag()
    {
        if (_startGem == null)
            return;

        if (TryGetPointerBoardLocal(out Vector2 local))
            _lastValidLocalDuringDrag = local;

        Vector2 center = CellCenterLocal(_startRow, _startCol);
        Vector2 delta = _lastValidLocalDuringDrag - center;

        float maxPullWorld = dragMaxPull * cellSize;
        float deadWorld = dragScaleDeadZone * cellSize;

        if (!TryResolveNeighborFromDelta(delta, out int nr, out int nc))
        {
            Vector2 offset = Vector2.ClampMagnitude(delta, maxPullWorld);
            _startGem.transform.localPosition = CellLocalPosition(_startRow, _startCol) + (Vector3)offset;
            ApplyDragScalesForDistance(offset.magnitude, deadWorld, maxPullWorld, null);
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

        Vector2 axis = GetSwapAxisLocal(_startRow, _startCol, nr, nc);
        float pull = Vector2.Dot(delta, axis);
        pull = Mathf.Clamp(pull, 0f, maxPullWorld);
        Vector2 offsetAxis = axis * pull;

        _startGem.transform.localPosition = CellLocalPosition(_startRow, _startCol) + (Vector3)offsetAxis;
        if (_neighborGem != null)
            PlaceCell(_neighborGem.transform, _neighborRow, _neighborCol);

        ApplyDragScalesForDistance(pull, deadWorld, maxPullWorld, _neighborGem);
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

        RegisterSuccessfulPlayerMove();

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

        while (true)
        {
            // A) 纵向下落/补块后，持续做“检测->消除”，直到这一阶段无可消。
            while (true)
            {
                var matches = _sim.CollectMatches();
                if (matches.Count == 0)
                    break;

                yield return CoEliminationShrinkAndParticles(matches);
                yield return new WaitForSeconds(Mathf.Max(0f, afterClearPause));
                yield return CoVerticalSettleAndRefillWithVisual();
                yield return new WaitForSeconds(Mathf.Max(0f, afterSettlePause));
            }

            // B) 纵向阶段稳定后，执行一次水平补全；之后再检测并回到 A)。
            bool horizontalMoved = false;
            {
                int[,] gridBefore = SnapshotGrid();
                Match3GemView[,] cellsBefore = SnapshotCells();
                bool h = _sim.StepHorizontalCompact();
                if (h)
                {
                    horizontalMoved = true;
                    ApplyHorizontalCellMapping(cellsBefore, gridBefore);
                    yield return CoTweenGemsToLogicalCells(gravityPassDuration);
                    yield return new WaitForSeconds(Mathf.Max(0f, afterSettlePause));
                }
            }

            // 水平也不再变化，且当前无可消：结算结束。
            if (!horizontalMoved && !_sim.HasAnyMatch())
                break;
        }

        _inputLocked = false;
        _runningRoutine = null;
    }

    private IEnumerator CoEliminationShrinkAndParticles(HashSet<(int r, int c)> matches)
    {
        var freezeToClear = CollectAdjacentFreezeCells(matches);
        var colorCounts = new int[Match3BoardSimulator.MaxType + 1];
        var colorPosSums = new Vector3[Match3BoardSimulator.MaxType + 1];

        foreach (var (r, c) in matches)
        {
            if (_sim.IsCellLocked(r, c))
                continue;
            var gem = _cells[r, c];
            int type = _sim.GetCell(r, c);
            if (damageSwarm != null && type >= Match3BoardSimulator.MinType && type <= Match3BoardSimulator.MaxType)
                damageSwarm.SpawnOrbAtBoardLocal(CellLocalPosition(r, c), scoreTextScaleFactor, Match3GemPalette.GetAccentColorForGemType(type));
            if (type >= Match3BoardSimulator.MinType && type <= Match3BoardSimulator.MaxType)
            {
                colorCounts[type]++;
                colorPosSums[type] += CellLocalPosition(r, c);
            }
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
            ReleaseCellView(v.gameObject);
            _cells[r, c] = null;
        }

        ClearFreezeCells(freezeToClear);
        _sim.ClearMatchCells(matches);
        ApplyColorScoreForElimination(colorCounts, colorPosSums);
    }

    private void ApplyColorScoreForElimination(int[] colorCounts, Vector3[] colorPosSums)
    {
        for (int type = Match3BoardSimulator.MinType; type <= Match3BoardSimulator.MaxType; type++)
        {
            if (colorCounts[type] > 0)
                OnMatchEliminatedGemType?.Invoke(type);
        }

        for (int type = Match3BoardSimulator.MinType; type <= Match3BoardSimulator.MaxType; type++)
        {
            int cnt = colorCounts[type];
            if (cnt <= 0)
                continue;

            int add = ScoreFromEliminationCount(cnt);
            if (add <= 0)
                continue;

            _colorScores[type] += add;
            OnEliminationDamage?.Invoke(add, type);
            Vector3 p = colorPosSums[type] / cnt;
            SpawnScoreText(p, $"+{add}", type);
        }

        LogColorScores();
    }

    /// <summary>3 连以下 0 分；3~8 在 <see cref="eliminationScoreAt3"/> 与 <see cref="eliminationScoreAt8"/> 间线性；更大消除按同斜率继续加分。</summary>
    private int ScoreFromEliminationCount(int count)
    {
        if (count < 3)
            return 0;

        int lo = eliminationScoreAt3;
        int hi = eliminationScoreAt8;
        if (hi < lo)
            (lo, hi) = (hi, lo);

        const int span = 8 - 3;
        float step = (hi - lo) / (float)span;
        int s = count <= 8
            ? Mathf.RoundToInt(lo + (count - 3) * step)
            : Mathf.RoundToInt(hi + (count - 8) * step);
        // 三连至少应有分，否则 +分飘字会被跳过；若 Inspector 把两端都配成 0，这里兜底为 1
        if (s < 1)
            return 1;
        return s;
    }

    private void LogColorScores()
    {
        var sb = new StringBuilder();
        sb.Append("[Match3] Color scores ");
        for (int t = Match3BoardSimulator.MinType; t <= Match3BoardSimulator.MaxType; t++)
        {
            if (t > Match3BoardSimulator.MinType) sb.Append(" | ");
            sb.Append("T").Append(t).Append(':').Append(_colorScores[t]);
        }
        Debug.Log(sb.ToString());
    }

    private HashSet<(int r, int c)> CollectAdjacentFreezeCells(HashSet<(int r, int c)> matches)
    {
        var set = new HashSet<(int r, int c)>();
        foreach (var (r, c) in matches)
        {
            TryMarkFreeze(r - 1, c, set);
            TryMarkFreeze(r + 1, c, set);
            TryMarkFreeze(r, c - 1, set);
            TryMarkFreeze(r, c + 1, set);
        }

        return set;
    }

    private void TryMarkFreeze(int r, int c, HashSet<(int r, int c)> set)
    {
        if ((uint)r >= Match3BoardSimulator.Size || (uint)c >= Match3BoardSimulator.Size)
            return;
        if (!_sim.IsCellLocked(r, c))
            return;
        set.Add((r, c));
    }

    private void ClearFreezeCells(HashSet<(int r, int c)> freezeToClear)
    {
        foreach (var (r, c) in freezeToClear)
        {
            var v = _cells[r, c];
            if (v != null)
                SpawnFreezeClearVfx(r, c);

            if (v != null)
            {
                ReleaseCellView(v.gameObject);
                _cells[r, c] = null;
            }

            _sim.SetLocked(r, c, false);
            _sim.SetCell(r, c, 0);
        }
    }

    private void SpawnFreezeClearVfx(int row, int col)
    {
        if (_freezeVfxPool == null)
            return;

        var fx = _freezeVfxPool.Get();
        fx.transform.SetParent(boardRoot, false);
        fx.transform.localPosition = CellLocalPosition(row, col);
        fx.transform.localRotation = Quaternion.identity;

        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);

        float wait = Mathf.Min(
            Match3VfxPool.EstimateMaxPlayDuration(fx) + matchVfxReleasePadding,
            Mathf.Max(0.1f, matchVfxRecycleMaxSeconds));
        StartCoroutine(CoReleaseVfx(fx, wait, _freezeVfxPool));
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
        StartCoroutine(CoReleaseVfx(fx, wait, _matchVfxPool));
    }

    private IEnumerator CoReleaseVfx(GameObject instance, float waitSeconds, Match3VfxPool targetPool)
    {
        yield return new WaitForSeconds(waitSeconds);
        if (targetPool != null && instance != null)
            targetPool.Release(instance);
    }

    private void PrewarmScoreTextPool()
    {
        if (scoreTextPrefab == null)
            return;

        int n = Mathf.Max(0, scoreTextPoolSize);
        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(scoreTextPrefab, poolRoot, false);
            var view = go.GetComponent<Match3ScoreTextView>();
            if (view == null)
                view = go.AddComponent<Match3ScoreTextView>();
            go.SetActive(false);
            _scoreTextPool.Enqueue(view);
        }
    }

    private void SpawnScoreText(Vector3 localPos, string text, int gemType)
    {
        if (scoreTextPrefab == null)
            return;

        Match3ScoreTextView view = _scoreTextPool.Count > 0
            ? _scoreTextPool.Dequeue()
            : CreateScoreTextViewInstance();
        GameObject go = view.gameObject;
        // 顺序：创建/取出 -> 写入文本 -> 激活 -> 定时休眠回池
        if (go.activeSelf)
            go.SetActive(false);
        go.transform.SetParent(boardRoot, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scoreTextScaleFactor);

        bool assigned = view.SetScoreText(text);
        view.SetTextColor(Match3GemPalette.GetAccentColorForGemType(gemType));
        if (!assigned)
            Debug.LogWarning("[Match3] scoreTextPrefab has no supported text component in Match3ScoreTextView.");

        go.SetActive(true);
        StartCoroutine(CoRecycleScoreText(go, scoreTextLifetime));
    }

    private IEnumerator CoRecycleScoreText(GameObject go, float wait)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, wait));
        if (go == null)
            yield break;

        go.SetActive(false);
        go.transform.SetParent(poolRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var view = go.GetComponent<Match3ScoreTextView>();
        if (view == null)
            view = go.AddComponent<Match3ScoreTextView>();
        _scoreTextPool.Enqueue(view);
    }

    private Match3ScoreTextView CreateScoreTextViewInstance()
    {
        var go = Instantiate(scoreTextPrefab, poolRoot, false);
        var view = go.GetComponent<Match3ScoreTextView>();
        if (view == null)
            view = go.AddComponent<Match3ScoreTextView>();
        return view;
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

    /// <summary>
    /// 仅做纵向阶段：下落到稳定，若还有空位则补块并继续，直到纵向稳定且无空位。
    /// 不做水平补全；水平阶段由上层流程单独触发。
    /// </summary>
    private IEnumerator CoVerticalSettleAndRefillWithVisual()
    {
        while (true)
        {
            bool verticalMoved;
            do
            {
                int[,] gridBefore = SnapshotGrid();
                Match3GemView[,] cellsBefore = SnapshotCells();
                verticalMoved = _sim.StepVerticalGravity();
                if (verticalMoved)
                {
                    ApplyVerticalCellMapping(cellsBefore, gridBefore);
                    yield return CoTweenGemsToLogicalCells(gravityPassDuration);
                }
            } while (verticalMoved);

            if (!_sim.HasAnyEmpty())
                break;

            int[,] gridSnapshot = SnapshotGrid();
            Match3GemView[,] cellsSnapshot = SnapshotCells();
            _sim.RefillEmptyCells();
            BuildCellsAfterRefill(cellsSnapshot, gridSnapshot);
            yield return CoTweenGemsToLogicalCells(newGemFallDuration);
        }
    }

    /// <summary>仅对「当前 localPosition 与逻辑格不一致」的棋子插值到格心（从当前位置落到目标，不整盘从上方放下）。</summary>
    private IEnumerator CoTweenGemsToLogicalCells(float duration)
    {
        const float eps = 1e-8f;
        var items = new List<(Transform tr, Vector3 start, Vector3 end, int row, int col)>();
        for (int r = 0; r < Match3BoardSimulator.Size; r++)
        {
            for (int c = 0; c < Match3BoardSimulator.Size; c++)
            {
                var v = _cells[r, c];
                if (v == null) continue;

                Transform tr = v.transform;
                Vector3 end = CellLocalPosition(r, c);
                Vector3 start = tr.localPosition;
                if ((start - end).sqrMagnitude < eps)
                    continue;

                items.Add((tr, start, end, r, c));
            }
        }

        float dur = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / dur);
            foreach (var (tr, start, end, _, _) in items)
                tr.localPosition = Vector3.Lerp(start, end, u);
            yield return null;
        }

        foreach (var (tr, _, _, row, col) in items)
            PlaceCell(tr, row, col);
    }

    private static int[,] SnapshotGrid(Match3BoardSimulator sim)
    {
        int n = Match3BoardSimulator.Size;
        var g = new int[n, n];
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                g[r, c] = sim.Grid[r, c];
        return g;
    }

    private int[,] SnapshotGrid() => SnapshotGrid(_sim);

    private Match3GemView[,] SnapshotCells()
    {
        int n = Match3BoardSimulator.Size;
        var a = new Match3GemView[n, n];
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                a[r, c] = _cells[r, c];
        return a;
    }

    /// <summary>与 <see cref="Match3BoardSimulator"/> 列栈顺序一致：自下而上收集可动棋子，再写入自下而上的非空目标格。</summary>
    private void ApplyVerticalCellMapping(Match3GemView[,] beforeCells, int[,] beforeGrid)
    {
        int n = Match3BoardSimulator.Size;
        for (int c = 0; c < n; c++)
        {
            var stack = new List<(int fromR, Match3GemView gem)>();
            for (int r = n - 1; r >= 0; r--)
            {
                if (_sim.IsCellLocked(r, c))
                    continue;
                if (beforeGrid[r, c] == 0)
                    continue;
                Match3GemView g = beforeCells[r, c];
                if (g == null)
                    continue;
                stack.Add((r, g));
            }

            var targetRows = new List<int>();
            for (int r = n - 1; r >= 0; r--)
            {
                if (_sim.IsCellLocked(r, c))
                    continue;
                if (_sim.GetCell(r, c) == 0)
                    continue;
                targetRows.Add(r);
            }

            for (int r = 0; r < n; r++)
            {
                if (_sim.IsCellLocked(r, c))
                    continue;
                _cells[r, c] = null;
            }

            for (int r = 0; r < n; r++)
            {
                if (!_sim.IsCellLocked(r, c))
                    continue;
                _cells[r, c] = beforeCells[r, c];
            }

            if (stack.Count != targetRows.Count)
            {
                Debug.LogError($"[Match3] Vertical column {c} stack/target count mismatch ({stack.Count}/{targetRows.Count}). Snapping.");
                SyncFromGrid();
                return;
            }

            for (int i = 0; i < stack.Count; i++)
            {
                int toR = targetRows[i];
                Match3GemView gem = stack[i].gem;
                _cells[toR, c] = gem;
                gem.Setup(_sim.GetCell(toR, c));
            }
        }
    }

    /// <summary>与模拟器行内左紧凑顺序一致：自左而右收集可动棋子，再写入自左而非空目标列。</summary>
    private void ApplyHorizontalCellMapping(Match3GemView[,] beforeCells, int[,] beforeGrid)
    {
        int n = Match3BoardSimulator.Size;
        for (int r = 0; r < n; r++)
        {
            var stack = new List<(int fromC, Match3GemView gem)>();
            for (int c = 0; c < n; c++)
            {
                if (_sim.IsCellLocked(r, c))
                    continue;
                if (beforeGrid[r, c] == 0)
                    continue;
                Match3GemView g = beforeCells[r, c];
                if (g == null)
                    continue;
                stack.Add((c, g));
            }

            var targetCols = new List<int>();
            for (int c = 0; c < n; c++)
            {
                if (_sim.IsCellLocked(r, c))
                    continue;
                if (_sim.GetCell(r, c) == 0)
                    continue;
                targetCols.Add(c);
            }

            for (int c = 0; c < n; c++)
            {
                if (_sim.IsCellLocked(r, c))
                    continue;
                _cells[r, c] = null;
            }

            for (int c = 0; c < n; c++)
            {
                if (!_sim.IsCellLocked(r, c))
                    continue;
                _cells[r, c] = beforeCells[r, c];
            }

            if (stack.Count != targetCols.Count)
            {
                Debug.LogError($"[Match3] Horizontal row {r} stack/target count mismatch ({stack.Count}/{targetCols.Count}). Snapping.");
                SyncFromGrid();
                return;
            }

            for (int i = 0; i < stack.Count; i++)
            {
                int toC = targetCols[i];
                Match3GemView gem = stack[i].gem;
                _cells[r, toC] = gem;
                gem.Setup(_sim.GetCell(r, toC));
            }
        }
    }

    /// <summary>补块后：原非空格沿用原视图；新出现的类型从池取出并从格心上方落下。</summary>
    private void BuildCellsAfterRefill(Match3GemView[,] cellsBefore, int[,] gridBefore)
    {
        int n = Match3BoardSimulator.Size;
        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
            {
                int t = _sim.GetCell(r, c);
                if (t == 0)
                {
                    if (_cells[r, c] != null)
                    {
                        ReleaseCellView(_cells[r, c].gameObject);
                        _cells[r, c] = null;
                    }

                    continue;
                }

                if (gridBefore[r, c] != 0)
                {
                    Match3GemView keep = cellsBefore[r, c];
                    _cells[r, c] = keep;
                    if (keep != null)
                        keep.Setup(t);
                    continue;
                }

                if (_cells[r, c] != null)
                {
                    ReleaseCellView(_cells[r, c].gameObject);
                    _cells[r, c] = null;
                }

                GameObject go = _pool.Get(t);
                if (go == null)
                {
                    Debug.LogError($"[Match3] Missing prefab for gem type {t}.");
                    continue;
                }

                go.transform.SetParent(boardRoot, false);
                var view = go.GetComponent<Match3GemView>();
                _cells[r, c] = view;
                view.Setup(t);
                Vector3 end = CellLocalPosition(r, c);
                float rowT = (Match3BoardSimulator.Size - r) / (float)Match3BoardSimulator.Size;
                float lift = cellSize * gravityFallHeightFactor * (rowT + 0.12f);
                go.transform.localPosition = end + Vector3.up * lift;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>与输入/UI 无关：原子 TrySwap + Sync（供测试或跳过动画的路径）。</summary>
    public bool TryCommitSwap(int rowA, int colA, int rowB, int colB)
    {
        if (!_sim.TrySwap(rowA, colA, rowB, colB))
            return false;
        RegisterSuccessfulPlayerMove();
        SyncFromGrid();
        return true;
    }

    /// <summary>
    /// 选中块：按下已是 dragSelectedScale，再随距离在 dragSelectedScaleMax 间过渡（无死区）。邻格：死区外才从 1 缩到 dragNeighborScale。
    /// </summary>
    private void ApplyDragScalesForDistance(float distance, float deadWorld, float maxPullWorld, Match3GemView neighbor)
    {
        if (_startGem == null)
            return;

        float tSel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / Mathf.Max(1e-4f, maxPullWorld)));
        float hi = Mathf.Max(dragSelectedScale, dragSelectedScaleMax);
        float selectedS = Mathf.Lerp(dragSelectedScale, hi, tSel);
        _startGem.transform.localScale = Vector3.one * selectedS;

        if (neighbor != null)
        {
            float span = Mathf.Max(1e-4f, maxPullWorld - deadWorld);
            float rawN = distance <= deadWorld ? 0f : Mathf.Clamp01((distance - deadWorld) / span);
            float tN = Mathf.SmoothStep(0f, 1f, rawN);
            float neighborS = Mathf.Lerp(1f, dragNeighborScale, tN);
            neighbor.transform.localScale = Vector3.one * neighborS;
        }
    }

    /// <summary>从起点格心指向邻格格心的单位向量（与 <see cref="CellLocalPosition"/> 一致），用于拉力投影。</summary>
    private Vector2 GetSwapAxisLocal(int sr, int sc, int nr, int nc)
    {
        Vector2 d = CellCenterLocal(nr, nc) - CellCenterLocal(sr, sc);
        float m = d.magnitude;
        if (m < 1e-5f)
            return Vector2.right;
        return d / m;
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
                if (_sim.IsCellLocked(r, c))
                {
                    EnsureFreezeViewAt(r, c);
                    continue;
                }

                int t = _sim.Grid[r, c];
                var existing = _cells[r, c];

                if (t == 0)
                {
                    if (existing != null)
                    {
                        ReleaseCellView(existing.gameObject);
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
                    ReleaseCellView(existing.gameObject);
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

    private void EnsureFreezeViewAt(int row, int col)
    {
        var existing = _cells[row, col];
        if (existing != null && existing.GetComponent<Match3FreezeView>() != null)
        {
            PlaceCell(existing.transform, row, col);
            return;
        }

        if (existing != null)
        {
            ReleaseCellView(existing.gameObject);
            _cells[row, col] = null;
        }

        if (freezePrefab == null)
        {
            Debug.LogError("[Match3] freezePrefab is not assigned.");
            return;
        }

        GameObject go = GetFreezeViewInstance();
        var view = go.GetComponent<Match3GemView>();
        if (view == null)
        {
            Debug.LogError("[Match3] freezePrefab requires Match3GemView.");
            Destroy(go);
            return;
        }

        _cells[row, col] = view;
        PlaceCell(go.transform, row, col);
    }

    private static bool IsFreezeView(GameObject go)
    {
        return go != null && go.GetComponent<Match3FreezeView>() != null;
    }

    private GameObject GetFreezeViewInstance()
    {
        if (_freezeViewPool.Count > 0)
        {
            var pooled = _freezeViewPool.Dequeue();
            if (pooled != null)
            {
                pooled.SetActive(true);
                pooled.transform.SetParent(boardRoot, false);
                pooled.transform.localPosition = Vector3.zero;
                pooled.transform.localRotation = Quaternion.identity;
                pooled.transform.localScale = Vector3.one;
                return pooled;
            }
        }

        return Instantiate(freezePrefab, boardRoot, false);
    }

    private void ReleaseCellView(GameObject go)
    {
        if (go == null)
            return;

        if (IsFreezeView(go))
        {
            go.SetActive(false);
            go.transform.SetParent(poolRoot, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _freezeViewPool.Enqueue(go);
            return;
        }

        _pool.Release(go);
    }

    /// <summary>
    /// 将坐标 (x,y) 置为冻结块（x=列, y=行）。冻结块不参与消除且不可移动。
    /// </summary>
    public void SetFreezeAt(int x, int y)
    {
        int row = y;
        int col = x;
        if ((uint)row >= Match3BoardSimulator.Size || (uint)col >= Match3BoardSimulator.Size)
            return;

        _sim.SetLocked(row, col, false);
        if (_sim.GetCell(row, col) == 0)
            _sim.SetCell(row, col, Match3BoardSimulator.MinType);
        _sim.SetLocked(row, col, true);
        EnsureFreezeViewAt(row, col);
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

        if (eliminationScoreAt3 == 0 && eliminationScoreAt8 == 0)
            Debug.LogWarning("[Match3] eliminationScoreAt3 and eliminationScoreAt8 are both 0; 3-match score floors at 1 so +text still shows. Set positive endpoints for intended linear curve.");

        if (freezePrefab != null)
        {
            if (freezePrefab.GetComponent<Match3GemView>() == null)
                Debug.LogWarning("[Match3] freezePrefab should have a Match3GemView component.");
            if (freezePrefab.GetComponent<Match3FreezeView>() == null)
                Debug.LogWarning("[Match3] freezePrefab should have a Match3FreezeView component.");
        }

        if (freezeClearVfxPrefab != null)
        {
            if (freezeClearVfxPrefab.GetComponentInChildren<ParticleSystem>(true) == null)
                Debug.LogWarning("[Match3] freezeClearVfxPrefab should contain at least one ParticleSystem.");
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
