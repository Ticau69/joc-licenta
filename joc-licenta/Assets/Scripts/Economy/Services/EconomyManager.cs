using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Economy Manager - cu validation, events și error handling
/// </summary>
public class EconomyManager : IEconomyService
{
    private readonly Dictionary<ProductType, ProductEconomics> _marketData;
    private readonly IEventBus _eventBus;
    private readonly GameConfigSO _config;

    public IReadOnlyDictionary<ProductType, ProductEconomics> MarketData => _marketData;
    public event Action<ProductType, float> OnPriceChanged;

    public EconomyManager(ProductDataSO productDB, IEventBus eventBus, GameConfigSO config)
    {
        _marketData = new Dictionary<ProductType, ProductEconomics>();
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        InitializeFromDatabase(productDB);
    }

    private void InitializeFromDatabase(ProductDataSO productDB)
    {
        if (productDB == null)
        {
            Debug.LogError("[EconomyManager] ProductDatabase is null! Cannot initialize economy.");
            return;
        }

        if (productDB.allProducts == null || productDB.allProducts.Count == 0)
        {
            Debug.LogError("[EconomyManager] ProductDatabase has no products!");
            return;
        }

        int successCount = 0;
        foreach (var productInfo in productDB.allProducts)
        {
            if (productInfo == null)
            {
                Debug.LogWarning("[EconomyManager] Null product entry in database, skipping...");
                continue;
            }

            if (productInfo.type == ProductType.None)
            {
                Debug.LogWarning("[EconomyManager] Product with type 'None' found, skipping...");
                continue;
            }

            if (_marketData.ContainsKey(productInfo.type))
            {
                Debug.LogWarning($"[EconomyManager] Duplicate product type {productInfo.type}, skipping...");
                continue;
            }

            ProductEconomics newEntry = new ProductEconomics(productInfo);
            _marketData.Add(productInfo.type, newEntry);
            successCount++;
        }

        if (_config.verboseLogging)
        {
            Debug.Log($"[EconomyManager] Initialized {successCount} products successfully.");
        }

        if (successCount == 0)
        {
            Debug.LogError("[EconomyManager] No valid products loaded! Check your ProductDatabase.");
        }
    }

    public bool TryGetProductData(ProductType type, out ProductEconomics data)
    {
        if (type == ProductType.None)
        {
            data = null;
            return false;
        }

        return _marketData.TryGetValue(type, out data);
    }

    public void UpdateSellingPrice(ProductType type, float newPrice)
    {
        if (type == ProductType.None)
        {
            Debug.LogWarning("[EconomyManager] Cannot update price for ProductType.None");
            return;
        }

        if (newPrice < 0)
        {
            Debug.LogWarning($"[EconomyManager] Invalid price {newPrice} for {type}. Must be >= 0.");
            return;
        }

        if (!_marketData.TryGetValue(type, out ProductEconomics data))
        {
            Debug.LogWarning($"[EconomyManager] Product {type} not found in market data.");
            return;
        }

        float oldPrice = data.sellingPrice;
        float baseCost = data.CurrentBaseCost;

        // Validate price range
        float minPrice = baseCost * _config.minPriceMultiplier;
        float maxPrice = baseCost * _config.maxPriceMultiplier;

        newPrice = Mathf.Clamp(newPrice, minPrice, maxPrice);
        newPrice = (float)Math.Round(newPrice, 2);

        data.sellingPrice = newPrice;

        // Notify listeners
        OnPriceChanged?.Invoke(type, newPrice);

        _eventBus.Publish(new ProductPriceChangedEvent
        {
            Product = type,
            OldPrice = oldPrice,
            NewPrice = newPrice
        });

        if (_config.verboseLogging)
        {
            Debug.Log($"[EconomyManager] Updated {type} price: {oldPrice:F2} → {newPrice:F2} RON");
        }
    }

    public float GetSellingPrice(ProductType type)
    {
        if (type == ProductType.None) return 0f;
        return _marketData.TryGetValue(type, out var data) ? data.sellingPrice : 0f;
    }

    public float GetBaseCost(ProductType type)
    {
        if (type == ProductType.None) return 0f;
        return _marketData.TryGetValue(type, out var data) ? data.CurrentBaseCost : 0f;
    }

    public float GetProfit(ProductType type)
    {
        if (type == ProductType.None) return 0f;
        return _marketData.TryGetValue(type, out var data) ? data.Profit : 0f;
    }

    public bool IsProductValid(ProductType type)
    {
        return type != ProductType.None && _marketData.ContainsKey(type);
    }
}