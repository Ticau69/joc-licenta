using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class DeliveryManager : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset deliveryItemTemplate;

    [Header("Config")]
    [SerializeField] private float deliverySpeedBase = 15f;

    [Header("References")]
    [SerializeField] private FleetManager fleetManager;
    [SerializeField] private SupplierPanelUI supplierPanelUI; // deschide popup-ul la click pe Furnizori

    // =========================================================================
    // PRIVATE UI
    // =========================================================================

    private VisualElement root;
    private ScrollView activeDeliveryList;
    private Label fleetCapacityLabel;
    private Button upgradeFleetBtn;
    private Button supplierOrderBtn;

    // =========================================================================
    // STATE
    // =========================================================================

    private readonly List<DeliveryOrder> activeOrders = new();
    private readonly Dictionary<DeliveryOrder, VisualElement> orderToVisualMap = new();

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        // --- Delivery list ---
        activeDeliveryList = root.Q<ScrollView>("ActiveDeliveryList");
        activeDeliveryList?.Clear();

        // --- Fleet UI ---
        fleetCapacityLabel = root.Q<Label>("FleetCapacityLabel");
        upgradeFleetBtn = root.Q<Button>("UpgradeFleetBtn");

        if (upgradeFleetBtn != null)
            upgradeFleetBtn.clicked += () => fleetManager?.TryUpgradeFleet();

        if (fleetManager != null)
        {
            fleetManager.OnFleetStatusChanged += UpdateFleetUI;
            UpdateFleetUI();
        }

        // --- Supplier button → deleagă la SupplierPanelUI ---
        supplierOrderBtn = root.Q<Button>("SupplierOrderBtn");
        if (supplierOrderBtn != null)
            supplierOrderBtn.clicked += () => supplierPanelUI?.Open();
    }

    void OnDisable()
    {
        if (fleetManager != null)
            fleetManager.OnFleetStatusChanged -= UpdateFleetUI;
    }

    void Update()
    {
        if (activeOrders.Count > 0)
            ProcessDeliveries();
    }

    // =========================================================================
    // FLEET UI
    // =========================================================================

    private void UpdateFleetUI()
    {
        if (fleetCapacityLabel != null)
        {
            fleetCapacityLabel.text = $"{fleetManager.ActiveTrucks}/{fleetManager.CurrentMaxTrucks} Camioane";
            fleetCapacityLabel.style.color = (fleetManager.ActiveTrucks >= fleetManager.CurrentMaxTrucks)
                ? Color.red
                : Color.white;
        }

        if (upgradeFleetBtn != null)
        {
            if (fleetManager.CanUpgrade())
                upgradeFleetBtn.text = $"Upgrade Flotă\n({fleetManager.GetNextUpgradeCost()} RON)";
            else
                upgradeFleetBtn.text = "Flotă Maximă";

            upgradeFleetBtn.SetEnabled(fleetManager.CanUpgrade());
        }
    }

    // =========================================================================
    // LIVRĂRI — API PUBLIC (apelat de SupplierOrderSystem după confirmare)
    // =========================================================================

    /// <summary>
    /// Creează o livrare nouă. Apelat din SupplierOrderSystem.TryPlaceOrder().
    /// </summary>
    public void CreateNewDelivery(Dictionary<ProductType, int> items, float duration, bool isNight)
    {
        if (!fleetManager.HasAvailableTrucks(1))
        {
            Debug.LogWarning("[DeliveryManager] Niciun camion disponibil!");
            return;
        }

        fleetManager.RentTruck();

        DeliveryOrder newOrder = new DeliveryOrder(items, duration, isNight);
        activeOrders.Add(newOrder);
        CreateVisualEntry(newOrder);
    }

    // =========================================================================
    // LIVRĂRI — INTERN
    // =========================================================================

    private void ProcessDeliveries()
    {
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            DeliveryOrder order = activeOrders[i];

            if (order.isNightOrder)
            {
                // Livrare de noapte — sosește instant la ora 07:00
                if (TimeManager.Instance != null
                    && TimeManager.Instance.CurrentHour >= 7f
                    && TimeManager.Instance.CurrentHour < TimeManager.Instance.closeHour)
                {
                    order.timeRemaining = 0f;
                }
            }
            else
            {
                order.timeRemaining -= Time.deltaTime;
            }

            if (orderToVisualMap.TryGetValue(order, out var visual))
                UpdateVisualItem(order, visual);

            if (order.IsCompleted)
                CompleteOrder(order);
        }
    }

    private void CreateVisualEntry(DeliveryOrder order)
    {
        if (deliveryItemTemplate == null || activeDeliveryList == null) return;

        VisualElement itemInstance = deliveryItemTemplate.Instantiate();

        Label nameLabel = itemInstance.Q<Label>("OrderName");
        if (nameLabel != null)
        {
            int totalAmount = order.products.Values.Sum();
            nameLabel.text = order.products.Count == 1
                ? $"{order.products.Keys.First()} ({totalAmount} buc)"
                : $"Comandă Mixtă ({totalAmount} buc)";
        }

        Button urgentBtn = itemInstance.Q<Button>("UrgentBtn");
        if (urgentBtn != null)
        {
            if (order.isNightOrder)
                urgentBtn.style.display = DisplayStyle.None;
            else
                urgentBtn.clicked += () => SpeedUpDelivery(order);
        }

        activeDeliveryList.Add(itemInstance);
        orderToVisualMap[order] = itemInstance;
    }

    private void UpdateVisualItem(DeliveryOrder order, VisualElement visual)
    {
        Label timerLabel = visual.Q<Label>("ETA");
        if (timerLabel == null) return;

        if (order.isNightOrder)
        {
            timerLabel.text = "Sosește la 07:00";
            timerLabel.style.color = new Color(0.3f, 0.7f, 1f);
        }
        else
        {
            int minutes = Mathf.FloorToInt(order.timeRemaining / 60);
            int seconds = Mathf.FloorToInt(order.timeRemaining % 60);
            timerLabel.text = $"ETA: {minutes:00}:{seconds:00}";
            timerLabel.style.color = order.timeRemaining < 10f ? Color.green : new Color(1f, 0.75f, 0f);
        }
    }

    private void CompleteOrder(DeliveryOrder order)
    {
        fleetManager.ReturnTruck();

        // Adaugă stocul în inventar
        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IInventoryService inventory))
        {
            foreach (var item in order.products)
                inventory.AddStock(item.Key, item.Value);
        }

        // Curăță vizualul
        if (orderToVisualMap.TryGetValue(order, out var visual))
        {
            activeDeliveryList?.Remove(visual);
            orderToVisualMap.Remove(order);
        }

        activeOrders.Remove(order);
    }

    private void SpeedUpDelivery(DeliveryOrder order)
    {
        if (GameManager.Instance.TrySpendMoney(50))
            order.timeRemaining -= 30f;
    }
}