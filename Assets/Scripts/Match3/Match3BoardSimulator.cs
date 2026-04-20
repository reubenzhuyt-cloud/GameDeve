using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 9x9 三消纯数据模拟：类型 1–5，0 表示空位。
/// 行 0 在顶部，行 8 在底部；纵向下落到底部后，按行向左紧凑填平行间空洞，再重复纵向下落；
/// 物理稳定后对剩余空位补随机块，循环直到全盘稳定；最后检测匹配并消除连锁。
/// </summary>
public sealed class Match3BoardSimulator
{
    public const int Size = 9;
    public const int MinType = 1;
    public const int MaxType = 5;

    private readonly int[,] _grid = new int[Size, Size];
    private readonly bool[,] _locked = new bool[Size, Size];

    public int[,] Grid => _grid;
    public bool[,] Locked => _locked;

    public void SetLocked(int row, int col, bool locked)
    {
        if (!IsInside(row, col)) return;
        _locked[row, col] = locked;
    }

    public void ClearLocks()
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                _locked[r, c] = false;
    }

    public int GetCell(int row, int col)
    {
        if (!IsInside(row, col)) return 0;
        return _grid[row, col];
    }

    public bool IsCellLocked(int row, int col)
    {
        if (!IsInside(row, col)) return false;
        return _locked[row, col];
    }

    /// <summary>写入格子（0=空，1–MaxType）；锁定格忽略。供测试与关卡数据。</summary>
    public void SetCell(int row, int col, int value)
    {
        if (!IsInside(row, col)) return;
        if (_locked[row, col]) return;
        if (value < 0) value = 0;
        if (value > MaxType) value = MaxType;
        _grid[row, col] = value;
    }

    /// <summary>完整复制盘面与锁定标记（含另一实例的 private 数组）。</summary>
    public void CopyStateFrom(Match3BoardSimulator source)
    {
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                _grid[r, c] = source._grid[r, c];
                _locked[r, c] = source._locked[r, c];
            }
        }
    }

    /// <summary>随机初始化，尽量不含初始三连；失败则重试。</summary>
    public void RandomInitNoMatches(int maxAttempts = 200)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                {
                    if (_locked[r, c]) continue;
                    _grid[r, c] = RandomType();
                }

            if (!HasAnyMatch() && HasAvailableSwap()) return;
        }

        Debug.LogWarning("[Match3] RandomInitNoMatches: max attempts reached, board may contain matches.");
    }

    public bool TrySwap(int rowA, int colA, int rowB, int colB)
    {
        if (!IsInside(rowA, colA) || !IsInside(rowB, colB)) return false;
        if (!IsAdjacent(rowA, colA, rowB, colB)) return false;
        if (_locked[rowA, colA] || _locked[rowB, colB]) return false;

        SwapCells(rowA, colA, rowB, colB);
        if (!HasAnyMatch())
        {
            SwapCells(rowA, colA, rowB, colB);
            return false;
        }

        ResolveUntilStable();
        return true;
    }

    /// <summary>若交换这两格（不写入盘面）是否会形成可消除匹配。</summary>
    public bool WouldMatchAfterSwap(int rowA, int colA, int rowB, int colB)
    {
        if (!IsInside(rowA, colA) || !IsInside(rowB, colB)) return false;
        if (!IsAdjacent(rowA, colA, rowB, colB)) return false;
        if (_locked[rowA, colA] || _locked[rowB, colB]) return false;
        return SwapWouldMatch(rowA, colA, rowB, colB);
    }

    /// <summary>仅交换两格数值（不检测、不消除、不下落）。表现层动画结束后再走消除流程。</summary>
    public bool ApplySwapOnly(int rowA, int colA, int rowB, int colB)
    {
        if (!IsInside(rowA, colA) || !IsInside(rowB, colB)) return false;
        if (!IsAdjacent(rowA, colA, rowB, colB)) return false;
        if (_locked[rowA, colA] || _locked[rowB, colB]) return false;
        SwapCells(rowA, colA, rowB, colB);
        return true;
    }

    /// <summary>将匹配集合中的非锁定格置为空（不执行下落与补块）。</summary>
    public void ClearMatchCells(HashSet<(int r, int c)> matches)
    {
        foreach (var (r, c) in matches)
        {
            if (!IsInside(r, c)) continue;
            if (_locked[r, c]) continue;
            _grid[r, c] = 0;
        }
    }

    /// <summary>消除所有匹配并反复结算，直到没有匹配为止。</summary>
    public void ResolveUntilStable()
    {
        while (true)
        {
            var matches = CollectMatches();
            if (matches.Count == 0) break;

            foreach (var (r, c) in matches)
                if (!_locked[r, c])
                    _grid[r, c] = 0;

            SettlePhysicsAndRefill();
        }
    }

    /// <summary>仅物理：下落 → 横向填平 → … → 补块，直到稳定。</summary>
    public void SettlePhysicsAndRefill()
    {
        while (true)
        {
            bool moved;
            do
            {
                moved = ApplyVerticalGravity() | ApplyHorizontalCompactLeft();
            } while (moved);

            if (!HasAnyEmpty()) break;

            RefillEmptyCellsRandom();
        }
    }

    /// <summary>单次纵向重力（一轮列内下落），有变化返回 true。供表现层分步动画。</summary>
    public bool StepVerticalGravity() => ApplyVerticalGravity();

    /// <summary>单次横向左紧凑。供表现层分步动画。</summary>
    public bool StepHorizontalCompact() => ApplyHorizontalCompactLeft();

    /// <summary>对所有空位随机补块（与 <see cref="SettlePhysicsAndRefill"/> 内补块一致）。</summary>
    public void RefillEmptyCells() => RefillEmptyCellsRandom();

    public bool HasAnyMatch() => CollectMatches().Count > 0;

    public HashSet<(int r, int c)> CollectMatches()
    {
        var set = new HashSet<(int r, int c)>();

        for (int r = 0; r < Size; r++)
        {
            int c = 0;
            while (c < Size)
            {
                if (_locked[r, c]) { c++; continue; }

                int v = _grid[r, c];
                if (v == 0) { c++; continue; }

                int start = c;
                while (c < Size && !_locked[r, c] && _grid[r, c] == v) c++;
                int len = c - start;
                if (len >= 3)
                    for (int k = start; k < c; k++)
                        set.Add((r, k));
            }
        }

        for (int c = 0; c < Size; c++)
        {
            int r = 0;
            while (r < Size)
            {
                if (_locked[r, c]) { r++; continue; }

                int v = _grid[r, c];
                if (v == 0) { r++; continue; }

                int start = r;
                while (r < Size && !_locked[r, c] && _grid[r, c] == v) r++;
                int len = r - start;
                if (len >= 3)
                    for (int k = start; k < r; k++)
                        set.Add((k, c));
            }
        }

        return set;
    }

    public void LogBoard(string tag = "")
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(tag))
            sb.AppendLine(tag);
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                if (_locked[r, c])
                    sb.Append('L');
                else
                {
                    int v = _grid[r, c];
                    sb.Append(v == 0 ? '.' : (char)('0' + v));
                }

                if (c < Size - 1) sb.Append(' ');
            }

            sb.AppendLine();
        }

        Debug.Log(sb.ToString());
    }

    private static bool IsInside(int r, int c) => (uint)r < Size && (uint)c < Size;

    private static bool IsAdjacent(int r1, int c1, int r2, int c2)
    {
        int dr = Mathf.Abs(r1 - r2);
        int dc = Mathf.Abs(c1 - c2);
        return dr + dc == 1;
    }

    private void SwapCells(int r1, int c1, int r2, int c2)
    {
        (_grid[r1, c1], _grid[r2, c2]) = (_grid[r2, c2], _grid[r1, c1]);
    }

    public bool HasAnyEmpty()
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                if (_grid[r, c] == 0 && !_locked[r, c])
                    return true;
        return false;
    }

    /// <summary>非锁定块在列内落到底部（row 较大的一侧）；锁定格为障碍占位。</summary>
    private bool ApplyVerticalGravity()
    {
        bool changed = false;

        for (int c = 0; c < Size; c++)
        {
            var before = new int[Size];
            for (int r = 0; r < Size; r++)
                before[r] = _grid[r, c];

            var stack = new List<int>(Size);
            for (int r = Size - 1; r >= 0; r--)
            {
                if (_locked[r, c]) continue;

                int v = _grid[r, c];
                if (v != 0)
                    stack.Add(v);
                _grid[r, c] = 0;
            }

            int write = Size - 1;
            for (int i = 0; i < stack.Count; i++)
            {
                while (write >= 0 && _locked[write, c])
                    write--;

                if (write < 0) break;

                _grid[write, c] = stack[i];
                write--;
            }

            for (int r = 0; r <= write; r++)
            {
                if (_locked[r, c]) continue;
                _grid[r, c] = 0;
            }

            for (int r = 0; r < Size; r++)
            {
                if (before[r] == _grid[r, c]) continue;
                changed = true;
                break;
            }
        }

        return changed;
    }

    /// <summary>每行将可移动块向左紧凑，保留相对顺序。</summary>
    private bool ApplyHorizontalCompactLeft()
    {
        bool changed = false;

        for (int r = 0; r < Size; r++)
        {
            var before = new int[Size];
            for (int c = 0; c < Size; c++)
                before[c] = _grid[r, c];

            var rowValues = new List<int>(Size);
            for (int c = 0; c < Size; c++)
            {
                if (_locked[r, c]) continue;

                int v = _grid[r, c];
                if (v != 0)
                    rowValues.Add(v);
                _grid[r, c] = 0;
            }

            int write = 0;
            for (int i = 0; i < rowValues.Count; i++)
            {
                while (write < Size && _locked[r, write])
                    write++;
                if (write >= Size) break;

                _grid[r, write] = rowValues[i];
                write++;
            }

            for (int c = 0; c < Size; c++)
            {
                if (before[c] == _grid[r, c]) continue;
                changed = true;
                break;
            }
        }

        return changed;
    }

    private void RefillEmptyCellsRandom()
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                if (_grid[r, c] == 0 && !_locked[r, c])
                    _grid[r, c] = RandomType();
    }

    private static int RandomType() => Random.Range(MinType, MaxType + 1);

    /// <summary>是否存在至少一组相邻交换能立即形成三连（用于开局可玩性检测）。</summary>
    public bool HasAvailableSwap()
    {
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                if (_locked[r, c]) continue;
                if (c + 1 < Size && !_locked[r, c + 1] && SwapWouldMatch(r, c, r, c + 1))
                    return true;
                if (r + 1 < Size && !_locked[r + 1, c] && SwapWouldMatch(r, c, r + 1, c))
                    return true;
            }
        }

        return false;
    }

    private bool SwapWouldMatch(int r1, int c1, int r2, int c2)
    {
        SwapCells(r1, c1, r2, c2);
        bool m = HasAnyMatch();
        SwapCells(r1, c1, r2, c2);
        return m;
    }

    /// <summary>执行第一组能消除的相邻交换（含连锁结算）。</summary>
    public bool TryApplyFirstValidSwap()
    {
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                if (_locked[r, c]) continue;
                if (c + 1 < Size && !_locked[r, c + 1] && TrySwap(r, c, r, c + 1))
                    return true;
                if (r + 1 < Size && !_locked[r + 1, c] && TrySwap(r, c, r + 1, c))
                    return true;
            }
        }

        return false;
    }

    /// <summary>随机盘 → 打印初始 → 尝试一次有效交换并打印结果（仅 Debug.Log）。</summary>
    public static void RunDemoToConsole()
    {
        var sim = new Match3BoardSimulator();
        sim.RandomInitNoMatches();
        sim.LogBoard("--- Match3 Initial ( . = empty, 1–5 = types, L = locked ) ---");
        bool ok = sim.TryApplyFirstValidSwap();
        Debug.Log($"[Match3] First valid swap applied: {ok}");
        sim.LogBoard("--- After swap + cascades ---");
    }
}
