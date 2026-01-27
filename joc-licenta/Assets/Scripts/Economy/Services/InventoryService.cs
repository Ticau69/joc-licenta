using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// Inventory Service - gestionează accesul la storage și stock
/// Cached pentru performanță maximă
/// </summary>
public class InventoryService : IInventoryService
{
    private WorkStation _mainStorage;
    private readonly IObjectRegistry _registry;
    private readonly IEventBus _eventBus;
    private readonly GameConfigSO _config;

    private float _lastCacheUpdate;
    private readonly float _cacheDuration;

    public WorkStation MainStorage
    {
        get
        {
            if (_mainStorage == null || Time.time - _lastCacheUpdate > _cacheDuration)
            {
                RefreshStorageCache();
            }
            return _mainStorage;
        }
    }

    public event Action<ProductType, int> OnStockChanged;

    public InventoryService(IObjectRegistry registry, IEventBus eventBus, GameConfigSO config)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cacheDuration = config.cacheDuration;

        RefreshStorageCache();
    }

    private void RefreshStorageCache()
    {
        var allStations = _registry.GetAll<WorkStation>();
        _mainStorage = allStations.FirstOrDefault(x => x.stationType == StationType.Storage);
        _lastCacheUpdate = Time.time;

        if (_mainStorage == null && _config.showPerformanceWarnings)
        {
            Debug.LogWarning("[InventoryService] Main storage not found in registry!");
        }
    }

    public int GetStock(ProductType type)
    {
        if (type == ProductType.None)
        {
            Debug.LogWarning("[InventoryService] Cannot get stock for ProductType.None");
            return 0;
        }

        if (MainStorage == null)
        {
            Debug.LogWarning("[InventoryService] Main storage not available");
            return 0;
        }

        return MainStorage.storageInventory.TryGetValue(type, out int amount) ? amount : 0;
    }

    public bool HasStock(ProductType type, int minimumAmount = 1)
    {
        if (type == ProductType.None) return false;
        if (minimumAmount < 0) minimumAmount = 0;

        return GetStock(type) >= minimumAmount;
    }

    public void AddStock(ProductType type, int amount)
    {
        if (type == ProductType.None)
        {
            Debug.LogWarning("[InventoryService] Cannot add stock for ProductType.None");
            return;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[InventoryService] Invalid amount to add: {amount}");
            return;
        }

        if (MainStorage == null)
        {
            Debug.LogError("[InventoryService] Cannot add stock - main storage not available!");
            return;
        }

        int oldStock = GetStock(type);
        MainStorage.AddToStorage(type, amount);
        int newStock = GetStock(type);

        NotifyStockChange(type, oldStock, newStock);

        if (_config.verboseLogging)
        {
            Debug.Log($"[InventoryService] Added {amount} x {type}. Stock: {oldStock} → {newStock}");
        }
    }

    public bool TryRemoveStock(ProductType type, int amount)
    {
        if (type == ProductType.None)
        {
            Debug.LogWarning("[InventoryService] Cannot remove stock for ProductType.None");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[InventoryService] Invalid amount to remove: {amount}");
            return false;
        }

        if (!HasStock(type, amount))
        {
            if (_config.verboseLogging)
            {
                Debug.Log($"[InventoryService] Insufficient stock. Required: {amount}, Available: {GetStock(type)}");
            }
            return false;
        }

        int oldStock = GetStock(type);
        MainStorage.TakeFromStorage(type, amount);
        int newStock = GetStock(type);

        NotifyStockChange(type, oldStock, newStock);

        if (_config.verboseLogging)
        {
            Debug.Log($"[InventoryService] Removed {amount} x {type}. Stock: {oldStock} → {newStock}");
        }

        return true;
    }

    private void NotifyStockChange(ProductType type, int oldStock, int newStock)
    {
        OnStockChanged?.Invoke(type, newStock);

        _eventBus.Publish(new StockChangedEvent
        {
            Product = type,
            OldStock = oldStock,
            NewStock = newStock,
            Location = StationType.Storage
        });
    }

    public void ForceRefreshCache()
    {
        RefreshStorageCache();
    }
}