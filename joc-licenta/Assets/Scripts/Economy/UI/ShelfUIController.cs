using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Shelf UI Controller - Optimized with registry pattern
/// </summary>
public class ShelfUIController : MonoBehaviour
{
    private VisualElement _objectSelectedInfo;
    private Label _objectNameLabel;
    private VisualElement _contentContainer;
    private Button _addProductButton;
    private Button _closeButton;

    private WorkStation _currentSelectedShelf;
    private IEconomyService _economy;
    private IShopService _shop;
    private IEventBus _eventBus;
    private IObjectRegistry _registry;
    private GameConfigSO _config;

    public void Initialize(
        VisualElement root,
        IEconomyService economy,
        IShopService shop,
        IEventBus eventBus,
        IObjectRegistry registry,
        GameConfigSO config)
    {
        _economy = economy ?? throw new System.ArgumentNullException(nameof(economy));
        _shop = shop ?? throw new System.ArgumentNullException(nameof(shop));
        _eventBus = eventBus ?? throw new System.ArgumentNullException(nameof(eventBus));
        _registry = registry ?? throw new System.ArgumentNullException(nameof(registry));
        _config = config ?? throw new System.ArgumentNullException(nameof(config));

        CacheUIElements(root);
        SetupEventListeners();

        if (_config.verboseLogging)
        {
            Debug.Log("[ShelfUI] Initialized successfully");
        }
    }

    private void CacheUIElements(VisualElement root)
    {
        _objectSelectedInfo = root.Q<VisualElement>("ObjectInfo");

        if (_objectSelectedInfo != null)
        {
            _objectNameLabel = _objectSelectedInfo.Q<Label>("ObjectName");
            _contentContainer = _objectSelectedInfo.Q<VisualElement>("Content");
            _addProductButton = _objectSelectedInfo.Q<Button>("AdaugareProdus");
            _closeButton = _objectSelectedInfo.Q<Button>("CloseButton");

            _objectSelectedInfo.style.display = DisplayStyle.None;
        }
        else
        {
            Debug.LogError("[ShelfUI] ObjectInfo panel not found in UI!");
        }
    }

    private void SetupEventListeners()
    {
        if (_addProductButton != null)
            _addProductButton.clicked += OnActionButtonClicked;

        if (_closeButton != null)
            _closeButton.clicked += ClosePanel;

        // Subscribe to events
        _eventBus.Subscribe<StockChangedEvent>(OnStockChanged);
        _eventBus.Subscribe<SupplyPurchasedEvent>(OnSupplyPurchased);
    }

    private void OnStockChanged(StockChangedEvent evt)
    {
        // Refresh UI if current shelf is affected
        if (_currentSelectedShelf != null)
        {
            RefreshUI();
        }
    }

    private void OnSupplyPurchased(SupplyPurchasedEvent evt)
    {
        if (evt.Success)
        {
            RefreshUI();
        }
    }

    public void SelectObject(GameObject obj)
    {
        if (obj == null) return;

        WorkStation shelf = obj.GetComponentInParent<WorkStation>(true);
        if (shelf == null) shelf = obj.transform.root.GetComponent<WorkStation>();

        if (shelf != null && shelf.stationType == StationType.Shelf)
        {
            _currentSelectedShelf = shelf;
            RefreshUI();
            _objectSelectedInfo.style.display = DisplayStyle.Flex;

            _eventBus.Publish(new ShelfSelectedEvent { Shelf = shelf });
        }
        else
        {
            ClosePanel();
        }
    }

    private void ClosePanel()
    {
        _objectSelectedInfo.style.display = DisplayStyle.None;
        _currentSelectedShelf = null;
    }

    private void RefreshUI()
    {
        if (_currentSelectedShelf == null || _contentContainer == null) return;

        _addProductButton.style.display = DisplayStyle.Flex;
        _addProductButton.SetEnabled(true);

        _contentContainer.Clear();
        _objectNameLabel.text = _currentSelectedShelf.stationType == StationType.Storage
            ? "Depozit Central"
            : _currentSelectedShelf.shelfVariant.ToString();

        if (_currentSelectedShelf.stationType == StationType.Storage)
        {
            SetupStorageUI();
        }
        else
        {
            SetupShelfUI();
        }
    }

    private void SetupStorageUI()
    {
        Label inventoryTitle = new Label("--- INVENTAR ---");
        inventoryTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        inventoryTitle.style.color = _config.lowStockColor;
        _contentContainer.Add(inventoryTitle);

        if (_currentSelectedShelf.storageInventory.Count == 0)
        {
            _contentContainer.Add(UIRowFactory.CreateInfoLabel("Depozit Gol", Color.white));
        }
        else
        {
            foreach (var item in _currentSelectedShelf.storageInventory)
            {
                var (color, _) = _config.GetStockStatus(item.Value);
                _contentContainer.Add(UIRowFactory.CreateInfoLabel($"{item.Key}: {item.Value} buc.", color));
            }
        }

        int cost = _config.temporarySupplyCost;
        _addProductButton.text = $"Comandă Marfă (-{cost} RON)";
    }

