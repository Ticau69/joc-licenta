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
    // MODIFICARE 1: Referință directă la baza de date (trage fișierul aici în Inspector)
    [SerializeField] private ProductDataSO productDatabase;

    [Header("Config")]
    [SerializeField] private float deliverySpeedBase = 15f;
    [Header("Fleet Reference")]
    [SerializeField] private FleetManager fleetManager;
    private Label fleetCapacityLabel;
    private Button upgradeFleetBtn;

    // Referințe UI Tab Principal
    private VisualElement root;
    private ScrollView activeDeliveryList;
    private Button newOrderBtn;

    // Referințe UI Popup Comandă
    private VisualElement orderPopup;
    private ScrollView productCatalogList;
    private ScrollView cartList;
    private Label orderTotalLabel;
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

        // --- 1. Găsim elementele Tab-ului Principal ---
        activeDeliveryList = root.Q<ScrollView>("ActiveDeliveryList");
        newOrderBtn = root.Q<Button>("NewOrderBtn");
        activeDeliveryList?.Clear();

        if (newOrderBtn != null) newOrderBtn.clicked += OpenSupplyMenu;

        // --- 2. Găsim elementele Popup-ului de Comandă ---
        orderPopup = root.Q<VisualElement>("OrderPopup");
        productCatalogList = root.Q<ScrollView>("ProductCatalogList");
        cartList = root.Q<ScrollView>("CartList");
        orderTotalLabel = root.Q<Label>("OrderTotalLabel");
        confirmOrderBtn = root.Q<Button>("ConfirmOrderBtn");
        cancelOrderBtn = root.Q<Button>("CancelOrderBtn");

        fleetCapacityLabel = root.Q<Label>("FleetCapacityLabel");
        upgradeFleetBtn = root.Q<Button>("UpgradeFleetBtn");

        if (confirmOrderBtn != null) confirmOrderBtn.clicked += FinalizeOrder;
        if (cancelOrderBtn != null) cancelOrderBtn.clicked += CloseSupplyMenu;

        if (upgradeFleetBtn != null)
            upgradeFleetBtn.clicked += () => fleetManager.TryUpgradeFleet();

        // Ne abonăm la schimbări ca să actualizăm textul
        if (fleetManager != null)
        {
            fleetManager.OnFleetStatusChanged += UpdateFleetUI;
            UpdateFleetUI(); // Actualizare inițială
        }
    }

    void OnDisable()
    {
        if (fleetManager != null)
            fleetManager.OnFleetStatusChanged -= UpdateFleetUI;
    }

    void Update()
    {
        if (activeOrders.Count > 0) ProcessDeliveries();
    }

    // =================================================================================
    // LOGICA DE COMANDĂ (SHOPPING CART)
    // =================================================================================

    private void OpenSupplyMenu()
    {
        if (orderPopup == null) return;

        currentCart.Clear();
        UpdateCartUI();
        PopulateCatalog();

        orderPopup.style.display = DisplayStyle.Flex;
    }

    private void CloseSupplyMenu()
    {
        if (orderPopup != null) orderPopup.style.display = DisplayStyle.None;
    }

    private void PopulateCatalog()
    {
        if (productCatalogList == null) return;
        productCatalogList.Clear();

        if (productDatabase == null)
        {
            Debug.LogError("[Delivery] Nu ai atașat ProductDatabase în Inspector!");
            return;
        }

        if (catalogItemTemplate == null)
        {
            Debug.LogError("[Delivery] Nu ai atașat 'Catalog Item Template' în Inspector!");
            return;
        }

        foreach (var product in productDatabase.allProducts)
        {
            // 1. Instanțiem Template-ul creat în UXML
            VisualElement itemInstance = catalogItemTemplate.Instantiate();

            // 2. Găsim elementele din interiorul template-ului după nume
            Label nameLabel = itemInstance.Q<Label>("ProductName");
            IntegerField qtyInput = itemInstance.Q<IntegerField>("QuantityInput");
            Button addBtn = itemInstance.Q<Button>("AddBtn");

            // 3. Populăm datele
            if (nameLabel != null)
            {
                nameLabel.text = $"{product.productName}\n{product.baseCost} RON/buc";
            }

            // 4. Configurăm logica
            if (qtyInput != null && addBtn != null)
            {
                // Ne asigurăm că nu scrie valori negative
                qtyInput.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue < 1) qtyInput.value = 1;
                });

                // Când apasă butonul, citim valoarea CURENTĂ din input-ul de lângă el
                addBtn.clicked += () =>
                {
                    AddToCart(product, qtyInput.value);
                };
            }

            // 5. Adăugăm rândul în listă
            productCatalogList.Add(itemInstance);
        }
    }

    private void AddToCart(ProductData product, int amount)
    {
        if (currentCart.ContainsKey(product))
            currentCart[product] += amount;
        else
            currentCart.Add(product, amount);

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
            float lineCost = product.baseCost * qty;
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

        if (orderTotalLabel != null)
            orderTotalLabel.text = $"{totalCost} RON";

        if (confirmOrderBtn != null)
        {
            // Verificăm banii prin GameManager (Singleton e ok aici pentru bani)
            bool canAfford = GameManager.Instance.CurrentMoney >= totalCost;
            confirmOrderBtn.SetEnabled(totalCost > 0 && canAfford);
            confirmOrderBtn.text = canAfford ? "PLASEAZĂ COMANDA" : "FONDURI INSUFICIENTE";
        }
    }

    private void UpdateFleetUI()
    {
        if (fleetCapacityLabel != null)
        {
            // Ex: "2/5 Camioane"
            fleetCapacityLabel.text = $"{fleetManager.ActiveTrucks}/{fleetManager.CurrentMaxTrucks} Camioane";

            // Schimbăm culoarea dacă e plin
            if (fleetManager.ActiveTrucks >= fleetManager.CurrentMaxTrucks)
                fleetCapacityLabel.style.color = Color.red;
            else
                fleetCapacityLabel.style.color = Color.white;
        }

        // Actualizăm textul butonului de Upgrade cu prețul
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
        // 1. Calculăm costul
        float totalCost = 0;
        foreach (var item in currentCart) totalCost += item.Key.baseCost * item.Value;

        // 2. Calculăm CÂTE camioane ne trebuie
        // (Presupunem 1 camion per tip de produs, cum am discutat)
        int trucksNeeded = currentCart.Count;

        // 3. VERIFICARE FLOTĂ
        if (!fleetManager.HasAvailableTrucks(trucksNeeded))
        {
            Debug.LogWarning("Nu ai destule camioane libere pentru această comandă!");
            // Aici poți schimba textul butonului în "LIPSĂ CAMIOANE" pentru 2 secunde
            confirmOrderBtn.text = "LIPSĂ CAMIOANE!";
            return;
        }

        // 4. Dacă avem bani ȘI camioane -> Executăm
        if (GameManager.Instance.TrySpendMoney((int)totalCost))
        {
            foreach (var item in currentCart)
            {
                float deliveryTime = deliverySpeedBase + (item.Value / 10f);

                // Chemăm funcția care consumă camionul
                CreateNewDelivery(item.Key.type, item.Value, deliveryTime);
            }
            CloseSupplyMenu();
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
            order.timeRemaining -= Time.deltaTime;

            if (orderToVisualMap.ContainsKey(order))
                UpdateVisualItem(order, orderToVisualMap[order]);

            if (order.IsCompleted) CompleteOrder(order);
        }
    }

    public void CreateNewDelivery(ProductType type, int amount, float duration)
    {
        // Ocupăm un camion
        fleetManager.RentTruck();

        DeliveryOrder newOrder = new DeliveryOrder(type, amount, duration);
        activeOrders.Add(newOrder);
        CreateVisualEntry(newOrder);
    }
    private void CreateVisualEntry(DeliveryOrder order)
    {
        if (deliveryItemTemplate == null) return;
        VisualElement itemInstance = deliveryItemTemplate.Instantiate();

        Label nameLabel = itemInstance.Q<Label>("OrderName");
        if (nameLabel != null) nameLabel.text = $"{order.product} ({order.amount} buc)";

        Button urgentBtn = itemInstance.Q<Button>("UrgentBtn");
        if (urgentBtn != null) urgentBtn.clicked += () => SpeedUpDelivery(order);

        activeDeliveryList.Add(itemInstance);
        orderToVisualMap.Add(order, itemInstance);
    }

    private void UpdateVisualItem(DeliveryOrder order, VisualElement visual)
    {
        Label timerLabel = visual.Q<Label>("ETA");
        if (timerLabel != null)
        {
            int minutes = Mathf.FloorToInt(order.timeRemaining / 60);
            int seconds = Mathf.FloorToInt(order.timeRemaining % 60);
            timerLabel.text = $"ETA: {minutes:00}:{seconds:00}";

            if (order.timeRemaining < 10f) timerLabel.style.color = Color.green;
            else timerLabel.style.color = new Color(1f, 0.75f, 0f);
        }
    }

    private void CompleteOrder(DeliveryOrder order)
    {
        // Eliberăm camionul
        fleetManager.ReturnTruck();

        if (ServiceLocator.Instance.TryGet(out IInventoryService inventory))
        {
            inventory.AddStock(order.product, order.amount);
        }

        if (orderToVisualMap.ContainsKey(order))
        {
            activeDeliveryList.Remove(orderToVisualMap[order]);
            orderToVisualMap.Remove(order);
        }
        activeOrders.Remove(order);
    }

    private void SpeedUpDelivery(DeliveryOrder order)
    {
        if (GameManager.Instance.TrySpendMoney(50))
        {
            order.timeRemaining -= 30f;
        }
    }
}