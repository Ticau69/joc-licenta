using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EventBus — comunicare decuplată între sisteme.
///
/// Fix-uri față de versiunea anterioară:
///   1. Lock-ul NU mai este ținut în timpul invocării handler-elor
///      → elimină deadlock-ul când un handler apela Subscribe/Publish
///   2. Coada stochează Action (closure tipizat) în loc de object+DynamicInvoke
///      → elimină alocările și penalizarea de ~10x la DynamicInvoke
///   3. Re-intrarea gestionată cu un contor (_depth) în loc de bool
///      → Publish-ul din interiorul unui handler se pune corect în coadă
///
/// Notă: EventBus este single-threaded (Unity main thread).
/// Lock-ul există doar pentru Subscribe/Unsubscribe din coroutine-uri sau Task-uri.
/// </summary>
public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
    private readonly Queue<Action> _pending = new();
    private readonly object _lock = new();
    private int _depth;

    private readonly Dictionary<Type, object> _lastEventStates = new();

    // ─── Subscribe / Unsubscribe ──────────────────────────────────────────────

    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) return;

        lock (_lock)
        {
            var type = typeof(T);
            if (!_subscriptions.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _subscriptions[type] = list;
            }
            if (!list.Contains(handler))
                list.Add(handler);

            // Dacă cineva se abonează și avem deja datele în memorie, trimite-le imediat!
            if (_lastEventStates.TryGetValue(type, out var lastData))
            {
                handler((T)lastData);
            }
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) return;

        lock (_lock)
        {
            var type = typeof(T);
            if (!_subscriptions.TryGetValue(type, out var list)) return;

            list.Remove(handler);
            if (list.Count == 0)
                _subscriptions.Remove(type);
        }
    }

    // ─── Publish ──────────────────────────────────────────────────────────────

    public void Publish<T>(T eventData) where T : struct
    {
        var type = typeof(T);

        // NOU: Salvăm ultima stare dacă este un eveniment de tip Load
        if (type == typeof(GameDataLoadedEvent))
        {
            lock (_lock) { _lastEventStates[type] = eventData; }
        }

        List<Delegate> snapshot;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(type, out var list)) return;
            snapshot = new List<Delegate>(list);
        }

        if (_depth > 0)
        {
            _pending.Enqueue(() => InvokeAll<T>(snapshot, eventData));
            return;
        }

        _depth++;
        InvokeAll<T>(snapshot, eventData);
        _depth--;

        Flush();
    }

    // ─── Internals ────────────────────────────────────────────────────────────

    private static void InvokeAll<T>(List<Delegate> handlers, T data) where T : struct
    {
        foreach (var d in handlers)
        {
            try
            {
                ((Action<T>)d)(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventBus] Eroare în handler pentru {typeof(T).Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    /// <summary>Procesează evenimentele publicate din interiorul unui handler.</summary>
    private void Flush()
    {
        while (_pending.Count > 0)
        {
            var action = _pending.Dequeue();
            _depth++;
            action();
            _depth--;
        }
    }

    // ─── Utilitare ────────────────────────────────────────────────────────────

    public void Clear()
    {
        lock (_lock)
        {
            _subscriptions.Clear();
            _pending.Clear();
            _depth = 0;
        }
        Debug.Log("[EventBus] Toate abonamentele au fost șterse.");
    }

    public void LogSubscriptions()
    {
        lock (_lock)
        {
            Debug.Log($"[EventBus] Abonamente active ({_subscriptions.Count}):");
            foreach (var kvp in _subscriptions)
                Debug.Log($"  • {kvp.Key.Name}: {kvp.Value.Count} handler(e)");
        }
    }
}