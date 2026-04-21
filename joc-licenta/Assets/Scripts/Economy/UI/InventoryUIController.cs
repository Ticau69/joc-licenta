using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

/// <summary>
/// Inventory UI Controller - Optimized with pooling and caching
/// </summary>
public class InventoryUIController : MonoBehaviour
{
    private InflationManager inflationManager; // Referință directă pentru a obține prețurile actualizate
    private VisualElement _inventoryTab;
    private ScrollView _inventoryList;
    private VisualElement _detailsPanel;
    [SerializeField] private VisualTreeAsset inventoryRowTemplate;

    private Label _detailNameLabel;
    private Label _detailStockLabel;
    private Label _detailStatusLabel;
    private Slider _priceSlider;
    private Label _priceDisplayLabel;
    private Label _profitDisplayLabel;
    private Label _storageCapacityLabel;
    private Label _competitorPriceLabel;
    private Label _marketStatusLabel;

    private IEconomyService _economy;
    private IInventoryService _inventory;
    private ProductDataSO _productDB;
    private IEventBus _eventBus;
    private GameConfigSO _config;

    private ProductType _currentViewingProduct = ProductType.None;
    private float _updateTimer = 0f;

    private readonly HashSet<ProductType> _overpricedProducts = new HashSet<ProductType>();
    private Label _priceWarningLabel;

    // Caching pentru performanță
    private readonly Dictionary<ProductType, (int stock, string status, Color color)> _cachedStockData
        = new Dictionary<ProductType, (int, string, Color)>();
    private bool _needsRefresh = true;

    public void Initialize(VisualElement root, IEconomyService economy, IEventBus eventBus, GameConfigSO config, IInventoryService inventory, ProductDataSO productDB, VisualTreeAsset rowTemplate = null)
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

        if (rowTemplate != null)
            UIRowFactory.SetRowTemplate(rowTemplate);
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
        _priceWarningLabel = root.Q<Label>("PriceWarningLabel");
        _storageCapacityLabel = root.Q<Label>("StorageCapacityLabel");
        _competitorPriceLabel = root.Q<Label>("CompetitorPriceLabel");
        _marketStatusLabel = root.Q<Label>("MarketStatusLabel");

