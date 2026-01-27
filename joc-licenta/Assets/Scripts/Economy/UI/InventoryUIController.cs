using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

/// <summary>
/// Inventory UI Controller - Optimized with pooling and caching
/// </summary>
public class InventoryUIController : MonoBehaviour
{
    private VisualElement _inventoryTab;
    private ScrollView _inventoryList;
    private VisualElement _detailsPanel;

    private Label _detailNameLabel;
    private Label _detailStockLabel;
    private Label _detailStatusLabel;
    private Slider _priceSlider;
    private Label _priceDisplayLabel;
    private Label _profitDisplayLabel;

    private IEconomyService _economy;
    private IInventoryService _inventory;
    private ProductDataSO _productDB;
    private IEventBus _eventBus;
    private GameConfigSO _config;

    private ProductType _currentViewingProduct = ProductType.None;
    private float _updateTimer = 0f;

    // Caching pentru performanță
    private readonly Dictionary<ProductType, (int stock, string status, Color color)> _cachedStockData
        = new Dictionary<ProductType, (int, string, Color)>();
    private bool _needsRefresh = true;

    public void Initialize(VisualElement root, IEconomyService economy, IEventBus eventBus, GameConfigSO config, IInventoryService inventory, ProductDataSO productDB)
    {
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _productDB = productDB ?? throw new ArgumentNullException(nameof(productDB));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        CacheUIElements(root);
        SetupEventListeners();

        if (_config.verboseLogging)
        {
            Debug.Log("[InventoryUI] Initialized successfully");
        }
    }

    private void CacheUIElements(VisualElement root)
    {
        _inventoryTab = root.Q<VisualElement>("Inventory");
        _inventoryList = root.Q<ScrollView>("InventoryList");
        _detailsPanel = root.Q<VisualElement>("DetailsPanel");

        _detailNameLabel = root.Q<Label>("DetailName");
        _detailStockLabel = root.Q<Label>("DetailStock");
        _detailStatusLabel = root.Q<Label>("DetailStatus");
        _priceSlider = root.Q<Slider>("PriceSlider");
        _priceDisplayLabel = root.Q<Label>("PriceDisplay");
        _profitDisplayLabel = root.Q<Label>("ProfitDisplay");

        if (_inventoryList == null) Debug.LogError("[InventoryUI] InventoryList not found in UI!");
        if (_detailsPanel == null) Debug.LogError("[InventoryUI] DetailsPanel not found in UI!");
    }

    private void SetupEventListeners()
    {
        if (_priceSlider != null)
        {
            _priceSlider.RegisterValueChangedCallback(OnPriceChanged);
        }
        _eventBus.Subscribe<StockChangedEvent>(OnStockChanged);
    }

    private void OnStockChanged(StockChangedEvent evt)
    {
        _needsRefresh = true;
        if (evt.Product == _currentViewingProduct)
        {
            UpdateCurrentProductDetails();
        }
    }

    public void UpdateIfNeeded()
    {
        if (_inventoryTab == null || _inventoryTab.style.display == DisplayStyle.None)
            return;

        _updateTimer += Time.deltaTime;

        if (_updateTimer >= _config.inventoryUpdateInterval || _needsRefresh)
        {
            RefreshInventoryList();
            _updateTimer = 0f;
            _needsRefresh = false;
        }
    }

