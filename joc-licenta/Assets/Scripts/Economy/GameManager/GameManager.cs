using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// GameManager — orchestrator pur.
/// Responsabilitate: lifecycle, singleton, backwards-compat API.
/// Logica de servicii → GameServiceInstaller
/// Logica de UI/input → GameUIInitializer
/// Logica de save/load → GameSaveHandler
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private GameConfigSO gameConfig;
    [SerializeField] private ProductDataSO productDB;

    [Header("UI")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset inventoryRowTemplate;

    // Sub-sisteme (componente pe același GameObject)
    private GameServiceInstaller _serviceInstaller;
    private GameUIInitializer _uiInitializer;
    private GameSaveHandler _saveHandler;

    private bool _isInitialized;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (!InitializeSingleton()) return;
        if (!ValidateConfiguration())
        {
            Debug.LogError("[GameManager] Configurație invalidă. Verifică Inspector.");
            enabled = false;
            return;
        }

        _serviceInstaller = gameObject.AddComponent<GameServiceInstaller>();
        if (!_serviceInstaller.Install(gameConfig, productDB)) return;

        _saveHandler ??= gameObject.AddComponent<GameSaveHandler>();
        _saveHandler.Initialize(_serviceInstaller.Money, _serviceInstaller.EventBus);

        if (CloudSaveManager.Instance != null)
        {
            CloudSaveManager.Instance.Initialize(_serviceInstaller.EventBus);
            Debug.Log("[GameManager] EventBus proaspăt injectat în CloudSaveManager.");
        }

        _isInitialized = true;
    }

    void OnEnable()
    {
        if (!_isInitialized) return;

        _uiInitializer ??= gameObject.AddComponent<GameUIInitializer>();
        _uiInitializer.Initialize(
            uiDocument, inventoryRowTemplate, gameConfig,
            _serviceInstaller.Economy, _serviceInstaller.Money,
            _serviceInstaller.Inventory, _serviceInstaller.Shop,
            _serviceInstaller.EventBus, _serviceInstaller.ObjectRegistry,
            productDB
        );
    }

    void Start()
    {
        if (!_isInitialized) return;

        _uiInitializer.SetupPlayerInput();




        if (gameConfig.verboseLogging)
            Debug.Log("[GameManager] Pornit cu succes.");
    }

    void Update()
    {
        if (!_isInitialized) return;
        _uiInitializer?.TickUpdate();
    }

    void OnDestroy()
    {
        if (!_isInitialized) return;

        _uiInitializer?.Cleanup();
        _serviceInstaller?.Cleanup();

        if (gameConfig.verboseLogging)
            Debug.Log("[GameManager] Cleanup finalizat.");
    }

    // ─── Singleton ────────────────────────────────────────────────────────────

    private bool InitializeSingleton()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[GameManager] Duplicat detectat. Se distruge.");
            Destroy(gameObject);
            return false;
        }
        Instance = this;
        return true;
    }

    // ─── Validare configurație ────────────────────────────────────────────────

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (gameConfig == null)
        {
            Debug.LogError("[GameManager] GameConfig neluat din Inspector!");
            valid = false;
        }
        if (productDB == null)
        {
            Debug.LogError("[GameManager] ProductDB neluat din Inspector!");
            valid = false;
        }
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("[GameManager] UIDocument lipsă!");
                valid = false;
            }
        }
        return valid;
    }

    // ─── Backwards-compat API (pentru codul vechi care referențiază GameManager) ──

    public int CurrentMoney => _serviceInstaller?.Money?.CurrentAmount ?? 0;

    public System.Collections.Generic.Dictionary<ProductType, ProductEconomics> marketData
    {
        get
        {
            var ro = _serviceInstaller?.Economy?.MarketData;
            return ro == null
                ? new System.Collections.Generic.Dictionary<ProductType, ProductEconomics>()
                : new System.Collections.Generic.Dictionary<ProductType, ProductEconomics>(ro);
        }
    }

    // Evenimentul se re-subscrie la fiecare add/remove — comportament păstrat din original.
    public event Action OnMoneyChanged
    {
        add
        {
            if (_serviceInstaller?.Money is MoneyManager mm)
                mm.OnMoneyChanged += (_, __) => value?.Invoke();
        }
        remove
        {
            if (_serviceInstaller?.Money is MoneyManager mm)
                mm.OnMoneyChanged -= (_, __) => value?.Invoke();
        }
    }

    public bool TrySpendMoney(int amount) => _serviceInstaller?.Money?.TrySpend(amount) ?? false;
    public void AddMoney(int amount) => _serviceInstaller?.Money?.Add(amount);
    public void UpdateMoneyUI() => (_serviceInstaller?.Money as MoneyManager)?.UpdateUI();
    public void TriggerGameSave() => _saveHandler?.TriggerSave();
}