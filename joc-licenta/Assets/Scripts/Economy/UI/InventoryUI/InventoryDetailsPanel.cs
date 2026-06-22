using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class InventoryDetailsPanel
{
    private readonly Label _detailName;
    private readonly Label _detailStock;
    private readonly Label _detailStatus;
    private readonly Label _priceDisplay;
    private readonly Slider _priceSlider;
    private readonly Label _profitDisplay;
    private readonly Label _priceWarning;
    private readonly Label _competitorPrice;
    private readonly Label _marketStatus;
    private readonly Label _storageCapacity;

    private Label _salesTaxRateValue;
    private Label _salesTaxAmount;
    private Label _netRevenue;

    private readonly IEconomyService _economy;
    private readonly IInventoryService _inventory;
    private readonly GameConfigSO _config;
    private readonly InventoryPriceState _priceState; // FIX: Folosim direct InventoryPriceState
    private readonly HashSet<ProductType> _belowCostNotified = new();

    private ProductType _currentProduct = ProductType.None;
    private bool _sliderUpdating;

    public InventoryDetailsPanel(
        VisualElement detailsRoot,
        IEconomyService economy,
        IInventoryService inventory,
        GameConfigSO config,
        InventoryPriceState priceState) // FIX: Aici cerea HashSet înainte
    {
        _economy = economy;
        _inventory = inventory;
        _config = config;
        _priceState = priceState;

        _detailName = detailsRoot.Q<Label>("DetailName");
        _detailStock = detailsRoot.Q<Label>("DetailStock");
        _detailStatus = detailsRoot.Q<Label>("DetailStatus");
        _priceDisplay = detailsRoot.Q<Label>("PriceDisplay");
        _priceSlider = detailsRoot.Q<Slider>("PriceSlider");
        _profitDisplay = detailsRoot.Q<Label>("ProfitDisplay");
        _priceWarning = detailsRoot.Q<Label>("PriceWarningLabel");
        _competitorPrice = detailsRoot.Q<Label>("CompetitorPriceLabel");
        _marketStatus = detailsRoot.Q<Label>("MarketStatusLabel");
        _storageCapacity = detailsRoot.Q<Label>("StorageCapacityLabel");

        TaxUIHelper.CacheTaxElements(detailsRoot,
            out _salesTaxRateValue, out _salesTaxAmount, out _netRevenue);

        if (_priceSlider != null)
            _priceSlider.RegisterValueChangedCallback(OnSliderMoved);
    }

    public void ShowProduct(ProductType type)
    {
        _currentProduct = type;
        if (type == ProductType.None) return;

        if (!_economy.TryGetProductData(type, out var econ)) return;

        if (_detailName != null) _detailName.text = type.ToString();

        int stock = _inventory.GetStock(type);
        var (color, status) = _config.GetStockStatus(stock);
        if (_detailStock != null) _detailStock.text = $"Stoc: {stock}";
        if (_detailStatus != null)
        {
            _detailStatus.text = $"Status: {status}";
            _detailStatus.style.color = new StyleColor(color);
        }

        if (_priceSlider != null)
        {
            _sliderUpdating = true;
            // FIX: Calculăm limitele manual pentru că ProductEconomics nu are MinPrice/MaxPrice
            float baseCost = econ.CurrentBaseCost;
            _priceSlider.lowValue = baseCost * 1.0f; // Setează multiplicatorul tău minim aici
            _priceSlider.highValue = baseCost * 3.0f; // Setează multiplicatorul tău maxim aici
            _priceSlider.value = econ.sellingPrice; // FIX: Folosim sellingPrice
            _sliderUpdating = false;
        }

        RefreshPriceLabels(econ);
        RefreshMarketInfo(econ);
        RefreshCapacityLabel();
    }

    public void RefreshCurrentStock()
    {
        if (_currentProduct == ProductType.None) return;

        int stock = _inventory.GetStock(_currentProduct);
        var (color, status) = _config.GetStockStatus(stock);

        if (_detailStock != null) _detailStock.text = $"Stoc: {stock}";
        if (_detailStatus != null)
        {
            _detailStatus.text = $"Status: {status}";
            _detailStatus.style.color = new StyleColor(color);
        }

        RefreshCapacityLabel();
    }

    // FIX: Funcțiile cerute de InventoryUIController
    public void RefreshIfViewing(ProductType type)
    {
        if (_currentProduct == type)
        {
            ShowProduct(type);
        }
    }

    public void Dispose()
    {
        if (_priceSlider != null)
            _priceSlider.UnregisterValueChangedCallback(OnSliderMoved);
    }

    private void OnSliderMoved(ChangeEvent<float> e)
    {
        if (_sliderUpdating || _currentProduct == ProductType.None) return;

        float newPrice = e.newValue;
        _economy.UpdateSellingPrice(_currentProduct, newPrice); // FIX: Numele corect al funcției

        if (_economy.TryGetProductData(_currentProduct, out var econ))
        {
            RefreshPriceLabels(econ);
            RefreshMarketInfo(econ);
            CheckBelowCostNotification(econ);
        }
    }

    private void RefreshPriceLabels(ProductEconomics econ)
    {
        int price = Mathf.RoundToInt(econ.sellingPrice); // FIX
        int profit = Mathf.RoundToInt(econ.Profit); // FIX

        if (_priceDisplay != null)
            _priceDisplay.text = $"{price} RON";

        if (_profitDisplay != null)
        {
            _profitDisplay.text = profit >= 0 ? $"Profit: +{profit} RON" : $"Pierdere: {profit} RON";
            _profitDisplay.style.color = new StyleColor(profit >= 0 ? Color.green : Color.red);
        }

        // Asumăm că InventoryPriceState are o proprietate sau metodă pentru asta (ex: IsOverpriced)
        // Dacă dă eroare aici, depinde cum ai scris InventoryPriceState.
        // bool overpriced = _priceState.IsOverpriced(_currentProduct); 
        // if (_priceWarning != null) _priceWarning.style.display = overpriced ? DisplayStyle.Flex : DisplayStyle.None;

        TaxUIHelper.UpdateSalesTax(price, _salesTaxAmount, _netRevenue);
    }

    private void RefreshMarketInfo(ProductEconomics econ)
    {
        if (_competitorPrice == null && _marketStatus == null) return;

        // Dacă nu ai CompetitorPrice, folosim BaseCost temporar sau îl poți adăuga în ProductEconomics
        float comp = econ.CurrentBaseCost * 1.5f;

        if (_competitorPrice != null)
            _competitorPrice.text = $"Piață: {comp:F0} RON";

        if (_marketStatus != null)
        {
            if (econ.sellingPrice < comp * 0.95f)
            {
                _marketStatus.text = "✓ Sub piață — atragi clienți";
                _marketStatus.style.color = new StyleColor(Color.green);
            }
            else if (econ.sellingPrice > comp * 1.10f)
            {
                _marketStatus.text = "⚠ Peste piață";
                _marketStatus.style.color = new StyleColor(new Color(1f, 0.6f, 0.1f));
            }
            else
            {
                _marketStatus.text = "≈ La nivelul pieței";
                _marketStatus.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            }
        }
    }

    private void RefreshCapacityLabel()
    {
        if (_storageCapacity == null) return;

        int used = _inventory.GetUsedCapacity();
        int total = _inventory.GetTotalCapacity();

        _storageCapacity.text = total == 0 ? "Fără rafturi!" : $"{used} / {total}";
        _storageCapacity.style.color = total == 0 || used >= total
            ? new StyleColor(new Color(1f, 0.2f, 0.2f))
            : new StyleColor(Color.white);
    }

    private void CheckBelowCostNotification(ProductEconomics econ)
    {
        if (econ.Profit < 0 && !_belowCostNotified.Contains(_currentProduct))
        {
            _belowCostNotified.Add(_currentProduct);
            MentorSystem.Instance?.NotifyPriceBelowCost();
        }
        else if (econ.Profit >= 0)
        {
            _belowCostNotified.Remove(_currentProduct);
        }
    }
}