    private void SetupShelfUI()
    {
        if (_currentSelectedShelf.slot1Product == ProductType.None)
        {
            _addProductButton.text = "Setează Produs";
            _addProductButton.SetEnabled(true);
        }
        else
        {
            _contentContainer.Add(UIRowFactory.CreateInfoLabel(
                $"Produs: {_currentSelectedShelf.slot1Product}",
                Color.white));

            float fillPercent = (float)_currentSelectedShelf.slot1Stock / _currentSelectedShelf.maxProductsPerSlot;
            Color stockColor = _currentSelectedShelf.slot1Stock == 0
                ? _config.criticalStockColor
                : fillPercent <= 0.5f ? _config.lowStockColor : _config.goodStockColor;

            Label stockLabel = UIRowFactory.CreateInfoLabel(
                $"Raft: {_currentSelectedShelf.slot1Stock}/{_currentSelectedShelf.maxProductsPerSlot}",
                stockColor);
            stockLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _contentContainer.Add(stockLabel);

            _addProductButton.text = "Schimbă Produsul";
        }
    }

    private void OnActionButtonClicked()
    {
        if (_currentSelectedShelf == null) return;

        if (_currentSelectedShelf.stationType == StationType.Storage)
            CreateSupplyDropdown();
        else
            CreateProductSelectionDropdown();
    }

    private void CreateSupplyDropdown()
    {
        _contentContainer.Clear();

        Label title = UIRowFactory.CreateInfoLabel("Ce dorești să comanzi?", Color.white);
        _contentContainer.Add(title);

        DropdownField supplyDrop = new DropdownField("Produs:");
        List<string> options = System.Enum.GetNames(typeof(ProductType))
            .Where(x => x != "None")
            .ToList();

        supplyDrop.choices = options;
        supplyDrop.value = "Selectează...";
        _contentContainer.Add(supplyDrop);

        supplyDrop.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue != "Selectează..." &&
                System.Enum.TryParse(evt.newValue, out ProductType type))
            {
                _shop.BuyDefaultSupply(type, _currentSelectedShelf, success =>
                {
                    if (success)
                    {
                        RefreshUI();
                        _addProductButton.style.display = DisplayStyle.Flex;
                    }
                });
            }
        });

        Button cancelBtn = UIRowFactory.CreateStyledButton("Anulează", () =>
        {
            RefreshUI();
            _addProductButton.style.display = DisplayStyle.Flex;
        });
        _contentContainer.Add(cancelBtn);
    }

    private void CreateProductSelectionDropdown()
    {
        _contentContainer.Clear();

        DropdownField dropdown = new DropdownField("Alege Produsul:");
        List<string> options = _currentSelectedShelf.GetAllowedProducts()
            .Select(x => x.ToString())
            .ToList();

        dropdown.choices = options;
        dropdown.value = "Selectează...";
        dropdown.style.marginBottom = 10;
        dropdown.style.width = Length.Percent(100);

        dropdown.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue != "Selectează...")
                OnProductSelected(evt.newValue);
        });

        _contentContainer.Add(dropdown);

        Button cancelButton = UIRowFactory.CreateStyledButton("Anulează", () =>
        {
            _addProductButton.style.display = DisplayStyle.Flex;
            RefreshUI();
        });
        _contentContainer.Add(cancelButton);
    }

    private void OnProductSelected(string productName)
    {
        if (!System.Enum.TryParse(productName, out ProductType selectedType))
            return;

        if (_currentSelectedShelf.slot1Stock == 0 ||
            _currentSelectedShelf.slot1Product == selectedType)
        {
            _currentSelectedShelf.slot1Product = selectedType;
            _currentSelectedShelf.pendingProduct = ProductType.None;
        }
        else
        {
            _currentSelectedShelf.pendingProduct = selectedType;
            if (_addProductButton != null)
                _addProductButton.text = "În curs de schimbare...";
        }

        NotifyAllRestockers();
        RefreshUI();
    }

    private void NotifyAllRestockers()
    {
        var employees = _registry.GetAll<Employee>();
        foreach (var emp in employees)
        {
            emp.WakeUpAndWork();
        }
    }

    void OnDestroy()
    {
        _eventBus?.Unsubscribe<StockChangedEvent>(OnStockChanged);
        _eventBus?.Unsubscribe<SupplyPurchasedEvent>(OnSupplyPurchased);
    }
}