    // --- MODIFICARE PRINCIPALĂ AICI ---
    private void RefreshInventoryList()
    {
        if (_inventoryList == null) return;

        // Folosim direct referința _inventory (salvată în Initialize)
        if (_inventory?.MainStorage == null) return;

        // Verificăm dacă avem baza de date conectată
        if (_productDB == null)
        {
            Debug.LogError("[InventoryUI] Nu pot genera lista - ProductDB lipsește!");
            return;
        }

        _inventoryList.Clear();
        _cachedStockData.Clear();

        // Iterăm prin produsele din Baza de Date (Lista Master)
        // Asta asigură că vedem TOATE produsele definite, chiar dacă stocul e 0.
        foreach (var productData in _productDB.allProducts)
        {
            ProductType type = productData.type;
            if (type == ProductType.None) continue;

            int amount = _inventory.GetStock(type);
            var (color, status) = _config.GetStockStatus(amount);

            _cachedStockData[type] = (amount, status, color);

            // Creăm rândul în UI
            var row = UIRowFactory.CreateInventoryRow(
                type,
                amount,
                status,
                color,
                () => ShowProductDetails(type));

            _inventoryList.Add(row);
        }
    }
    // ----------------------------------

    private void ShowProductDetails(ProductType type)
    {
        _currentViewingProduct = type;

        if (!_cachedStockData.TryGetValue(type, out var stockData))
        {
            // Fallback dacă cumva datele lipsesc (ex: update întârziat)
            int amount = _inventory.GetStock(type);
            var (color, status) = _config.GetStockStatus(amount);
            stockData = (amount, status, color);
        }

        UpdateProductInfo(type);
        UpdateStockInfo(stockData);
        UpdatePricingControls(type);
    }

    private void UpdateProductInfo(ProductType type)
    {
        if (_economy.TryGetProductData(type, out ProductEconomics data))
        {
            if (_detailNameLabel != null)
                _detailNameLabel.text = data.data.productName;
        }
        else
        {
            if (_detailNameLabel != null)
                _detailNameLabel.text = type.ToString();
        }
    }

    private void UpdateStockInfo((int stock, string status, Color color) stockData)
    {
        if (_detailStockLabel != null)
        {
            _detailStockLabel.text = $"Stoc Actual: {stockData.stock} bucăți";
            _detailStockLabel.style.color = stockData.color;
        }

        if (_detailStatusLabel != null)
        {
            _detailStatusLabel.text = $"Status Depozit: {stockData.status}";
            _detailStatusLabel.style.color = stockData.color;
        }
    }

    private void UpdatePricingControls(ProductType type)
    {
        if (!_economy.TryGetProductData(type, out ProductEconomics data))
            return;

        if (_priceSlider != null)
        {
            float minPrice = data.CurrentBaseCost * _config.minPriceMultiplier;
            float maxPrice = data.CurrentBaseCost * _config.maxPriceMultiplier;

            _priceSlider.lowValue = minPrice;
            _priceSlider.highValue = maxPrice;
            _priceSlider.SetValueWithoutNotify(data.sellingPrice);
        }

        UpdatePriceLabels(data);
    }

    private void OnPriceChanged(ChangeEvent<float> evt)
    {
        if (_currentViewingProduct == ProductType.None) return;

        _economy.UpdateSellingPrice(_currentViewingProduct, evt.newValue);

        if (_economy.TryGetProductData(_currentViewingProduct, out ProductEconomics data))
        {
            UpdatePriceLabels(data);
        }
    }

    private void UpdatePriceLabels(ProductEconomics data)
    {
        if (_priceDisplayLabel != null)
            _priceDisplayLabel.text = $"{data.sellingPrice:F2} RON";

        if (_profitDisplayLabel != null)
        {
            float profit = data.Profit;
            _profitDisplayLabel.text = $"Profit: {(profit >= 0 ? "+" : "")}{profit:F2} RON";
            _profitDisplayLabel.style.color = profit >= 0 ? _config.goodStockColor : _config.criticalStockColor;
        }
    }

    private void UpdateCurrentProductDetails()
    {
        if (_currentViewingProduct != ProductType.None)
        {
            ShowProductDetails(_currentViewingProduct);
        }
    }

    void OnDestroy()
    {
        _eventBus?.Unsubscribe<StockChangedEvent>(OnStockChanged);

        if (_priceSlider != null)
        {
            _priceSlider.UnregisterValueChangedCallback(OnPriceChanged);
        }
    }
}