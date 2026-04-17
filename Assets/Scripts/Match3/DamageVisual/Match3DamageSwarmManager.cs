using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伤害飞弹群：得分飘字消失时生成；在棋盘局部空间内做 N² 相互吸引 + 向 Boss 加速；
/// 合并与命中半径由 <see cref="Radius"/> 等模拟参数决定，<b>不使用</b> prefab 上放大的碰撞体尺寸（该碰撞体仅作视觉）。
/// </summary>
[DisallowMultipleComponent]
public sealed class Match3DamageSwarmManager : MonoBehaviour, IMatch3DamageProjectileSpawn
{
    /// <summary>伤害球命中 Boss 碰撞体时触发；伤害量由球体质量折算（合并越大伤害越高）。</summary>
    public event Action<int> OnBossDamagedByOrb;

    [Header("Refs")]
    [SerializeField] private Transform boardRoot;
    [SerializeField] private BossDamageHitFeedback bossHit;
    [SerializeField] private Transform poolRoot;

    [Header("Prefab / pool")]
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private int poolSize = 32;
    [SerializeField] private int maxActive = 64;

    [Header("Spawn")]
    [Tooltip("相对得分飘字缩放的比例（飞弹更小）")]
    [SerializeField] private float orbScaleVsScoreText = 0.35f;
    [SerializeField] private float scoreTextScaleFallback = 0.2f;
    [SerializeField] private float orbMinSpawnScale = 0.14f;
    [Tooltip("生成时沿棋盘局部空间指向 Boss 方向的初速度大小")]
    [SerializeField] private float spawnInitialSpeedTowardBoss = 2.8f;
    [Tooltip("生成时垂直于指向 Boss 方向的随机切向扰动")]
    [SerializeField] private float spawnRandomTangentSpeed = 0.35f;

    [Header("Unit ball size")]
    [Tooltip("单位球（质量=1）视觉整体缩放，Inspector 滑块调节")]
    [SerializeField, Range(0.2f, 3f)]
    private float unitBallSize = 1f;

    [Header("Sim (board-local space)")]
    [Tooltip("与距离平方成反比的 Boss 吸引强度")]
    [SerializeField] private float bossSeekAccel = 1100f;
    [SerializeField] private float bossSeekAccelMax = 560f;
    [Tooltip("加在 bossSeekAccel/d² 之后的常数 C，远距离 d 大时仍保持明显指向 Boss 的分量")]
    [SerializeField] private float bossSeekInverseSquareConstantC = 48f;
    [Tooltip("较大的指向 Boss 的恒定加速度（与 prefab 碰撞体大小无关）")]
    [SerializeField] private float bossConstantRadialAcceleration = 1900f;
    [SerializeField] private float mutualGravity = 88f;
    [SerializeField] private float mutualForceMax = 1040f;
    [SerializeField] private float damping = 0.992f;
    [SerializeField] private float mergeDistanceFactor = 0.92f;
    [SerializeField] private float radiusPerSqrtMass = 0.11f;
    [SerializeField] private float hitSlack = 0.08f;

    /// <summary>公开可调：飞弹相对得分飘字的缩放倍率。</summary>
    public float OrbScaleVsScoreText
    {
        get => orbScaleVsScoreText;
        set => orbScaleVsScoreText = Mathf.Max(0.01f, value);
    }

    /// <summary>公开可调：飞弹最小可见缩放。</summary>
    public float OrbMinSpawnScale
    {
        get => orbMinSpawnScale;
        set => orbMinSpawnScale = Mathf.Max(0.01f, value);
    }

    /// <summary>公开可调：指向 Boss 的恒定径向加速度（数值越大越“吸向”Boss）。</summary>
    public float BossConstantRadialAcceleration
    {
        get => bossConstantRadialAcceleration;
        set => bossConstantRadialAcceleration = Mathf.Max(0f, value);
    }

    /// <summary>单位球视觉缩放（与 Inspector 滑块 <see cref="unitBallSize"/> 同步）。</summary>
    public float UnitBallSize
    {
        get => unitBallSize;
        set => unitBallSize = Mathf.Clamp(value, 0.2f, 3f);
    }

    private struct SimOrb
    {
        public GameObject View;
        public bool Alive;
        public Vector3 Pos;
        public Vector3 Vel;
        public float Mass;
        public float TextScaleRef;
    }

    private readonly List<SimOrb> _orbs = new List<SimOrb>(32);
    private readonly Queue<GameObject> _pool = new Queue<GameObject>();

