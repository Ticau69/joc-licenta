using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the scrollable inventory list (left panel).
/// Handles row creation, the storage-capacity label,
/// and the overpriced-product warning badge.
/// Plain C# class — not a MonoBehaviour.
/// </summary>
public class InventoryListPanel
{
    // ── Callback wired by the orchestrator ───────────────────────────────────
    /// <summary>Called when the player clicks "View" on a row.</summary>
    public Action<ProductType> OnShowProductDetails;

    // ── Dependencies ─────────────────────────────────────────────────────────
    private readonly IEconomyService _economy;
    private readonly IInventoryService _inventory;
    private readonly GameConfigSO _config;
    private readonly ProductDataSO _productDB;
    private readonly InventoryPriceState _priceState;

    // ── UI references ─────────────────────────────────────────────────────────
    private readonly ScrollView _inventoryList;
    private readonly Label _storageCapacityLabel;

    // ── Internal state ────────────────────────────────────────────────────────
    private readonly Dictionary<ProductType, (int stock, string status, Color color)> _cachedStockData = new();
    private bool _needsRefresh = true;
    private bool _firstProductAutoSelected;

    public bool NeedsRefresh => _needsRefresh;

    // ── Constructor ───────────────────────────────────────────────────────────

    public InventoryListPanel(
        VisualElement root,
        IEconomyService economy,
        IInventoryService inventory,
        GameConfigSO config,
        ProductDataSO productDB,
        InventoryPriceState priceState)
    {
        _economy = economy;
        _inventory = inventory;
        _config = config;
        _productDB = productDB;
        _priceState = priceState;

        _inventoryList = root.Q<ScrollView>("InventoryList");
        _storageCapacityLabel = root.Q<Label>("StorageCapacityLabel");

        if (_inventoryList == null)
            Debug.LogError("[InventoryListPanel] 'InventoryList' ScrollView not found in UI!");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void MarkDirty() => _needsRefresh = true;

    public void Refresh()
    {
        if (_inventoryList == null || _productDB == null) return;

        _inventoryList.Clear();
        _cachedStockData.Clear();

        bool autoSelectDone = _firstProductAutoSelected;

        foreach (var productData in _productDB.allProducts)
        {
            ProductType type = productData.type;
            if (type == ProductType.None) continue;

            // Stock data
            int stockAmount = _inventory.GetStock(type);
            var (color, status) = _config.GetStockStatus(stockAmount);
            _cachedStockData[type] = (stockAmount, status, color);

            // Profit per unit
            float profitPerUnit = 0f;
            if (_economy.TryGetProductData(type, out ProductEconomics econ))
                profitPerUnit = econ.Profit;

            // Build row
            VisualElement row = UIRowFactory.CreateInventoryRow(
                type: type,
                stockAmount: stockAmount,
                maxStock: 999,
                profitPerUnit: profitPerUnit,
                status: status,
                statusColor: color,
                onViewClicked: () => OnShowProductDetails?.Invoke(type)
            );

            // Overpriced badge
            if (_priceState.IsOverpriced(type))
            {
                var badge = new Label("📉")
                {
                    style =
                    {
                        position  = Position.Absolute,
                        right     = 60,
                        alignSelf = Align.Center,
                    }
                };
                row.Add(badge);
            }

            _inventoryList.Add(row);

            // Auto-select the first product on the initial load
            if (!autoSelectDone)
            {
                OnShowProductDetails?.Invoke(type);
                _firstProductAutoSelected = true;
                autoSelectDone = true;
            }
        }

        UpdateStorageCapacityLabel();
        _needsRefresh = false;
    }

    /// <summary>
    /// Returns cached stock data for a product, or fetches it fresh if missing.
    /// Used by InventoryDetailsPanel to avoid duplicate service calls.
    /// </summary>
    public (int stock, string status, Color color) GetStockData(ProductType type)
    {
        if (_cachedStockData.TryGetValue(type, out var cached))
            return cached;

        int amount = _inventory.GetStock(type);
        var (color, status) = _config.GetStockStatus(amount);
        return (amount, status, color);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void UpdateStorageCapacityLabel()
    {
        if (_storageCapacityLabel == null) return;

        int used = _inventory.GetUsedCapacity();
        int total = _inventory.GetTotalCapacity();

        if (total == 0)
        {
            _storageCapacityLabel.text = "Fără Rafturi!";
            _storageCapacityLabel.style.color = new StyleColor(new Color(1f, 0.2f, 0.2f));
            return;
        }

        _storageCapacityLabel.text = $"{used} / {total}";

        _storageCapacityLabel.style.color = used >= total
            ? new StyleColor(new Color(1f, 0.2f, 0.2f))
            : used >= total * 0.8f
                ? new StyleColor(new Color(1f, 0.8f, 0.2f))
                : new StyleColor(Color.white);
    }
}