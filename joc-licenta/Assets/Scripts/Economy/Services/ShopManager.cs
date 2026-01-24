using UnityEngine;
using System;

/// <summary>
/// Shop Manager - cu validation completă și prețuri reale din economie
/// </summary>
public class ShopManager : IShopService
{
    private readonly IMoneyService _money;
    private readonly IEconomyService _economy;
    private readonly IInventoryService _inventory;
    private readonly IEventBus _eventBus;
    private readonly GameConfigSO _config;

    public ShopManager(
        IMoneyService money,
        IEconomyService economy,
        IInventoryService inventory,
        IEventBus eventBus,
        GameConfigSO config)
    {
        _money = money ?? throw new ArgumentNullException(nameof(money));
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool CanAfford(ProductType type, int quantity)
    {
        if (type == ProductType.None || quantity <= 0)
            return false;

        int totalCost = CalculateCost(type, quantity);
        return _money.CanAfford(totalCost);
    }

    public void BuySupply(ProductType type, int quantity, WorkStation target, Action<bool> callback)
    {
        // Validation
        if (!ValidatePurchase(type, quantity, target, out string errorMessage))
        {
            Debug.LogWarning($"[ShopManager] Purchase validation failed: {errorMessage}");
            callback?.Invoke(false);
            return;
        }

        int totalCost = CalculateCost(type, quantity);

        if (!_money.TrySpend(totalCost))
        {
            Debug.LogWarning($"[ShopManager] Insufficient funds. Required: {totalCost} RON, Available: {_money.CurrentAmount} RON");
            callback?.Invoke(false);
            PublishPurchaseEvent(type, quantity, totalCost, false);
            return;
        }

        // Add to storage
        target.AddToStorage(type, quantity);

        // Success
        if (_config.verboseLogging)
        {
            Debug.Log($"[ShopManager] Purchased {quantity} x {type} for {totalCost} RON");
        }

        PublishPurchaseEvent(type, quantity, totalCost, true);
        callback?.Invoke(true);
    }

    private int CalculateCost(ProductType type, int quantity)
    {
        // Folosește prețul real din economie dacă există
        if (_economy.IsProductValid(type))
        {
            float baseCost = _economy.GetBaseCost(type);
            return Mathf.CeilToInt(baseCost * quantity);
        }

        // Fallback pentru backwards compatibility
        if (_config.verboseLogging)
        {
            Debug.LogWarning($"[ShopManager] Product {type} not in economy database. Using temporary cost.");
        }
        return _config.temporarySupplyCost;
    }

    private bool ValidatePurchase(ProductType type, int quantity, WorkStation target, out string errorMessage)
    {
        if (type == ProductType.None)
        {
            errorMessage = "Cannot purchase ProductType.None";
            return false;
        }

        if (quantity <= 0)
        {
            errorMessage = $"Invalid quantity: {quantity}";
            return false;
        }

        if (target == null)
        {
            errorMessage = "Target storage is null";
            return false;
        }

        if (target.stationType != StationType.Storage)
        {
            errorMessage = $"Target is not a storage (it's a {target.stationType})";
            return false;
        }

        if (!_economy.IsProductValid(type))
        {
            Debug.LogWarning($"[ShopManager] Product {type} not in economy database. Purchase will use fallback pricing.");
        }

        errorMessage = string.Empty;
        return true;
    }

    private void PublishPurchaseEvent(ProductType type, int quantity, int cost, bool success)
    {
        _eventBus.Publish(new SupplyPurchasedEvent
        {
            Product = type,
            Quantity = quantity,
            Cost = cost,
            Success = success
        });
    }

    // Helper pentru UI - verifică dacă poți cumpăra cu cantitatea default
    public bool CanAffordDefaultSupply(ProductType type)
    {
        return CanAfford(type, _config.defaultSupplyQuantity);
    }

    // Cumpără cu cantitatea default din config
    public void BuyDefaultSupply(ProductType type, WorkStation target, Action<bool> callback)
    {
        BuySupply(type, _config.defaultSupplyQuantity, target, callback);
    }

    // Calculează și returnează costul pentru UI
    public int GetPurchaseCost(ProductType type, int quantity)
    {
        return CalculateCost(type, quantity);
    }
}