    private void Awake()
    {
        if (poolRoot == null)
            poolRoot = transform;

        if (orbPrefab == null)
        {
            Debug.LogWarning("[DamageSwarm] orbPrefab is null. Spawn requests will be ignored.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            var go = Instantiate(orbPrefab, poolRoot, false);
            if (go.GetComponent<Match3DamageOrbAgent>() == null)
                go.AddComponent<Match3DamageOrbAgent>();
            PrepareOrbForTransformDrive(go);
            go.SetActive(false);
            _pool.Enqueue(go);
        }
    }

    public void SpawnOrbAtBoardLocal(Vector3 boardLocalSpawnPosition, float referenceScaleForSize)
    {
        if (orbPrefab == null || boardRoot == null)
        {
            Debug.LogWarning($"[DamageSwarm] Skip spawn. orbPrefab={(orbPrefab != null)}, boardRoot={(boardRoot != null)}");
            return;
        }

        if (_orbs.Count >= maxActive)
        {
            Debug.LogWarning($"[DamageSwarm] Skip spawn. Active cap reached: {_orbs.Count}/{maxActive}");
            return;
        }

        GameObject go = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(orbPrefab, poolRoot, false);
        if (go.GetComponent<Match3DamageOrbAgent>() == null)
            go.AddComponent<Match3DamageOrbAgent>();

        go.transform.SetParent(boardRoot, false);
        go.transform.localRotation = Quaternion.identity;
        float refScale = referenceScaleForSize > 1e-4f ? referenceScaleForSize : scoreTextScaleFallback;
        float s = Mathf.Max(orbMinSpawnScale, refScale * orbScaleVsScoreText) * unitBallSize;
        go.transform.localScale = Vector3.one * s;
        go.transform.localPosition = boardLocalSpawnPosition;
        PrepareOrbForTransformDrive(go);
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.enabled = true;
            sr.sortingOrder = 400;
        }

        go.SetActive(true);

        var orb = new SimOrb
        {
            View = go,
            Alive = true,
            Pos = boardLocalSpawnPosition,
            Vel = ComputeInitialVelocityTowardBoss(boardLocalSpawnPosition),
            Mass = 1f,
            TextScaleRef = refScale
        };
        ApplyVisualScale(ref orb);
        _orbs.Add(orb);
    }

    private void FixedUpdate()
    {
        if (boardRoot == null || bossHit == null)
            return;

        AdoptActiveOrphanAgentsUnderBoard();

        if (_orbs.Count == 0)
            return;

        Collider2D bossCol = bossHit.HitCollider;
        if (bossCol == null)
        {
            Debug.LogWarning("[DamageSwarm] bossHit.HitCollider is null. Cannot detect hits.");
            return;
        }

        Vector3 bossLocal = boardRoot.InverseTransformPoint(bossHit.transform.position);
        float dt = Time.fixedDeltaTime;

        int n = _orbs.Count;
        var acc = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            if (!_orbs[i].Alive) continue;
            Vector3 toBoss = bossLocal - _orbs[i].Pos;
            float d = Mathf.Max(0.05f, toBoss.magnitude);
            Vector3 dir = toBoss / d;
            float invSq = bossSeekAccel / (d * d);
            float a = Mathf.Min(bossSeekAccelMax, invSq + bossSeekInverseSquareConstantC);
            acc[i] += dir * (a + bossConstantRadialAcceleration);
        }

        for (int i = 0; i < n; i++)
        {
            if (!_orbs[i].Alive) continue;
            for (int j = i + 1; j < n; j++)
            {
                if (!_orbs[j].Alive) continue;

                Vector3 delta = _orbs[j].Pos - _orbs[i].Pos;
                float dist = Mathf.Max(0.02f, delta.magnitude);
                Vector3 dir = delta / dist;
                float mag = mutualGravity * _orbs[i].Mass * _orbs[j].Mass / (dist * dist);
                mag = Mathf.Min(mutualForceMax, mag);
                Vector3 f = dir * mag;
                acc[i] += f / Mathf.Max(0.01f, _orbs[i].Mass);
                acc[j] -= f / Mathf.Max(0.01f, _orbs[j].Mass);
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (!_orbs[i].Alive) continue;
            var o = _orbs[i];
            o.Vel += acc[i] * dt;
            o.Vel *= damping;
            o.Pos += o.Vel * dt;
            o.View.transform.localPosition = o.Pos;
            _orbs[i] = o;
        }

        MergePass();
        BossHitPass(bossCol);
        CompactOrbs();
    }

    /// <summary>逻辑半径：合并判定等；与 prefab 上放大的 Collider 无关。</summary>
    private float Radius(float mass) =>
        Mathf.Max(0.04f, Mathf.Sqrt(Mathf.Max(0.01f, mass)) * radiusPerSqrtMass);

