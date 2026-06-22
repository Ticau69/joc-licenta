using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Gestionează stocurile distribuite pe multiple StorageRacks fizice din scenă.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IObjectRegistry _registry;
    private readonly IEventBus _eventBus;
    private readonly GameConfigSO _config;
    private readonly float _cacheDuration;

    private List<StorageRacks> _cachedRacks = new();
    private float _lastCacheUpdate;
    private bool _cacheValid;          // flag separat — evită re-scan când scena are 0 rafturi

    public event Action<ProductType, int> OnStockChanged;

    public InventoryService(IObjectRegistry registry, IEventBus eventBus, GameConfigSO config)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cacheDuration = config.cacheDuration;
    }

    // ─── Cache ────────────────────────────────────────────────────────────────

    private List<StorageRacks> GetRacks()
    {
        if (!_cacheValid || Time.time - _lastCacheUpdate > _cacheDuration)
            RefreshStorageCache();
        return _cachedRacks;
    }

    private void RefreshStorageCache()
    {
        _cachedRacks = new List<StorageRacks>(UnityEngine.Object.FindObjectsByType<StorageRacks>(FindObjectsSortMode.None));
        _lastCacheUpdate = Time.time;
        _cacheValid = true;   // marcat valid chiar și cu 0 rafturi — evităm re-scan per-frame

        if (_cachedRacks.Count == 0 && _config.showPerformanceWarnings)
            Debug.LogWarning("[InventoryService] Niciun StorageRack în scenă.");
    }

    public void ForceRefreshCache()
    {
        _cacheValid = false;
        RefreshStorageCache();
    }

    // ─── Capacitate ───────────────────────────────────────────────────────────

    public int GetTotalCapacity()
    {
        int total = 0;
        foreach (var rack in GetRacks())
            total += rack.maxBoxes * rack.maxAmountPerBox;
        return total;
    }

    public int GetUsedCapacity()
    {
        int used = 0;
        foreach (var rack in GetRacks())
            foreach (var box in rack.storedBoxes)
                used += box.Amount;
        return used;
    }

    public int GetAvailableCapacity() => GetTotalCapacity() - GetUsedCapacity();

    // ─── Stoc ─────────────────────────────────────────────────────────────────

    public int GetStock(ProductType type)
    {
        if (type == ProductType.None) return 0;

        int total = 0;
        foreach (var rack in GetRacks())
            total += rack.GetStockAmount(type);
        return total;
    }

    public bool HasStock(ProductType type, int minimumAmount = 1)
    {
        if (type == ProductType.None) return false;
        if (minimumAmount <= 0) return true;
        return GetStock(type) >= minimumAmount;
    }

    public void AddStock(ProductType type, int amount)
    {
        if (type == ProductType.None || amount <= 0) return;

        int oldStock = GetStock(type);
        int remaining = amount;

        foreach (var rack in GetRacks())
        {
            remaining = rack.AddProduct(type, remaining);
            if (remaining <= 0) break;
        }

        // Delta calculat din remaining — nu mai apelăm GetStock a doua oară
        int added = amount - remaining;
        int newStock = oldStock + added;
        NotifyStockChange(type, oldStock, newStock);

        if (remaining > 0)
            Debug.LogWarning($"[InventoryService] Depozit plin! {remaining} x {type} pierdute.");
        else if (_config.verboseLogging)
            Debug.Log($"[InventoryService] +{amount} x {type}. Stoc: {oldStock} → {newStock}");
    }

    public bool TryRemoveStock(ProductType type, int amount)
    {
        if (type == ProductType.None || amount <= 0) return false;

        int oldStock = GetStock(type);
        if (oldStock < amount) return false;

        int remaining = amount;

        foreach (var rack in GetRacks())
        {
            int taken = rack.TakeProduct(type, remaining);
            remaining -= taken;
            if (remaining <= 0) break;
        }

        // Delta calculat din remaining — nu mai apelăm GetStock a doua oară
        int removed = amount - remaining;
        int newStock = oldStock - removed;
        NotifyStockChange(type, oldStock, newStock);

        if (_config.verboseLogging)
            Debug.Log($"[InventoryService] -{amount} x {type}. Stoc: {oldStock} → {newStock}");

        return true;
    }

    // ─── Căutare rafturi ──────────────────────────────────────────────────────

    public StorageRacks FindRackWithProduct(ProductType type)
    {
        foreach (var rack in GetRacks())
            if (rack.GetStockAmount(type) > 0)
                return rack;
        return null;
    }

    public StorageRacks FindRackWithSpace(ProductType type)
    {
        foreach (var rack in GetRacks())
        {
            bool hasEmptySlot = rack.storedBoxes.Count < rack.maxBoxes;
            bool hasPartialBox = rack.GetStockAmount(type) % rack.maxAmountPerBox != 0;
            if (hasEmptySlot || hasPartialBox)
                return rack;
        }
        return null;
    }

    // ─── Salvare / Încărcare ──────────────────────────────────────────────────

    public string GenerateSaveJson()
    {
        var state = new InventorySaveState();

        foreach (ProductType type in Enum.GetValues(typeof(ProductType)))
        {
            if (type == ProductType.None) continue;
            int qty = GetStock(type);
            if (qty > 0)
                state.StockList.Add(new ProductStockData { Product = type, Quantity = qty });
        }

        return JsonUtility.ToJson(state);
    }

    public void RestoreFromSave(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "{}") return;

        try
        {
            var state = JsonUtility.FromJson<InventorySaveState>(json);
            ForceRefreshCache();

            foreach (var item in state.StockList)
                AddStock(item.Product, item.Quantity);

            Debug.Log($"[InventoryService] Stocuri restaurate — {state.StockList.Count} produse distincte.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[InventoryService] Eroare la RestoreFromSave: {ex.Message}");
        }
    }

    // ─── Internals ────────────────────────────────────────────────────────────

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
}