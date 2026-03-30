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

    private Label _detailNameLabel;
    private Label _detailStockLabel;
    private Label _detailStatusLabel;
    private Slider _priceSlider;
    private Label _priceDisplayLabel;
    private Label _profitDisplayLabel;
    private Label _storageCapacityLabel;

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
        _priceWarningLabel = root.Q<Label>("PriceWarningLabel");
        _storageCapacityLabel = root.Q<Label>("StorageCapacityLabel");

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
        }

        // --- NOU: Dacă modifică prețul, scoatem produsul din "lista neagră" a clienților! ---
        if (_overpricedProducts.Contains(_currentViewingProduct))
        {
            _overpricedProducts.Remove(_currentViewingProduct);
            _needsRefresh = true;
            if (_priceWarningLabel != null) _priceWarningLabel.style.display = DisplayStyle.None;
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

    private void RefreshInventoryList()
    {
        if (_inventoryList == null) return;

        if (_productDB == null)
        {
            Debug.LogError("[InventoryUI] Nu pot genera lista - ProductDB lipsește!");
            return;
        }

        _inventoryList.Clear();
        _cachedStockData.Clear();

        // Variabilă nouă pentru a ști când suntem la primul produs
        bool isFirstProduct = true;

        foreach (var productData in _productDB.allProducts)
        {
            ProductType type = productData.type;
            if (type == ProductType.None) continue;

            int amount = _inventory.GetStock(type);
            var (color, status) = _config.GetStockStatus(amount);

            _cachedStockData[type] = (amount, status, color);

            var row = UIRowFactory.CreateInventoryRow(
                type,
                amount,
                status,
                color,
                () => ShowProductDetails(type));

            // Lipim iconița de avertisment pe rândul din listă, dacă e cazul
            if (_overpricedProducts != null && _overpricedProducts.Contains(type))
            {
                Label listWarningIcon = new Label("📉 Preț prea mare!");
                listWarningIcon.style.color = new Color(1f, 0.2f, 0.2f);
                listWarningIcon.style.unityFontStyleAndWeight = FontStyle.Bold;

                // --- REPARAȚIA: Poziționare Absolută ---
                listWarningIcon.style.position = Position.Absolute;
                listWarningIcon.style.right = 80; // Îl punem cu 80px spre stânga (chiar înainte de butonul VIEW)
                listWarningIcon.style.alignSelf = Align.Center;

                row.Add(listWarningIcon);
            }

            _inventoryList.Add(row);

            // --- NOU: Autoselectarea primului produs la deschiderea panoului ---
            // Dacă este primul produs generat ȘI jucătorul nu se uită deja la altceva
            if (isFirstProduct && _currentViewingProduct == ProductType.None)
            {
                ShowProductDetails(type);
            }
            isFirstProduct = false; // După prima trecere, nu mai este primul produs
        }

        if (_storageCapacityLabel != null && _inventory != null)
        {
            int used = _inventory.GetUsedCapacity();
            int total = _inventory.GetTotalCapacity();

            _storageCapacityLabel.text = $"{used} / {total}";

            // Feedback vizual (Colorăm textul dacă depozitul se umple)
            if (total == 0)
            {
                _storageCapacityLabel.text = "Fără Rafturi!";
                _storageCapacityLabel.style.color = new Color(1f, 0.2f, 0.2f); // Roșu
            }
            else if (used >= total)
            {
                _storageCapacityLabel.style.color = new Color(1f, 0.2f, 0.2f); // Roșu (Plin)
            }
            else if (used >= total * 0.8f)
            {
                _storageCapacityLabel.style.color = new Color(1f, 0.8f, 0.2f); // Portocaliu (Aproape plin - 80%)
            }
            else
            {
                _storageCapacityLabel.style.color = new Color(1f, 1f, 1f); // Alb normal
            }
        }
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