    private static void PrepareOrbForTransformDrive(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null)
            return;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
    }

    private void ApplyVisualScale(ref SimOrb o)
    {
        float baseS = Mathf.Max(orbMinSpawnScale, o.TextScaleRef * orbScaleVsScoreText) * unitBallSize;
        float s = baseS * Mathf.Sqrt(o.Mass);
        o.View.transform.localScale = Vector3.one * s;
    }

    /// <summary>
    /// 将 <see cref="boardRoot"/> 下已激活、带 <see cref="Match3DamageOrbAgent"/> 但未纳入模拟列表的实例并入 N² 计算。
    /// </summary>
    private void AdoptActiveOrphanAgentsUnderBoard()
    {
        AdoptRecursive(boardRoot);
    }

    private void AdoptRecursive(Transform t)
    {
        var agent = t.GetComponent<Match3DamageOrbAgent>();
        if (agent != null && agent.isActiveAndEnabled)
        {
            GameObject go = agent.gameObject;
            if (!IsViewTracked(go))
            {
                Vector3 pos = go.transform.localPosition;
                var orb = new SimOrb
                {
                    View = go,
                    Alive = true,
                    Pos = pos,
                    Vel = ComputeInitialVelocityTowardBoss(pos),
                    Mass = 1f,
                    TextScaleRef = scoreTextScaleFallback
                };
                ApplyVisualScale(ref orb);
                _orbs.Add(orb);
            }
        }

        for (int c = 0; c < t.childCount; c++)
            AdoptRecursive(t.GetChild(c));
    }

    private Vector3 ComputeInitialVelocityTowardBoss(Vector3 boardLocalPos)
    {
        Vector3 tangential = (Vector3)(UnityEngine.Random.insideUnitCircle * spawnRandomTangentSpeed);
        if (bossHit == null || boardRoot == null)
            return tangential;

        Vector3 bossLocal = boardRoot.InverseTransformPoint(bossHit.transform.position);
        Vector3 toBoss = bossLocal - boardLocalPos;
        float d = toBoss.magnitude;
        if (d < 1e-4f)
            return tangential;

        return toBoss / d * spawnInitialSpeedTowardBoss + tangential;
    }

    private bool IsViewTracked(GameObject go)
    {
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i].Alive && _orbs[i].View == go)
                return true;
        }

        return false;
    }

    private void MergePass()
    {
        while (TryMergeOnce()) { }
    }

    private bool TryMergeOnce()
    {
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (!_orbs[i].Alive) continue;
            float ri = Radius(_orbs[i].Mass);
            for (int j = i + 1; j < _orbs.Count; j++)
            {
                if (!_orbs[j].Alive) continue;
                float rj = Radius(_orbs[j].Mass);
                float dist = Vector3.Distance(_orbs[i].Pos, _orbs[j].Pos);
                if (dist > (ri + rj) * mergeDistanceFactor)
                    continue;

                var a = _orbs[i];
                var b = _orbs[j];
                float ma = a.Mass;
                float mb = b.Mass;
                float mSum = ma + mb;
                Vector3 mergedPos = (a.Pos * ma + b.Pos * mb) / mSum;
                a.Vel = (a.Vel * ma + b.Vel * mb) / mSum;
                a.Mass = mSum;
                a.TextScaleRef = (a.TextScaleRef * ma + b.TextScaleRef * mb) / mSum;
                a.Pos = mergedPos;
                a.View.transform.localPosition = a.Pos;
                ApplyVisualScale(ref a);

                ReleaseView(b.View);
                b.Alive = false;
                _orbs[j] = b;
                _orbs[i] = a;
                return true;
            }
        }

        return false;
    }

    private void BossHitPass(Collider2D bossCol)
    {
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (!_orbs[i].Alive) continue;
            var o = _orbs[i];
            float r = Radius(o.Mass);
            Vector2 world = boardRoot.TransformPoint(o.Pos);
            Vector2 closest = bossCol.ClosestPoint(world);
            if ((world - closest).sqrMagnitude <= (r + hitSlack) * (r + hitSlack))
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(o.Mass));
                OnBossDamagedByOrb?.Invoke(dmg);
                bossHit.PlayHitFlash();
                ReleaseView(o.View);
                o.Alive = false;
                _orbs[i] = o;
            }
        }
    }

    private void CompactOrbs()
    {
        for (int i = _orbs.Count - 1; i >= 0; i--)
        {
            if (!_orbs[i].Alive)
                _orbs.RemoveAt(i);
        }
    }

    private void ReleaseView(GameObject go)
    {
        if (go == null)
            return;
        go.SetActive(false);
        go.transform.SetParent(poolRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _pool.Enqueue(go);
    }

    private void OnDisable()
    {
        for (int i = 0; i < _orbs.Count; i++)
        {
            if (_orbs[i].View != null)
                ReleaseView(_orbs[i].View);
        }

        _orbs.Clear();
    }
}
