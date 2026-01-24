using UnityEngine;

/// <summary>
/// Component pentru auto-înregistrare în ObjectRegistry
/// Adaugă acest component pe WorkStation și Employee pentru a elimina FindObjects
/// </summary>
[DefaultExecutionOrder(-100)] // Execute early
public class AutoRegisterComponent : MonoBehaviour
{
    [SerializeField] private bool unregisterOnDestroy = true;

    private IObjectRegistry _registry;
    private bool _isRegistered = false;

    void Awake()
    {
        RegisterSelf();
    }

    void OnEnable()
    {
        if (!_isRegistered)
        {
            RegisterSelf();
        }
    }

    private void RegisterSelf()
    {
        if (!ServiceLocator.Instance.TryGet(out _registry))
        {
            // Retry in Start if registry not ready yet
            return;
        }

        RegisterComponents();
        _isRegistered = true;
    }

    void Start()
    {
        if (!_isRegistered)
        {
            RegisterSelf();
        }
    }

    private void RegisterComponents()
    {
        // Register WorkStation
        var workStation = GetComponent<WorkStation>();
        if (workStation != null)
        {
            _registry.Register(workStation);
        }

        // Register Employee
        var employee = GetComponent<Employee>();
        if (employee != null)
        {
            _registry.Register(employee);
        }

        // Add more component types as needed
    }

    void OnDisable()
    {
        if (unregisterOnDestroy && _isRegistered)
        {
            UnregisterSelf();
        }
    }

    void OnDestroy()
    {
        if (unregisterOnDestroy && _isRegistered)
        {
            UnregisterSelf();
        }
    }

    private void UnregisterSelf()
    {
        if (_registry == null) return;

        var workStation = GetComponent<WorkStation>();
        if (workStation != null)
        {
            _registry.Unregister(workStation);
        }

        var employee = GetComponent<Employee>();
        if (employee != null)
        {
            _registry.Unregister(employee);
        }

        _isRegistered = false;
    }
}

/// <summary>
/// Extension pentru WorkStation - trebuie adăugat la clasa WorkStation existentă
/// </summary>
public static class WorkStationExtensions
{
    // Metodă helper pentru a adăuga/remove din storage cu events
    public static void AddToStorageWithEvent(this WorkStation station, ProductType type, int amount)
    {
        station.AddToStorage(type, amount);

        // Publish event via ServiceLocator
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new StockChangedEvent
            {
                Product = type,
                OldStock = 0, // Would need to track this
                NewStock = amount,
                Location = station.stationType
            });
        }
    }

    public static void RemoveFromStorageWithEvent(this WorkStation station, ProductType type, int amount)
    {
        station.RemoveFromStorage(type, amount);

        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new StockChangedEvent
            {
                Product = type,
                OldStock = amount,
                NewStock = 0,
                Location = station.stationType
            });
        }
    }
}