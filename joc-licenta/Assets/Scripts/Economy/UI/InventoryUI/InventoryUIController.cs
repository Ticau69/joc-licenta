using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Orchestrator for the Inventory UI tab.
/// Owns the sub-panels and routes events; contains no layout or business logic itself.
/// </summary>
public class InventoryUIController : MonoBehaviour
{
    // ── Serialized ──────────────────────────────────────────────────────────
    [SerializeField] private VisualTreeAsset inventoryRowTemplate;

    // ── Private state ────────────────────────────────────────────────────────
    private VisualElement _inventoryTab;
    private float _updateTimer;

    private IEconomyService _economy;
    private IInventoryService _inventory;
    private IEventBus _eventBus;
    private GameConfigSO _config;
    private ProductDataSO _productDB;

    private InventoryPriceState _priceState;
    private InventoryListPanel _listPanel;
    private InventoryDetailsPanel _detailsPanel;

    // ── Public Init ──────────────────────────────────────────────────────────

    public void Initialize(
        VisualElement root,
        IEconomyService economy,
        IEventBus eventBus,
        GameConfigSO config,
        IInventoryService inventory,
        ProductDataSO productDB,
        VisualTreeAsset rowTemplate = null)
    {
        _economy = economy ?? throw new System.ArgumentNullException(nameof(economy));
        _inventory = inventory ?? throw new System.ArgumentNullException(nameof(inventory));
        _productDB = productDB ?? throw new System.ArgumentNullException(nameof(productDB));
        _eventBus = eventBus ?? throw new System.ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new System.ArgumentNullException(nameof(config));

        _inventoryTab = root.Q<VisualElement>("Inventory");

        if (rowTemplate != null)
            UIRowFactory.SetRowTemplate(rowTemplate);
        else if (inventoryRowTemplate != null)
            UIRowFactory.SetRowTemplate(inventoryRowTemplate);

        // Shared price-state (both panels read/write this)
        _priceState = new InventoryPriceState();

        // Sub-panels
        _listPanel = new InventoryListPanel(root, _economy, _inventory, _config, _productDB, _priceState);
        _detailsPanel = new InventoryDetailsPanel(root, _economy, _inventory, _config, _priceState);

        // Wire up cross-panel callback: list asks details to show a product
        _listPanel.OnShowProductDetails = _detailsPanel.ShowProduct;

        SubscribeEvents();

        if (_config.verboseLogging)
            Debug.Log("[InventoryUI] Initialized successfully");
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void OnDestroy()
    {
        UnsubscribeEvents();
        _detailsPanel?.Dispose();
    }

    // ── Called each frame by the parent UIManager ────────────────────────────

    public void UpdateIfNeeded()
    {
        if (_inventoryTab == null || _inventoryTab.style.display == DisplayStyle.None)
            return;

        _updateTimer += Time.deltaTime;

        if (_updateTimer >= _config.inventoryUpdateInterval || _listPanel.NeedsRefresh)
        {
            _listPanel.Refresh();
            _updateTimer = 0f;
        }
    }

    // ── Event subscriptions ──────────────────────────────────────────────────

    private void SubscribeEvents()
    {
        _eventBus.Subscribe<StockChangedEvent>(OnStockChanged);
        _eventBus.Subscribe<ProductPricedTooHighEvent>(OnProductPricedTooHigh);
    }

    private void UnsubscribeEvents()
    {
        _eventBus?.Unsubscribe<StockChangedEvent>(OnStockChanged);
        _eventBus?.Unsubscribe<ProductPricedTooHighEvent>(OnProductPricedTooHigh);
    }

    private void OnStockChanged(StockChangedEvent evt)
    {
        _listPanel.MarkDirty();
        _detailsPanel.RefreshIfViewing(evt.Product);
    }

    private void OnProductPricedTooHigh(ProductPricedTooHighEvent evt)
    {
        if (_priceState.MarkOverpriced(evt.Product))
        {
            _listPanel.MarkDirty();
            _detailsPanel.RefreshIfViewing(evt.Product);
        }
    }
}