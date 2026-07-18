using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic pool for Component-based prefab instances. Pre-warms a fixed number
/// of deactivated instances under a dedicated root transform and grows
/// dynamically when the pool runs dry. Plain C# class — not a MonoBehaviour.
/// </summary>
public class MonoBehaviourPool<T> : IPool<T> where T : Component
{
    private GameObject _prefab;
    private Transform _root;
    private readonly Stack<T> _stack = new Stack<T>();
    private readonly HashSet<T> _pooled = new HashSet<T>();

    private bool _initialized;

    /// <summary>
    /// Prepares the pool: validates the prefab, creates a root transform to
    /// parent pooled instances, and pre-instantiates <paramref name="count"/>
    /// deactivated instances.
    /// </summary>
    public void Init(GameObject prefab, int count)
    {
        if (prefab == null)
        {
            Debug.LogError($"MonoBehaviourPool<{typeof(T).Name}>: Init called with a null prefab.");
            return;
        }

        if (prefab.GetComponent<T>() == null)
        {
            Debug.LogError($"MonoBehaviourPool<{typeof(T).Name}>: prefab '{prefab.name}' has no {typeof(T).Name} component.");
            return;
        }

        _prefab = prefab;
        _root = new GameObject("Pool_" + typeof(T).Name).transform;
        _initialized = true;

        for (int i = 0; i < count; i++)
        {
            T instance = CreateInstance();
            instance.gameObject.SetActive(false);
            _stack.Push(instance);
            _pooled.Add(instance);
        }
    }

    public T Get()
    {
        if (!_initialized)
        {
            Debug.LogError($"MonoBehaviourPool<{typeof(T).Name}>: Get called before Init.");
            return null;
        }

        // Pop until we find a live instance, skipping destroyed entries.
        while (_stack.Count > 0)
        {
            T item = _stack.Pop();
            _pooled.Remove(item);

            if (item == null)
            {
                continue;
            }

            item.gameObject.SetActive(true);
            return item;
        }

        // Pool is empty — grow dynamically.
        T fresh = CreateInstance();
        fresh.gameObject.SetActive(true);
        return fresh;
    }

    public void Release(T item)
    {
        if (item == null)
        {
            Debug.LogError($"MonoBehaviourPool<{typeof(T).Name}>: Release called with a null item.");
            return;
        }

        if (_pooled.Contains(item))
        {
            Debug.LogError($"MonoBehaviourPool<{typeof(T).Name}>: '{item.name}' was already released.");
            return;
        }

        item.gameObject.SetActive(false);

        if (_root != null)
        {
            item.transform.SetParent(_root, false);
        }

        _stack.Push(item);
        _pooled.Add(item);
    }

    private T CreateInstance()
    {
        GameObject go = Object.Instantiate(_prefab, _root);
        return go.GetComponent<T>();
    }
}
