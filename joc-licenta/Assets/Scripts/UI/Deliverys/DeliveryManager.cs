using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Gestionează UI-ul flotei și afișează vizual comenzile active de la furnizori.
/// Logica de livrare (inventar, plăți, zile) e în SupplierOrderSystem.
/// </summary>
public class DeliveryManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset deliveryItemTemplate;

    [Header("References")]
    [SerializeField] private FleetManager fleetManager;

    // FIX #6: _root nu trebuie stocat ca field — e folosit doar în InitUI()
    private ScrollView _activeDeliveryList;
    private Label _fleetCapacityLabel;
    private Button _upgradeFleetBtn;

    // FIX #1: flag ca să nu apelăm InitUI de două ori → dubla înregistrare clicked
    private bool _uiInitialized = false;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void Awake()
    {
        InitUI();
    }

    void OnEnable()
    {
        if (fleetManager != null)
        {
            fleetManager.OnFleetStatusChanged += UpdateFleetUI;
            UpdateFleetUI();
        }
    }

    void OnDisable()
    {
        if (fleetManager != null)
            fleetManager.OnFleetStatusChanged -= UpdateFleetUI;
    }

    private void InitUI()
    {
        // FIX #1: Previne dubla înregistrare a clicked pe _upgradeFleetBtn
        if (_uiInitialized) return;

        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;

        _activeDeliveryList = root.Q<ScrollView>("ActiveDeliveryList");
        _activeDeliveryList?.Clear();

        _fleetCapacityLabel = root.Q<Label>("FleetCapacityLabel");
        _upgradeFleetBtn = root.Q<Button>("UpgradeFleetBtn");

        if (_upgradeFleetBtn != null)
            _upgradeFleetBtn.clicked += () => fleetManager?.TryUpgradeFleet();

        _uiInitialized = true;
    }

    // =========================================================================
    // FLEET UI
    // =========================================================================

    private void UpdateFleetUI()
    {
        if (_fleetCapacityLabel != null)
        {
            _fleetCapacityLabel.text = $"{fleetManager.ActiveTrucks}/{fleetManager.CurrentMaxTrucks} Camioane";
            _fleetCapacityLabel.style.color = fleetManager.ActiveTrucks >= fleetManager.CurrentMaxTrucks
                ? Color.red : Color.white;
        }

        if (_upgradeFleetBtn != null)
        {
            _upgradeFleetBtn.text = fleetManager.CanUpgrade()
                ? $"Upgrade Flotă\n({fleetManager.GetNextUpgradeCost()} RON)"
                : "Flotă Maximă";
            _upgradeFleetBtn.SetEnabled(fleetManager.CanUpgrade());
        }
    }

    // =========================================================================
    // API PUBLIC — apelat de SupplierOrderSystem după plasarea comenzii
    // =========================================================================

    public void RegisterSupplierOrder(SupplierDeliveryOrder order)
    {
        // Lazy init cu protecție împotriva dublei înregistrări
        if (!_uiInitialized) InitUI();

        if (_activeDeliveryList == null)
        {
            Debug.LogError("[DeliveryManager] 'ActiveDeliveryList' ScrollView nu a fost găsit în UXML!");
            return;
        }
        if (deliveryItemTemplate == null)
        {
            Debug.LogError("[DeliveryManager] deliveryItemTemplate nu e asignat în Inspector!");
            return;
        }

        VisualElement item = deliveryItemTemplate.Instantiate();

        var nameLabel = item.Q<Label>("OrderName");
        if (nameLabel != null)
            nameLabel.text = $"{order.Product} ({order.Quantity} buc) — {order.Supplier.supplierName}";

        var etaLabel = item.Q<Label>("ETA");
        if (etaLabel != null)
        {
            etaLabel.text = order.Supplier.deliveryDays == 0
                ? "Livrare instant"
                : $"Ziua {order.DeliveryDay}";
            etaLabel.style.color = new Color(0.3f, 0.7f, 1f);
        }

        var urgentBtn = item.Q<Button>("UrgentBtn");
        if (urgentBtn != null)
            urgentBtn.style.display = DisplayStyle.None;

        _activeDeliveryList.Add(item);

        Debug.Log($"[DeliveryManager] Vizual adăugat: {order.Product} x{order.Quantity} " +
                  $"de la {order.Supplier.supplierName} — ziua {order.DeliveryDay}");

        StartCoroutine(RemoveWhenDelivered(order, item));
    }

    private IEnumerator RemoveWhenDelivered(SupplierDeliveryOrder order, VisualElement visual)
    {
        while (order.Status == OrderStatus.Pending)
            yield return new WaitForSeconds(1f);

        _activeDeliveryList?.Remove(visual);
    }
}