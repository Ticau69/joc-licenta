using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

public class ShelfUIController : MonoBehaviour
{
    private VisualElement _objectSelectedInfo;
    private Label _objectNameLabel;
    private VisualElement _contentContainer;
    private Button _closeButton;
    private Label _statusText;
    private Label _stockText;
    private DropdownField _uiDropdown;

    private WorkStation _currentSelectedShelf;
    private IEconomyService _economy;
    private IShopService _shop;
    private IEventBus _eventBus;
    private IObjectRegistry _registry;
    private GameConfigSO _config;

    // -----------------------------------------------------------------------
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
    }

    // -----------------------------------------------------------------------
    private void CacheUIElements(VisualElement root)
    {
        _objectSelectedInfo = root.Q<VisualElement>("ObjectInfo");
        if (_objectSelectedInfo == null)
        {
            Debug.LogError("❌ Nu am găsit 'ObjectInfo' în UXML!");
            return;
        }

        _objectNameLabel = _objectSelectedInfo.Q<Label>("ObjectName");
        _closeButton = _objectSelectedInfo.Q<Button>("CloseButton");
        _contentContainer = _objectSelectedInfo.Q<VisualElement>("Content");
        _statusText = _objectSelectedInfo.Q<Label>("StatusText");
        _stockText = _objectSelectedInfo.Q<Label>("StockText");
        _uiDropdown = _objectSelectedInfo.Q<DropdownField>("ProductDropdown");

        if (_objectNameLabel == null) Debug.LogError("❌ Nu am găsit 'ObjectName'!");
        if (_contentContainer == null) Debug.LogError("❌ Nu am găsit 'Content'!");
        if (_statusText == null) Debug.LogError("❌ Nu am găsit 'StatusText'!");
        if (_stockText == null) Debug.LogError("❌ Nu am găsit 'StockText'!");
        if (_uiDropdown == null) Debug.LogError("❌ Nu am găsit 'ProductDropdown'!");

        _objectSelectedInfo.style.display = DisplayStyle.None;
    }

    // -----------------------------------------------------------------------
    private void SetupEventListeners()
    {
        if (_closeButton != null)
            _closeButton.clicked += ClosePanel;

        if (_uiDropdown != null)
            _uiDropdown.RegisterValueChangedCallback(OnProductDropdownChanged);

        _eventBus.Subscribe<StockChangedEvent>(OnStockChanged);
        _eventBus.Subscribe<SupplyPurchasedEvent>(OnSupplyPurchased);
    }

    // -----------------------------------------------------------------------
    private void OnStockChanged(StockChangedEvent evt)
    {
        if (_currentSelectedShelf != null)
            RefreshUI();
    }

    private void OnSupplyPurchased(SupplyPurchasedEvent evt)
    {
        if (evt.Success)
            RefreshUI();
    }

    // -----------------------------------------------------------------------
    public void SelectObject(GameObject obj)
    {
        if (obj == null) return;

        WorkStation shelf = obj.GetComponentInParent<WorkStation>(true)
                         ?? obj.transform.root.GetComponent<WorkStation>();

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

    // -----------------------------------------------------------------------
    private void RefreshUI()
    {
        if (_currentSelectedShelf == null || _objectNameLabel == null) return;

        _objectNameLabel.text = _currentSelectedShelf.shelfVariant.ToString();

        UpdateStatusLabels();
        UpdateDropdown();
    }

    private void UpdateStatusLabels()
    {
        if (_currentSelectedShelf.slot1Product == ProductType.None)
        {
            _statusText.text = "Raftul este gol.";
            _statusText.style.color = Color.gray;

            _stockText.style.display = DisplayStyle.None;
        }
        else
        {
            _statusText.text = $"Produs: {_currentSelectedShelf.slot1Product}";
            _statusText.style.color = Color.white;

            float fill = (float)_currentSelectedShelf.slot1Stock
                       / _currentSelectedShelf.maxProductsPerSlot;

            Color stockColor = _currentSelectedShelf.slot1Stock == 0
                ? _config.criticalStockColor
                : fill <= 0.5f ? _config.lowStockColor : _config.goodStockColor;

            _stockText.text = $"Raft: {_currentSelectedShelf.slot1Stock}/{_currentSelectedShelf.maxProductsPerSlot}";
            _stockText.style.color = stockColor;
            _stockText.style.display = DisplayStyle.Flex;
        }
    }

    private void UpdateDropdown()
    {
        if (_uiDropdown == null) return;

        // Dezactivăm callback-ul temporar ca să nu declanșăm schimbări la populare
        _uiDropdown.UnregisterValueChangedCallback(OnProductDropdownChanged);

        _uiDropdown.choices = _currentSelectedShelf.GetAllowedProducts()
            .Select(x => x.ToString())
            .ToList();

        // Selectăm produsul curent în dropdown (sau primul dacă raftul e gol)
        _uiDropdown.value = _currentSelectedShelf.slot1Product != ProductType.None
            ? _currentSelectedShelf.slot1Product.ToString()
            : _uiDropdown.choices.FirstOrDefault() ?? "";

        _uiDropdown.RegisterValueChangedCallback(OnProductDropdownChanged);
    }

    // -----------------------------------------------------------------------
    private void OnProductDropdownChanged(ChangeEvent<string> evt)
    {
        if (string.IsNullOrEmpty(evt.newValue)) return;
        if (!System.Enum.TryParse(evt.newValue, out ProductType selectedType)) return;

        ApplyProductChange(selectedType);
    }

    private void ApplyProductChange(ProductType selectedType)
    {
        if (_currentSelectedShelf == null) return;

        bool shelfEmpty = _currentSelectedShelf.slot1Stock == 0;
        bool sameProduct = _currentSelectedShelf.slot1Product == selectedType;

        if (shelfEmpty || sameProduct)
        {
            // Schimbare imediată
            _currentSelectedShelf.slot1Product = selectedType;
            _currentSelectedShelf.pendingProduct = ProductType.None;
        }
        else
        {
            // Raftul are stoc din alt produs → angajații vor face schimbarea gradual
            _currentSelectedShelf.pendingProduct = selectedType;
        }

        NotifyAllRestockers();
        RefreshUI();
    }

    private void NotifyAllRestockers()
    {
        foreach (var emp in _registry.GetAll<Employee>())
            emp.WakeUpAndWork();
    }

    // -----------------------------------------------------------------------
    void OnDestroy()
    {
        _eventBus?.Unsubscribe<StockChangedEvent>(OnStockChanged);
        _eventBus?.Unsubscribe<SupplyPurchasedEvent>(OnSupplyPurchased);
    }
}