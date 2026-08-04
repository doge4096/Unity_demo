using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池 — 复用频繁生成/销毁的 GameObject（弹道、敌人、特效等）
/// 用法：ObjectPool pool = new ObjectPool(prefab, 10);
///       var obj = pool.Get();  pool.Return(obj);
/// </summary>
[System.Serializable]
public class ObjectPool
{
    public GameObject Prefab { get; private set; }
    public int Capacity { get; private set; }

    private readonly Queue<GameObject> _pool = new();
    private Transform _parent;

    /// <summary>创建对象池</summary>
    /// <param name="prefab">预制体</param>
    /// <param name="initialSize">预实例化数量</param>
    /// <param name="parent">池内对象的父节点（可选）</param>
    public ObjectPool(GameObject prefab, int initialSize = 10, Transform parent = null)
    {
        Prefab = prefab;
        Capacity = initialSize;
        _parent = parent;

        // 创建池的根节点便于层级管理
        if (_parent == null)
        {
            var root = new GameObject($"[Pool] {prefab.name}");
            _parent = root.transform;
        }

        // 预实例化
        for (int i = 0; i < initialSize; i++)
        {
            var obj = CreateNew();
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    /// <summary>从池中取出一个对象</summary>
    public GameObject Get()
    {
        GameObject obj;
        if (_pool.Count > 0)
        {
            obj = _pool.Dequeue();
        }
        else
        {
            // 池中无可用对象 → 扩容
            obj = CreateNew();
            Capacity++;
        }

        obj.SetActive(true);
        return obj;
    }

    /// <summary>将对象归还池中</summary>
    public void Return(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        obj.transform.SetParent(_parent);
        _pool.Enqueue(obj);
    }

    /// <summary>清空对象池（销毁所有实例）</summary>
    public void Clear()
    {
        foreach (var obj in _pool)
        {
            if (obj != null) Object.Destroy(obj);
        }
        _pool.Clear();
        Capacity = 0;
    }

    private GameObject CreateNew()
    {
        var obj = Object.Instantiate(Prefab, _parent);
        obj.name = Prefab.name;
        return obj;
    }
}
