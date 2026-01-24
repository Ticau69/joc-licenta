using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service Locator - elimină dependențele de FindObjects și oferă acces centralizat la servicii
/// Thread-safe, performant, cu validation
/// </summary>
public class ServiceLocator
{
    private static ServiceLocator _instance;
    public static ServiceLocator Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ServiceLocator();
            }
            return _instance;
        }
    }

    private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
    private readonly object _lock = new object();

    public void Register<T>(T service) where T : class
    {
        lock (_lock)
        {
            Type type = typeof(T);

            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Service {type.Name} already registered. Overwriting.");
            }

            _services[type] = service;
            Debug.Log($"[ServiceLocator] Registered service: {type.Name}");
        }
    }

    public void Unregister<T>() where T : class
    {
        lock (_lock)
        {
            Type type = typeof(T);
            if (_services.Remove(type))
            {
                Debug.Log($"[ServiceLocator] Unregistered service: {type.Name}");
            }
        }
    }

    public T Get<T>() where T : class
    {
        lock (_lock)
        {
            Type type = typeof(T);

            if (_services.TryGetValue(type, out object service))
            {
                return service as T;
            }

            throw new InvalidOperationException($"[ServiceLocator] Service {type.Name} not found! Did you forget to register it?");
        }
    }

    public bool TryGet<T>(out T service) where T : class
    {
        lock (_lock)
        {
            Type type = typeof(T);

            if (_services.TryGetValue(type, out object obj))
            {
                service = obj as T;
                return service != null;
            }

            service = null;
            return false;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _services.Clear();
            Debug.Log("[ServiceLocator] All services cleared.");
        }
    }

    // Pentru debugging
    public void LogRegisteredServices()
    {
        lock (_lock)
        {
            Debug.Log($"[ServiceLocator] Registered services ({_services.Count}):");
            foreach (var kvp in _services)
            {
                Debug.Log($"  - {kvp.Key.Name}");
            }
        }
    }
}

/// <summary>
/// Object Registry - înlocuiește FindObjects cu un registry performant
/// </summary>
public class ObjectRegistry : IObjectRegistry
{
    private readonly Dictionary<Type, HashSet<object>> _registry = new Dictionary<Type, HashSet<object>>();
    private readonly object _lock = new object();

    public void Register<T>(T obj) where T : class
    {
        if (obj == null)
        {
            Debug.LogWarning("[ObjectRegistry] Attempted to register null object");
            return;
        }

        lock (_lock)
        {
            Type type = typeof(T);

            if (!_registry.ContainsKey(type))
            {
                _registry[type] = new HashSet<object>();
            }

            _registry[type].Add(obj);
        }
    }

    public void Unregister<T>(T obj) where T : class
    {
        if (obj == null) return;

        lock (_lock)
        {
            Type type = typeof(T);

            if (_registry.TryGetValue(type, out HashSet<object> set))
            {
                set.Remove(obj);
            }
        }
    }

    public T Get<T>() where T : class
    {
        lock (_lock)
        {
            Type type = typeof(T);

            if (_registry.TryGetValue(type, out HashSet<object> set))
            {
                foreach (var obj in set)
                {
                    if (obj is T result)
                        return result;
                }
            }

            return null;
        }
    }

    public IEnumerable<T> GetAll<T>() where T : class
    {
        lock (_lock)
        {
            Type type = typeof(T);

            if (_registry.TryGetValue(type, out HashSet<object> set))
            {
                List<T> results = new List<T>();
                foreach (var obj in set)
                {
                    if (obj is T result)
                        results.Add(result);
                }
                return results;
            }

            return new List<T>();
        }
    }

    public bool TryGet<T>(out T result) where T : class
    {
        result = Get<T>();
        return result != null;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _registry.Clear();
        }
    }
}