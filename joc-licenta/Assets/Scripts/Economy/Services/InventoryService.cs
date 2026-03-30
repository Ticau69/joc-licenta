using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Inventory Service - Acum gestionează multiple rafturi fizice de depozit (StorageRacks).
/// Cached pentru performanță maximă.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IObjectRegistry _registry;
    private readonly IEventBus _eventBus;
    private readonly GameConfigSO _config;

    private float _lastCacheUpdate;
    private readonly float _cacheDuration;

    // --- NOU: O listă cu toate rafturile fizice din depozit ---
    private List<StorageRacks> _cachedRacks = new List<StorageRacks>();

    public event Action<ProductType, int> OnStockChanged;

    public InventoryService(IObjectRegistry registry, IEventBus eventBus, GameConfigSO config)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cacheDuration = config.cacheDuration;
    }

    // Returnează rafturile (și face update la listă doar dacă a trecut timpul de cache)
    private List<StorageRacks> GetRacks()
    {
        if (_cachedRacks == null || _cachedRacks.Count == 0 || Time.time - _lastCacheUpdate > _cacheDuration)
        {
            RefreshStorageCache();
        }
        return _cachedRacks;
    }

    private void RefreshStorageCache()
    {
        // Găsim toate rafturile de depozit construite în scenă
        _cachedRacks = new List<StorageRacks>(UnityEngine.Object.FindObjectsByType<StorageRacks>(FindObjectsSortMode.None));
        _lastCacheUpdate = Time.time;

        if (_cachedRacks.Count == 0 && _config.showPerformanceWarnings)
        {
            Debug.LogWarning("[InventoryService] Nu am găsit niciun StorageRack în scenă! Depozitul este gol/inexistent.");
        }
    }

    // --- NOU: CALCULUL CAPACITĂȚII ---
    public int GetTotalCapacity()
    {
        int total = 0;
        foreach (var rack in GetRacks())
        {
            // Capacitatea totală a unui raft = Numărul maxim de cutii * Câte produse încap într-o cutie
            total += rack.maxBoxes * rack.maxAmountPerBox;
        }
        return total;
    }

    public int GetUsedCapacity()
    {
        int used = 0;
        foreach (var rack in GetRacks())
        {
            foreach (var box in rack.storedBoxes)
            {
                used += box.Amount; // Adunăm fiecare produs fizic din cutiile de pe raft
            }
        }
        return used;
    }

    public int GetAvailableCapacity()
    {
        return GetTotalCapacity() - GetUsedCapacity();
    }

    public int GetStock(ProductType type)
    {
        if (type == ProductType.None) return 0;

        int totalStock = 0;
        // Facem inventarul adunând stocul de pe fiecare raft fizic
        foreach (var rack in GetRacks())
        {
            totalStock += rack.GetStockAmount(type);
        }

        return totalStock;
    }

    public bool HasStock(ProductType type, int minimumAmount = 1)
    {
        if (type == ProductType.None) return false;
        if (minimumAmount < 0) minimumAmount = 0;

        return GetStock(type) >= minimumAmount;
    }

    public void AddStock(ProductType type, int amount)
    {
        if (type == ProductType.None || amount <= 0) return;

        int oldStock = GetStock(type);
        int remainingAmount = amount;

        // Punem marfa pe rafturi, unul câte unul, până terminăm cutiile
        foreach (var rack in GetRacks())
        {
            remainingAmount = rack.AddProduct(type, remainingAmount);
            if (remainingAmount <= 0) break; // Am pus tot
        }

        int newStock = GetStock(type);
        NotifyStockChange(type, oldStock, newStock);

        if (remainingAmount > 0)
        {
            Debug.LogWarning($"[InventoryService] Depozitul este PLIN! S-au pierdut {remainingAmount} produse de tip {type}. Jucătorul trebuie să construiască mai multe rafturi de depozit!");
        }
        else if (_config.verboseLogging)
        {
            Debug.Log($"[InventoryService] Added {amount} x {type}. Stock: {oldStock} → {newStock}");
        }
    }

    public bool TryRemoveStock(ProductType type, int amount)
    {
        if (type == ProductType.None || amount <= 0) return false;

        // Dacă nu avem destul în tot depozitul, refuzăm tranzacția
        if (!HasStock(type, amount))
        {
            return false;
        }

        int oldStock = GetStock(type);
        int remainingToTake = amount;

        // Angajatul ia marfa de pe rafturi (începând cu primul găsit)
        foreach (var rack in GetRacks())
        {
            int takenHere = rack.TakeProduct(type, remainingToTake);
            remainingToTake -= takenHere;

            if (remainingToTake <= 0) break; // A luat tot ce îi trebuia
        }

        int newStock = GetStock(type);
        NotifyStockChange(type, oldStock, newStock);

        if (_config.verboseLogging)
        {
            Debug.Log($"[InventoryService] Removed {amount} x {type}. Stock: {oldStock} → {newStock}");
        }

        return true;
    }

    public StorageRacks FindRackWithProduct(ProductType type)
    {
        foreach (var rack in GetRacks())
        {
            if (rack.GetStockAmount(type) > 0)
            {
                return rack;
            }
        }
        return null; // Nu avem produsul pe niciun raft!
    }

    // Caută primul raft fizic care are loc să mai primească o cutie (folosit la Clearing)
    public StorageRacks FindRackWithSpace(ProductType type)
    {
        foreach (var rack in GetRacks())
        {
            // Verificăm dacă are măcar 1 spațiu liber simulând o adăugare fictivă (sau făcând matematică)
            // Simplificat: Dacă are loc conform formulelor tale din StorageRack
            if (rack.storedBoxes.Count < rack.maxBoxes || rack.GetStockAmount(type) % rack.maxAmountPerBox != 0)
            {
                return rack;
            }
        }
        return null;
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