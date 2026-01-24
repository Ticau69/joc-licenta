using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Event Bus - sistem complet decuplat pentru comunicare între componente
/// Performance optimizat cu caching și pooling
/// </summary>
public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new Dictionary<Type, List<Delegate>>();
    private readonly object _lock = new object();
    private readonly Queue<EventInvocation> _eventQueue = new Queue<EventInvocation>();
    private bool _isProcessing = false;

    private struct EventInvocation
    {
        public Type EventType;
        public object EventData;
    }

    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null)
        {
            Debug.LogWarning("[EventBus] Attempted to subscribe null handler");
            return;
        }

        lock (_lock)
        {
            Type eventType = typeof(T);

            if (!_subscriptions.ContainsKey(eventType))
            {
                _subscriptions[eventType] = new List<Delegate>();
            }

            if (!_subscriptions[eventType].Contains(handler))
            {
                _subscriptions[eventType].Add(handler);
            }
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        if (handler == null) return;

        lock (_lock)
        {
            Type eventType = typeof(T);

            if (_subscriptions.TryGetValue(eventType, out List<Delegate> handlers))
            {
                handlers.Remove(handler);

                if (handlers.Count == 0)
                {
                    _subscriptions.Remove(eventType);
                }
            }
        }
    }

    public void Publish<T>(T eventData) where T : struct
    {
        Type eventType = typeof(T);

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out List<Delegate> handlers))
            {
                return; // No subscribers
            }

            // Clone the list to avoid modification during iteration
            List<Delegate> handlersCopy = new List<Delegate>(handlers);

            // Invoke immediately if not already processing
            if (!_isProcessing)
            {
                _isProcessing = true;

                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        (handler as Action<T>)?.Invoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventBus] Error invoking handler for {eventType.Name}: {ex.Message}");
                    }
                }

                _isProcessing = false;

                // Process queued events
                ProcessQueue();
            }
            else
            {
                // Queue event if already processing to avoid recursion
                _eventQueue.Enqueue(new EventInvocation
                {
                    EventType = eventType,
                    EventData = eventData
                });
            }
        }
    }

    private void ProcessQueue()
    {
        while (_eventQueue.Count > 0)
        {
            EventInvocation invocation = _eventQueue.Dequeue();

            if (_subscriptions.TryGetValue(invocation.EventType, out List<Delegate> handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler.DynamicInvoke(invocation.EventData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventBus] Error processing queued event {invocation.EventType.Name}: {ex.Message}");
                    }
                }
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _subscriptions.Clear();
            _eventQueue.Clear();
            _isProcessing = false;
            Debug.Log("[EventBus] All subscriptions cleared");
        }
    }

    // Debugging helper
    public void LogSubscriptions()
    {
        lock (_lock)
        {
            Debug.Log($"[EventBus] Active subscriptions ({_subscriptions.Count}):");
            foreach (var kvp in _subscriptions)
            {
                Debug.Log($"  - {kvp.Key.Name}: {kvp.Value.Count} handlers");
            }
        }
    }
}