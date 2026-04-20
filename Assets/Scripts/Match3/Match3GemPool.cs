using System.Collections.Generic;
using UnityEngine;

/// <summary>按颜色类型分池；类型 t 使用 prefabs[t-1]。</summary>
public sealed class Match3GemPool
{
    private readonly Transform _poolRoot;
    private readonly GameObject[] _prefabs;
    private readonly Queue<GameObject>[] _queues;

    public Match3GemPool(Transform poolRoot, GameObject[] prefabs)
    {
        _poolRoot = poolRoot;
        _prefabs = prefabs;
        int n = prefabs.Length;
        _queues = new Queue<GameObject>[n];
        for (int i = 0; i < n; i++)
            _queues[i] = new Queue<GameObject>();
    }

    public GameObject Get(int gemType)
    {
        int idx = gemType - 1;
        if ((uint)idx >= (uint)_prefabs.Length || _prefabs[idx] == null)
            return null;

        GameObject go;
        if (_queues[idx].Count > 0)
        {
            go = _queues[idx].Dequeue();
            go.SetActive(true);
        }
        else
            go = Object.Instantiate(_prefabs[idx], _poolRoot);

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var view = go.GetComponent<Match3GemView>();
        if (view != null)
            view.Setup(gemType);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        var view = go.GetComponent<Match3GemView>();
        int idx = view != null ? view.GemType - 1 : 0;
        if ((uint)idx >= (uint)_queues.Length)
            idx = 0;

        go.SetActive(false);
        go.transform.SetParent(_poolRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        _queues[idx].Enqueue(go);
    }
}