        if (_inventoryList == null) Debug.LogError("[InventoryUI] InventoryList not found in UI!");
        if (_detailsPanel == null) Debug.LogError("[InventoryUI] DetailsPanel not found in UI!");
        if (_priceDisplayLabel != null && _priceDisplayLabel.parent != null)
        {
            _priceDisplayLabel.parent.Add(_priceWarningLabel);
        }
    }

    private void SetupEventListeners()
    {
        if (_priceSlider != null) _priceSlider.RegisterValueChangedCallback(OnPriceChanged);

        _eventBus.Subscribe<StockChangedEvent>(OnStockChanged);
        // --- NOU: Ascultăm când clienții se plâng de preț ---
        _eventBus.Subscribe<ProductPricedTooHighEvent>(OnProductPricedTooHigh);
    }

    private void OnStockChanged(StockChangedEvent evt)
    {
        _needsRefresh = true;
        if (evt.Product == _currentViewingProduct)
        {
            UpdateCurrentProductDetails();
        }
    }

    private void OnPriceChanged(ChangeEvent<float> evt)
    {
        if (_currentViewingProduct == ProductType.None) return;
        _economy.UpdateSellingPrice(_currentViewingProduct, evt.newValue);

        if (_economy.TryGetProductData(_currentViewingProduct, out ProductEconomics data))
        {
            UpdatePriceLabels(data);
            UpdateMarketInfo(_currentViewingProduct, evt.newValue); // ── NOU
        }
    }
    private void OnProductPricedTooHigh(ProductPricedTooHighEvent evt)
    {
        // Adăugăm produsul în lista neagră. Dacă nu era deja acolo, dăm refresh la UI.
        if (_overpricedProducts.Add(evt.Product))
        {
            _needsRefresh = true;

            // Dacă jucătorul se uită CHIAR ACUM la acest produs, îi dăm update panoului din dreapta
            if (_currentViewingProduct == evt.Product)
            {
                UpdateCurrentProductDetails();
            }
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

    private void UpdateStorageCapacityLabel()
    {
        if (_storageCapacityLabel == null || _inventory == null) return;

        int used = _inventory.GetUsedCapacity();
        int total = _inventory.GetTotalCapacity();

        if (total == 0)
        {
            _storageCapacityLabel.text = "Fără Rafturi!";
            _storageCapacityLabel.style.color = new StyleColor(new Color(1f, 0.2f, 0.2f));
            return;
        }

        _storageCapacityLabel.text = $"{used} / {total}";

        if (used >= total)
            _storageCapacityLabel.style.color = new StyleColor(new Color(1f, 0.2f, 0.2f));
        else if (used >= total * 0.8f)
            _storageCapacityLabel.style.color = new StyleColor(new Color(1f, 0.8f, 0.2f));
        else
            _storageCapacityLabel.style.color = new StyleColor(Color.white);
    }

    private void UpdateMarketInfo(ProductType type, float playerPrice)
    {
        if (_competitorPriceLabel == null || _marketStatusLabel == null) return;
        if (CompetitiveMarketManager.Instance == null)
        {
            _competitorPriceLabel.style.display = DisplayStyle.None;
            _marketStatusLabel.style.display = DisplayStyle.None;
            return;
        }

        var (cheapestStore, cheapestPrice) =
            CompetitiveMarketManager.Instance.GetCheapestCompetitor(type);

        if (cheapestStore == null || cheapestPrice <= 0f)
        {
            _competitorPriceLabel.text = "Fără concurență pe această piață";
            _competitorPriceLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _marketStatusLabel.text = "";
            return;
        }

        _competitorPriceLabel.text =
            $"{cheapestStore.storeName}: {cheapestPrice:F2} RON";

        float diff = playerPrice - cheapestPrice;
        float diffPct = cheapestPrice > 0 ? (diff / cheapestPrice) * 100f : 0f;

        if (diff < -0.01f)
        {
            // Jucătorul e mai ieftin
            _marketStatusLabel.text = $"✓ Ești mai ieftin cu {Mathf.Abs(diffPct):F0}%";
            _marketStatusLabel.style.color = new Color(0.3f, 0.85f, 0.3f);
            _competitorPriceLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        }
        else if (diff > 0.01f)
        {
            // Concurentul e mai ieftin
            _marketStatusLabel.text = $"⚠ Ești mai scump cu {diffPct:F0}%";
            _marketStatusLabel.style.color = new Color(1f, 0.5f, 0.2f);
            _competitorPriceLabel.style.color = new Color(1f, 0.7f, 0.3f);
        }
        else
        {
            // Prețuri egale
            _marketStatusLabel.text = "≈ Preț similar cu concurența";
            _marketStatusLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            _competitorPriceLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        }
    }

    private void RefreshInventoryList()
    {
        if (_inventoryList == null) return;
        if (_productDB == null) return;

        _inventoryList.Clear();
        _cachedStockData.Clear();

        bool isFirstProduct = true;

        foreach (var productData in _productDB.allProducts)
        {
            ProductType type = productData.type;
            if (type == ProductType.None) continue;

            // ── Date stoc ────────────────────────────────────────────────────────
            int stockAmount = _inventory.GetStock(type);
            int maxStock = 999; // sau un maxim din config
            var (color, status) = _config.GetStockStatus(stockAmount);
            _cachedStockData[type] = (stockAmount, status, color);

            // ── Profit per unitate ────────────────────────────────────────────────
            float profitPerUnit = 0f;
            if (_economy.TryGetProductData(type, out ProductEconomics econ))
                profitPerUnit = econ.Profit;

            // ── Creăm rândul cu noul factory ─────────────────────────────────────
            VisualElement row = UIRowFactory.CreateInventoryRow(
                type: type,
                stockAmount: stockAmount,
                maxStock: maxStock,
                profitPerUnit: profitPerUnit,
                status: status,
                statusColor: color,
                onViewClicked: () => ShowProductDetails(type)
            );

            // Warning preț prea mare
            if (_overpricedProducts != null && _overpricedProducts.Contains(type))
            {
                Label warning = new Label("📉");
                warning.style.position = Position.Absolute;
                warning.style.right = 60;
                warning.style.alignSelf = Align.Center;
                row.Add(warning);
            }

            _inventoryList.Add(row);

            // Auto-selectare primul produs
            if (isFirstProduct && _currentViewingProduct == ProductType.None)
                ShowProductDetails(type);

            isFirstProduct = false;
        }

        UpdateStorageCapacityLabel();
    }

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

        if (_priceWarningLabel != null)
        {
            bool isOverpriced = _overpricedProducts.Contains(type);
            _priceWarningLabel.style.display = isOverpriced ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_economy.TryGetProductData(type, out ProductEconomics data))
            UpdateMarketInfo(type, data.sellingPrice);
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
            float inflationAdjustedCost = ServiceLocator.Instance.Get<InflationManager>().GetPrice(data.data.baseCost);

            float minPrice = inflationAdjustedCost * _config.minPriceMultiplier;
            float maxPrice = inflationAdjustedCost * _config.maxPriceMultiplier;

            _priceSlider.lowValue = minPrice;
            _priceSlider.highValue = maxPrice;
            _priceSlider.SetValueWithoutNotify(data.sellingPrice * ServiceLocator.Instance.Get<InflationManager>().CurrentInflation);
        }

        UpdatePriceLabels(data);
    }

    private void UpdatePriceLabels(ProductEconomics data)
    {
        if (_priceDisplayLabel != null)
            _priceDisplayLabel.text = $"{data.sellingPrice * ServiceLocator.Instance.Get<InflationManager>().CurrentInflation:F2} RON";

        if (_profitDisplayLabel != null)
        {
            float profit = data.Profit * ServiceLocator.Instance.Get<InflationManager>().CurrentInflation;
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
        // --- NOU: Dezabonare ---
        _eventBus?.Unsubscribe<ProductPricedTooHighEvent>(OnProductPricedTooHigh);

        if (_priceSlider != null) _priceSlider.UnregisterValueChangedCallback(OnPriceChanged);
    }
}