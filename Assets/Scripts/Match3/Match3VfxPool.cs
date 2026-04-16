using System.Collections.Generic;
using UnityEngine;

/// <summary>消除等粒子预制体的简单对象池；取用时激活，回收时 Stop+Clear 并挂回池根节点。</summary>
public sealed class Match3VfxPool
{
    private readonly GameObject _prefab;
    private readonly Transform _poolRoot;
    private readonly Queue<GameObject> _queue = new Queue<GameObject>();

    public Match3VfxPool(GameObject prefab, Transform poolRoot, int prewarmCount)
    {
        _prefab = prefab;
        _poolRoot = poolRoot;

        prewarmCount = Mathf.Max(0, prewarmCount);
        for (int i = 0; i < prewarmCount; i++)
        {
            var go = Object.Instantiate(_prefab, _poolRoot);
            go.SetActive(false);
            _queue.Enqueue(go);
        }
    }

    public GameObject Get()
    {
        GameObject go;
        if (_queue.Count > 0)
        {
            go = _queue.Dequeue();
            go.SetActive(true);
        }
        else
            go = Object.Instantiate(_prefab, _poolRoot);

        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }

        go.SetActive(false);
        go.transform.SetParent(_poolRoot, false);
        _queue.Enqueue(go);
    }

    /// <summary>用于协程等待：取所有子 ParticleSystem 的 duration + 起始寿命上界近似。</summary>
    public static float EstimateMaxPlayDuration(GameObject instance)
    {
        float maxT = 0f;
        foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            if (m.loop)
                return 8f;

            float dur = m.duration;
            var life = m.startLifetime;
            float lifeMax = Mathf.Max(life.constantMin, life.constantMax);
            if (lifeMax < 0.001f)
                lifeMax = 1f;
            float t = dur + lifeMax;
            if (t > maxT) maxT = t;
        }

        return maxT > 0.01f ? maxT : 1f;
    }
}
