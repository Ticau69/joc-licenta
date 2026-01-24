using UnityEngine;
using System;
using UnityEngine.UIElements;

/// <summary>
/// GameManager - Production Ready
/// - Dependency Injection via interfaces
/// - Complete error handling
/// - Service Locator pattern
/// - Event-driven architecture
/// - Backwards compatible
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private GameConfigSO gameConfig;
    [SerializeField] private ProductDataSO productDB;

    [Header("UI")]
    [SerializeField] private UIDocument uiDocument;

    // Services (private - access via ServiceLocator)
    private IEconomyService _economy;
    private IMoneyService _money;
    private IInventoryService _inventory;
    private IShopService _shop;
    private IEventBus _eventBus;
    private IObjectRegistry _objectRegistry;

    // UI Controllers
    private InventoryUIController _inventoryUI;
    private ShelfUIController _shelfUI;

    // Validation
    private bool _isInitialized = false;

    void Awake()
    {
        if (!InitializeSingleton()) return;

        if (!ValidateConfiguration())
        {
            Debug.LogError("[GameManager] Configuration validation failed! Check inspector settings.");
            enabled = false;
            return;
        }

        InitializeServices();
        RegisterServices();

        _isInitialized = true;
    }

    private bool InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            return true;
        }
        else
        {
            Debug.LogWarning("[GameManager] Duplicate GameManager detected. Destroying...");
            Destroy(gameObject);
            return false;
        }
    }

    private bool ValidateConfiguration()
    {
        bool isValid = true;

        if (gameConfig == null)
        {
            Debug.LogError("[GameManager] GameConfig is not assigned!");
            isValid = false;
        }

        if (productDB == null)
        {
            Debug.LogError("[GameManager] ProductDB is not assigned!");
            isValid = false;
        }

        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("[GameManager] UIDocument component not found!");
                isValid = false;
            }
        }

        return isValid;
    }

    private void InitializeServices()
    {
        // Create core services
        _eventBus = new EventBus();
        _objectRegistry = new ObjectRegistry();

        // Register self in registry
        _objectRegistry.Register(this);

        // Create game services with dependencies
        _economy = new EconomyManager(productDB, _eventBus, gameConfig);
        _money = new MoneyManager(gameConfig.startingMoney, _eventBus, gameConfig);
        _inventory = new InventoryService(_objectRegistry, _eventBus, gameConfig);
        _shop = new ShopManager(_money, _economy, _inventory, _eventBus, gameConfig);

        if (gameConfig.verboseLogging)
        {
            Debug.Log("[GameManager] All services initialized successfully");
        }
    }

    private void RegisterServices()
    {
        // Register in ServiceLocator for global access
        ServiceLocator.Instance.Register<IEconomyService>(_economy);
        ServiceLocator.Instance.Register<IMoneyService>(_money);
        ServiceLocator.Instance.Register<IInventoryService>(_inventory);
        ServiceLocator.Instance.Register<IShopService>(_shop);
        ServiceLocator.Instance.Register<IEventBus>(_eventBus);
        ServiceLocator.Instance.Register<IObjectRegistry>(_objectRegistry);
        ServiceLocator.Instance.Register(gameConfig);

        if (gameConfig.verboseLogging)
        {
            ServiceLocator.Instance.LogRegisteredServices();
        }
    }

    void OnEnable()
    {
        if (!_isInitialized) return;

        InitializeUI();
    }

    private void InitializeUI()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[GameManager] Cannot initialize UI - UIDocument is null");
            return;
        }

        var root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("[GameManager] UIDocument root is null!");
            return;
        }

        // Initialize Money UI
        (_money as MoneyManager)?.Initialize(root);

        // Create UI Controllers
        _inventoryUI = gameObject.AddComponent<InventoryUIController>();
        _shelfUI = gameObject.AddComponent<ShelfUIController>();

        _inventoryUI.Initialize(root, _economy, _eventBus, gameConfig);
        _shelfUI.Initialize(root, _economy, _shop, _eventBus, _objectRegistry, gameConfig);

        if (gameConfig.verboseLogging)
        {
            Debug.Log("[GameManager] UI initialized successfully");
        }
    }

    void Start()
    {
        if (!_isInitialized) return;

        SetupPlayerInput();

        if (gameConfig.verboseLogging)
        {
            Debug.Log("[GameManager] Game started successfully");
        }
    }

    private void SetupPlayerInput()
    {
        var playerInput = FindFirstObjectByType<PlayerInput>();

        if (playerInput != null)
        {
            playerInput.OnObjectClicked += _shelfUI.SelectObject;
        }
        else
        {
            Debug.LogWarning("[GameManager] PlayerInput not found in scene");
        }
    }

    void Update()
    {
        if (!_isInitialized) return;

        _inventoryUI?.UpdateIfNeeded();
    }

    void OnDestroy()
    {
        if (!_isInitialized) return;

        // Cleanup
        (_money as MoneyManager)?.Cleanup();

        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.OnObjectClicked -= _shelfUI.SelectObject;
        }

        // Clear ServiceLocator
        ServiceLocator.Instance.Clear();

        if (gameConfig.verboseLogging)
        {
            Debug.Log("[GameManager] Cleanup completed");
        }
    }

    // === BACKWARDS COMPATIBILITY API ===
    // Păstrăm exact aceleași metode pentru codul vechi

    public int CurrentMoney => _money?.CurrentAmount ?? 0;

    public System.Collections.Generic.Dictionary<ProductType, ProductEconomics> marketData
    {
        get
        {
            var readOnlyDict = _economy?.MarketData;
            if (readOnlyDict == null) return new System.Collections.Generic.Dictionary<ProductType, ProductEconomics>();
            return new System.Collections.Generic.Dictionary<ProductType, ProductEconomics>(readOnlyDict);
        }
    }

    public event Action OnMoneyChanged
    {
        add
        {
            if (_money != null)
            {
                (_money as MoneyManager).OnMoneyChanged += (old, newVal) => value?.Invoke();
            }
        }
        remove
        {
            if (_money != null)
            {
                (_money as MoneyManager).OnMoneyChanged -= (old, newVal) => value?.Invoke();
            }
        }
    }

    public bool TrySpendMoney(int amount) => _money?.TrySpend(amount) ?? false;
    public void AddMoney(int amount) => _money?.Add(amount);
    public void UpdateMoneyUI() => (_money as MoneyManager)?.UpdateUI();
}