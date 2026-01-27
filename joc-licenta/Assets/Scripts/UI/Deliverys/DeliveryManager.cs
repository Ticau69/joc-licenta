using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class DeliveryManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset deliveryItemTemplate; // <--- AICI TRAGI TEMPLATE-UL TĂU

    [Header("Game References")]
    [SerializeField] private GameManager gameManager;
    // Vom accesa InventoryService prin ServiceLocator sau GameManager, depinde cum ai structura acum.
    // Presupunem că GameManager are acces la InventoryService.

    private VisualElement root;
    private ScrollView activeDeliveryList;
    private Button newOrderBtn;

    // Lista logică a comenzilor active
    private List<DeliveryOrder> activeOrders = new List<DeliveryOrder>();

    // Dicționar pentru a lega comanda logică de elementul vizual din UI
    private Dictionary<DeliveryOrder, VisualElement> orderToVisualMap = new Dictionary<DeliveryOrder, VisualElement>();

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        // Găsim elementele din Tab-ul DELIVERY
        activeDeliveryList = root.Q<ScrollView>("ActiveDeliveryList");
        newOrderBtn = root.Q<Button>("NewOrderBtn");

        // Curățăm Mock-ul (acel element pus de design)
        activeDeliveryList?.Clear();

        if (newOrderBtn != null)
        {
            newOrderBtn.clicked += OpenSupplyMenu; // Momentan doar simulăm o comandă
        }
    }

    void Update()
    {
        // Actualizăm timerele
        if (activeOrders.Count > 0)
        {
            ProcessDeliveries();
        }
    }

    private void ProcessDeliveries()
    {
        // Iterăm invers ca să putem șterge elemente în siguranță
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            DeliveryOrder order = activeOrders[i];

            // 1. Scădem timpul
            order.timeRemaining -= Time.deltaTime;

            // 2. Actualizăm UI-ul aferent acestei comenzi
            if (orderToVisualMap.ContainsKey(order))
            {
                UpdateVisualItem(order, orderToVisualMap[order]);
            }

            // 3. Verificăm dacă a ajuns camionul
            if (order.IsCompleted)
            {
                CompleteOrder(order);
            }
        }
    }

    // --- LOGICA DE UI ---

    // Această funcție creează vizual rândul folosind Template-ul tău
    private void CreateVisualEntry(DeliveryOrder order)
    {
        if (deliveryItemTemplate == null) return;

        // Instanțiem template-ul
        VisualElement itemInstance = deliveryItemTemplate.Instantiate();

        // Luăm containerul principal din template (de obicei primul copil)
        VisualElement itemRoot = itemInstance.Q<VisualElement>();
        // Sau itemInstance direct, depinde cum ai făcut template-ul. 
        // De obicei TemplateContainer are un copil.

        // Setăm valorile inițiale
        // Asigură-te că în DeliveryItemTemplate ai dat Nume la Label-uri!
        // Ex: Label "OrderName", Label "Timer", Button "UrgentBtn"

        Label nameLabel = itemInstance.Q<Label>("OrderName"); // Verifică numele în Template!
        if (nameLabel != null) nameLabel.text = $"{order.product} ({order.amount} buc)";

        // Adăugăm în listă
        activeDeliveryList.Add(itemInstance);

        // Salvăm legătura
        orderToVisualMap.Add(order, itemInstance);

        // Logică buton Urgent (Opțional)
        Button urgentBtn = itemInstance.Q<Button>("UrgentBtn");
        if (urgentBtn != null)
        {
            urgentBtn.clicked += () => SpeedUpDelivery(order);
        }
    }

    private void UpdateVisualItem(DeliveryOrder order, VisualElement visual)
    {
        Label timerLabel = visual.Q<Label>("Timer"); // Verifică numele în Template!

        if (timerLabel != null)
        {
            // Formatăm timpul 00:00
            int minutes = Mathf.FloorToInt(order.timeRemaining / 60);
            int seconds = Mathf.FloorToInt(order.timeRemaining % 60);

            timerLabel.text = $"ETA: {minutes:00}:{seconds:00}";

            // Schimbăm culoarea dacă mai e puțin timp
            if (order.timeRemaining < 10f) timerLabel.style.color = Color.green;
            else timerLabel.style.color = new Color(1f, 0.75f, 0f); // Galben/Portocaliu
        }
    }

    // --- LOGICA DE JOC ---

    public void CreateNewDelivery(ProductType type, int amount, float duration)
    {
        // 1. Creăm datele
        DeliveryOrder newOrder = new DeliveryOrder(type, amount, duration);

        // 2. Adăugăm în lista logică
        activeOrders.Add(newOrder);

        // 3. Creăm UI-ul
        CreateVisualEntry(newOrder);

        Debug.Log($"[Delivery] Comandă nouă: {type}, ajunge în {duration}s");
    }

    private void CompleteOrder(DeliveryOrder order)
    {
        // 1. Adăugăm marfa în depozit
        // Folosim InventoryService prin ServiceLocator sau referință
        if (ServiceLocator.Instance.TryGet(out IInventoryService inventory))
        {
            inventory.AddStock(order.product, order.amount);
            Debug.Log($"[Delivery] Camionul cu {order.product} a ajuns!");
        }
        else
        {
            Debug.LogError("Nu am găsit InventoryService pentru a descărca marfa!");
        }

        // 2. Ștergem din UI
        if (orderToVisualMap.ContainsKey(order))
        {
            activeDeliveryList.Remove(orderToVisualMap[order]);
            orderToVisualMap.Remove(order);
        }

        // 3. Ștergem din lista logică
        activeOrders.Remove(order);
    }

    // Funcție temporară pentru butonul "Order" (Simulare)
    private void OpenSupplyMenu()
    {
        // Aici ar trebui să deschizi un popup real.
        // Pentru test, comandăm Cola random.
        CreateNewDelivery(ProductType.Cola, 50, 10.0f); // Vine în 10 secunde
    }

    private void SpeedUpDelivery(DeliveryOrder order)
    {
        // Exemplu: Plătești bani ca să vină instant
        // if (gameManager.TrySpendMoney(50)) ...
        order.timeRemaining -= 30f; // Scade 30 secunde
    }
}