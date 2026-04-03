using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class DeliveryManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset deliveryItemTemplate;
    [SerializeField] private VisualTreeAsset catalogItemTemplate;

    [Header("Data References")]
    [SerializeField] private ProductDataSO productDatabase;

    [Header("Config")]
    [SerializeField] private float deliverySpeedBase = 15f;
    [SerializeField] private int maxItemPerOrder = 100; // Limita pe camion
    [Header("Fleet Reference")]
    [SerializeField] private FleetManager fleetManager;
    private Label fleetCapacityLabel;
    private Button upgradeFleetBtn;

    // Referințe UI
    private VisualElement root;
    private ScrollView activeDeliveryList;
    private Button newOrderBtn;
    private VisualElement orderPopup;
    private ScrollView productCatalogList;
    private ScrollView cartList;
    private Label orderTotalLabel;
    private Label capacityLabel; // Capacitatea camionului (din popup)
    private Label globalStorageLabel; // Capacitatea depozitului (din tab-ul Inventar)
    private Button confirmOrderBtn;
    private Button cancelOrderBtn;

    // Logică Internă
    private List<DeliveryOrder> activeOrders = new List<DeliveryOrder>();
    private Dictionary<DeliveryOrder, VisualElement> orderToVisualMap = new Dictionary<DeliveryOrder, VisualElement>();
    private Dictionary<ProductData, int> currentCart = new Dictionary<ProductData, int>();

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        activeDeliveryList = root.Q<ScrollView>("ActiveDeliveryList");
        newOrderBtn = root.Q<Button>("NewOrderBtn");
        activeDeliveryList?.Clear();

        if (newOrderBtn != null) newOrderBtn.clicked += OpenSupplyMenu;

        orderPopup = root.Q<VisualElement>("OrderPopup");
        productCatalogList = root.Q<ScrollView>("ProductCatalogList");
        cartList = root.Q<ScrollView>("CartList");
        orderTotalLabel = root.Q<Label>("OrderTotalLabel");
        confirmOrderBtn = root.Q<Button>("ConfirmOrderBtn");
        cancelOrderBtn = root.Q<Button>("CancelOrderBtn");

        capacityLabel = orderPopup.Q<Label>("Capactitate");
        globalStorageLabel = root.Q<Label>("StorageCapacityLabel"); // Label-ul din tab-ul de inventar
        fleetCapacityLabel = root.Q<Label>("FleetCapacityLabel");
        upgradeFleetBtn = root.Q<Button>("UpgradeFleetBtn");

        if (confirmOrderBtn != null) confirmOrderBtn.clicked += FinalizeOrder;
        if (cancelOrderBtn != null) cancelOrderBtn.clicked += CloseSupplyMenu;

        if (upgradeFleetBtn != null) upgradeFleetBtn.clicked += () => fleetManager.TryUpgradeFleet();

        if (fleetManager != null)
        {
            fleetManager.OnFleetStatusChanged += UpdateFleetUI;
            UpdateFleetUI();
        }

        UpdateGlobalStorageLabel();
    }

    void OnDisable()
    {
        if (fleetManager != null) fleetManager.OnFleetStatusChanged -= UpdateFleetUI;
    }

    void Update()
    {
        if (activeOrders.Count > 0) ProcessDeliveries();
    }

    // =================================================================================
    // CALCUL DEPOZIT (NOU)
    // =================================================================================

    private void CalculateWarehouseCapacity(out int usedSpace, out int maxSpace)
    {
        usedSpace = 0;
        maxSpace = 0;

        // Găsim toate rafturile din scenă
        StorageRacks[] allRacks = FindObjectsByType<StorageRacks>(FindObjectsSortMode.None);

        foreach (var rack in allRacks)
        {
            // Cât încape total pe acest raft
            maxSpace += (rack.maxBoxes * rack.maxAmountPerBox);

            // Cât este ocupat în prezent
            foreach (var box in rack.storedBoxes)
            {
                usedSpace += box.Amount;
            }
        }
    }

    private void UpdateGlobalStorageLabel()
    {
        if (globalStorageLabel != null)
        {
            CalculateWarehouseCapacity(out int used, out int max);
            globalStorageLabel.text = $"{used} / {max}";
            globalStorageLabel.style.color = (used >= max && max > 0) ? Color.red : Color.white;
        }
    }

    // =================================================================================
    // LOGICA DE COMANDĂ (SHOPPING CART)
    // =================================================================================

    private void OpenSupplyMenu()
    {
        if (orderPopup == null) return;

        currentCart.Clear();
        UpdateGlobalStorageLabel(); // Actualizăm spațiul disponibil când deschidem meniul
        UpdateCartUI();
        PopulateCatalog();

        orderPopup.style.display = DisplayStyle.Flex;
    }

    private int GetTotalItemsInCart()
    {
        int total = 0;
        foreach (var quantity in currentCart.Values) total += quantity;
        return total;
    }

    private void CloseSupplyMenu()
    {
        if (orderPopup != null) orderPopup.style.display = DisplayStyle.None;
    }

    private void PopulateCatalog()
    {
        if (productCatalogList == null || productDatabase == null || catalogItemTemplate == null) return;
        productCatalogList.Clear();

        foreach (var product in productDatabase.allProducts)
        {
            VisualElement itemInstance = catalogItemTemplate.Instantiate();
            Label nameLabel = itemInstance.Q<Label>("ProductName");
            IntegerField qtyInput = itemInstance.Q<IntegerField>("QuantityInput");
            Button addBtn = itemInstance.Q<Button>("AddBtn");

            if (nameLabel != null)
            {
                // Am folosit un fallback pentru preț în caz că InflationManager nu e pus încă în scenă
                float price = product.baseCost;
                if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out InflationManager inflation))
                {
                    price = inflation.GetPrice(product.baseCost);
                }
                nameLabel.text = $"{product.productName}\n{price} RON/buc";
            }

            if (qtyInput != null && addBtn != null)
            {
                qtyInput.value = 1;
                qtyInput.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue < 1) qtyInput.SetValueWithoutNotify(1);
                    else if (evt.newValue > maxItemPerOrder) qtyInput.SetValueWithoutNotify(maxItemPerOrder);
                });

                addBtn.clicked += () => AddToCart(product, qtyInput.value);
            }
            productCatalogList.Add(itemInstance);
        }
    }

    private void AddToCart(ProductData product, int amount)
    {
        int currentTotalProducts = GetTotalItemsInCart();

        // 1. Verificăm limita camionului
        if (currentTotalProducts + amount > maxItemPerOrder)
        {
            Debug.LogWarning("Ai atins limita maximă a camionului!");
            return;
        }

        // 2. Verificăm limita Depozitului (NOU)
        CalculateWarehouseCapacity(out int used, out int max);
        int freeSpace = max - used;

        if (currentTotalProducts + amount > freeSpace)
        {
            Debug.LogWarning("Nu ai destul spațiu liber în depozit pentru această comandă!");
            // Opțional: Aici am putea afișa o notificare pe ecran pentru jucător
            return;
        }

        if (currentCart.ContainsKey(product)) currentCart[product] += amount;
        else currentCart.Add(product, amount);

        UpdateCartUI();
    }

    private void UpdateCartUI()
    {
        if (cartList == null) return;
        cartList.Clear();
        float totalCost = 0;

        foreach (var item in currentCart)
        {
            ProductData product = item.Key;
            int qty = item.Value;

            float price = product.baseCost;
            if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out InflationManager inflation))
                price = inflation.GetPrice(product.baseCost);

            float lineCost = price * qty;
            totalCost += lineCost;

            VisualElement cartRow = new VisualElement();
            cartRow.style.flexDirection = FlexDirection.Row;
            cartRow.style.justifyContent = Justify.SpaceBetween;
            cartRow.style.marginBottom = 5;

            Label name = new Label($"{product.productName} x{qty}");
            name.style.color = Color.white;
            Label cost = new Label($"{lineCost} RON");
            cost.style.color = Color.yellow;

            cartRow.Add(name);
            cartRow.Add(cost);
            cartList.Add(cartRow);
        }

        int currentItems = GetTotalItemsInCart();

        if (capacityLabel != null)
        {
            CalculateWarehouseCapacity(out int used, out int max);
            int freeSpace = max - used;

            capacityLabel.text = $"Camion: {currentItems}/{maxItemPerOrder} | Depozit Liber: {freeSpace}";
            capacityLabel.style.color = (currentItems >= maxItemPerOrder || currentItems >= freeSpace) ? Color.red : Color.white;
        }

        if (orderTotalLabel != null) orderTotalLabel.text = $"{totalCost} RON";

        if (confirmOrderBtn != null)
        {
            bool canAfford = GameManager.Instance.CurrentMoney >= totalCost;
            confirmOrderBtn.SetEnabled(totalCost > 0 && canAfford);
            confirmOrderBtn.text = canAfford ? "PLASEAZĂ COMANDA" : "FONDURI INSUFICIENTE";
        }
    }

    private void UpdateFleetUI()
    {
        if (fleetCapacityLabel != null)
        {
            fleetCapacityLabel.text = $"{fleetManager.ActiveTrucks}/{fleetManager.CurrentMaxTrucks} Camioane";
            fleetCapacityLabel.style.color = (fleetManager.ActiveTrucks >= fleetManager.CurrentMaxTrucks) ? Color.red : Color.white;
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

    private void FinalizeOrder()
    {
        float totalCost = 0;
        int totalItems = 0;
        foreach (var item in currentCart)
        {
            totalCost += item.Key.baseCost * item.Value;
            totalItems += item.Value;
        }

        if (!fleetManager.HasAvailableTrucks(1))
        {
            confirmOrderBtn.text = "LIPSĂ CAMIOANE!";
            return;
        }

        if (GameManager.Instance.TrySpendMoney((int)totalCost))
        {
            if (FinanceManager.Instance != null)
            {
                FinanceManager.Instance.RegisterTransaction(TransactionCategory.Marfa_Depozit, (int)totalCost);
            }
            float deliveryTime = deliverySpeedBase + (totalItems / 10f);

            Dictionary<ProductType, int> itemsForOrder = new Dictionary<ProductType, int>();
            foreach (var item in currentCart) itemsForOrder.Add(item.Key.type, item.Value);

            // --- NOU: VERIFICĂM DACA E NOAPTE ---
            bool isNight = false;
            if (TimeManager.Instance != null)
            {
                float hour = TimeManager.Instance.CurrentHour;
                // Dacă e mai târziu de ora de închidere SAU e înainte de 7 dimineața
                if (hour >= TimeManager.Instance.closeHour || hour < 7f)
                {
                    isNight = true;
                }
            }

            CreateNewDelivery(itemsForOrder, deliveryTime, isNight);
            CloseSupplyMenu();
            UpdateGlobalStorageLabel();
        }
    }

    // =================================================================================
    // LOGICA LIVRARE
    // =================================================================================

    private void ProcessDeliveries()
    {
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            DeliveryOrder order = activeOrders[i];

            // --- NOU: Logica de pauză pentru comenzile de noapte ---
            if (order.isNightOrder)
            {
                // Verificăm dacă s-a făcut dimineață (Ora 07:00)
                if (TimeManager.Instance != null && TimeManager.Instance.CurrentHour >= 7f && TimeManager.Instance.CurrentHour < TimeManager.Instance.closeHour)
                {
                    // Forțăm livrarea instant la ora 07:00
                    order.timeRemaining = 0f;
                }
            }
            else
            {
                // Comandă normală de zi, scade timpul
                order.timeRemaining -= Time.deltaTime;
            }

            if (orderToVisualMap.ContainsKey(order))
                UpdateVisualItem(order, orderToVisualMap[order]);

            if (order.IsCompleted) CompleteOrder(order);
        }
    }

    public void CreateNewDelivery(Dictionary<ProductType, int> items, float duration, bool isNight)
    {
        fleetManager.RentTruck();

        DeliveryOrder newOrder = new DeliveryOrder(items, duration, isNight);
        activeOrders.Add(newOrder);
        CreateVisualEntry(newOrder);
    }

    private void CreateVisualEntry(DeliveryOrder order)
    {
        if (deliveryItemTemplate == null) return;
        VisualElement itemInstance = deliveryItemTemplate.Instantiate();

        Label nameLabel = itemInstance.Q<Label>("OrderName");
        if (nameLabel != null)
        {
            int totalAmount = 0;
            foreach (var qty in order.products.Values) totalAmount += qty;

            if (order.products.Count == 1) nameLabel.text = $"{order.products.Keys.First()} ({totalAmount} buc)";
            else nameLabel.text = $"Comandă Mixtă ({totalAmount} buc)";
        }

        Button urgentBtn = itemInstance.Q<Button>("UrgentBtn");
        if (urgentBtn != null)
        {
            // Ascundem butonul de URGENT pentru comenzile de noapte (nu are sens să grăbești timpul de noapte)
            if (order.isNightOrder) urgentBtn.style.display = DisplayStyle.None;
            else urgentBtn.clicked += () => SpeedUpDelivery(order);
        }

        activeDeliveryList.Add(itemInstance);
        orderToVisualMap.Add(order, itemInstance);
    }

    private void UpdateVisualItem(DeliveryOrder order, VisualElement visual)
    {
        Label timerLabel = visual.Q<Label>("ETA");
        if (timerLabel != null)
        {
            if (order.isNightOrder)
            {
                timerLabel.text = "Sosește la 07:00";
                timerLabel.style.color = new Color(0.3f, 0.7f, 1f); // Albastru deschis pentru noapte
            }
            else
            {
                int minutes = Mathf.FloorToInt(order.timeRemaining / 60);
                int seconds = Mathf.FloorToInt(order.timeRemaining % 60);
                timerLabel.text = $"ETA: {minutes:00}:{seconds:00}";

                if (order.timeRemaining < 10f) timerLabel.style.color = Color.green;
                else timerLabel.style.color = new Color(1f, 0.75f, 0f);
            }
        }
    }

    private void CompleteOrder(DeliveryOrder order)
    {
        fleetManager.ReturnTruck();

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IInventoryService inventory))
        {
            foreach (var item in order.products) inventory.AddStock(item.Key, item.Value);
        }

        if (orderToVisualMap.ContainsKey(order))
        {
            activeDeliveryList.Remove(orderToVisualMap[order]);
            orderToVisualMap.Remove(order);
        }
        activeOrders.Remove(order);

        // Actualizăm UI-ul depozitului odată ce a ajuns marfa
        UpdateGlobalStorageLabel();
    }

    private void SpeedUpDelivery(DeliveryOrder order)
    {
        if (GameManager.Instance.TrySpendMoney(50)) order.timeRemaining -= 30f;
    }
}