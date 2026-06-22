using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Inițializează UI-ul jocului și input-ul jucătorului.
/// Adăugat dinamic de GameManager în OnEnable.
/// </summary>
public class GameUIInitializer : MonoBehaviour
{
    private InventoryUIController _inventoryUI;
    private ShelfUIController _shelfUI;
    private PlayerInput _playerInput;
    private GameConfigSO _config;

    public void Initialize(
        UIDocument uiDocument,
        VisualTreeAsset inventoryRowTemplate,
        GameConfigSO config,
        IEconomyService economy,
        IMoneyService money,
        IInventoryService inventory,
        IShopService shop,
        IEventBus eventBus,
        IObjectRegistry objectRegistry,
        ProductDataSO productDB)
    {
        _config = config;

        if (uiDocument == null)
        {
            Debug.LogError("[GameUIInitializer] UIDocument este null.");
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[GameUIInitializer] rootVisualElement este null.");
            return;
        }

        // Inițializăm MoneyUI
        (money as MoneyManager)?.Initialize(root);

        // Creăm controller-ele dacă nu există deja
        _inventoryUI ??= gameObject.AddComponent<InventoryUIController>();
        _shelfUI ??= gameObject.AddComponent<ShelfUIController>();
        PlayerProfileUIController playerProfileUI = gameObject.AddComponent<PlayerProfileUIController>();

        _inventoryUI.Initialize(root, economy, eventBus, config, inventory, productDB, inventoryRowTemplate);
        _shelfUI.Initialize(root, economy, shop, eventBus, objectRegistry, config);
        playerProfileUI.Initialize(root, eventBus);

        if (config.verboseLogging)
            Debug.Log("[GameUIInitializer] UI inițializat.");
    }

    public void SetupPlayerInput()
    {
        _playerInput = FindFirstObjectByType<PlayerInput>();

        if (_playerInput != null)
            _playerInput.OnObjectClicked += _shelfUI.SelectObject;
        else
            Debug.LogWarning("[GameUIInitializer] PlayerInput nu a fost găsit în scenă.");
    }

    /// <summary>Apelat din GameManager.Update.</summary>
    public void TickUpdate() => _inventoryUI?.UpdateIfNeeded();

    public void Cleanup()
    {
        if (_playerInput != null && _shelfUI != null)
            _playerInput.OnObjectClicked -= _shelfUI.SelectObject;
    }
}