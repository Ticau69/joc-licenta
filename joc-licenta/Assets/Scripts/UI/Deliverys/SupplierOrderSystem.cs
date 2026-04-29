using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Gestionează comenzile plasate la furnizori specifici.
/// Lucrează alături de DeliveryManager (camion) și FleetManager.
/// </summary>
public class SupplierOrderSystem : MonoBehaviour
{
    public static SupplierOrderSystem Instance { get; private set; }

    [Header("Referințe")]
    [SerializeField] private FleetManager fleetManager;

    public event Action<SupplierDeliveryOrder> OnOrderPlaced;
    public event Action<SupplierDeliveryOrder> OnOrderDelivered;

    private List<SupplierDeliveryOrder> _activeOrders = new List<SupplierDeliveryOrder>();
    public IReadOnlyList<SupplierDeliveryOrder> ActiveOrders => _activeOrders;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (fleetManager == null)
            fleetManager = FindFirstObjectByType<FleetManager>();
    }

    void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += OnDayChanged;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= OnDayChanged;
    }

    // ── Plasare comandă ───────────────────────────────────────────────────────

    public bool TryPlaceOrder(
        FurnizoriSO supplier,
        ProductType product,
        int quantity,
        PaymentType paymentType,
        out string errorMessage)
    {
        errorMessage = "";

        if (supplier == null)
        {
            errorMessage = "Furnizor invalid!";
            return false;
        }

        if (!SupplierRelationshipManager.Instance.CanOrder(supplier))
        {
            int debt = SupplierRelationshipManager.Instance.GetPendingDebt(supplier);
            errorMessage = $"{supplier.supplierName} refuză comenzile!\n" +
                           $"Achitați datoria de {debt} RON mai întâi.";
            return false;
        }

        // Verificăm camioane disponibile
        if (fleetManager != null && !fleetManager.HasAvailableTrucks(1))
        {
            errorMessage = "Nu ai camioane disponibile! Fă upgrade la flotă.";
            return false;
        }

        float pricePerUnit = SupplierRelationshipManager.Instance
            .GetFinalPrice(supplier, product);
        int totalCost = Mathf.RoundToInt(pricePerUnit * quantity);

        // Plată imediată — verificăm banii
        if (paymentType == PaymentType.Immediate)
        {
            if (!ServiceLocator.Instance.TryGet(out IMoneyService money) ||
                !money.TrySpend(totalCost))
            {
                errorMessage = $"Fonduri insuficiente! Necesar: {totalCost} RON";
                return false;
            }

            if (FinanceManager.Instance != null)
                FinanceManager.Instance.RegisterTransaction(
                    TransactionCategory.Marfa_Depozit, totalCost);
        }

        // Credit — disponibil doar la relație Prietenos
        if (paymentType == PaymentType.Credit)
        {
            var status = SupplierRelationshipManager.Instance.GetStatus(supplier);
            if (!supplier.allowsCredit || status != RelationshipStatus.Friendly)
            {
                errorMessage = "Creditul e disponibil doar cu furnizori Prietenoși!";
                return false;
            }
            totalCost = Mathf.RoundToInt(totalCost * (1f + supplier.creditInterestRate));
        }

        int currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;

        var order = new SupplierDeliveryOrder
        {
            OrderId = Guid.NewGuid().ToString("N").Substring(0, 8),
            Supplier = supplier,
            Product = product,
            Quantity = quantity,
            TotalCost = totalCost,
            Payment = paymentType,
            OrderDay = currentDay,
            DeliveryDay = currentDay + supplier.deliveryDays,
            Status = OrderStatus.Pending
        };

        _activeOrders.Add(order);

        // Plata la livrare/credit → înregistrăm datoria
        if (paymentType != PaymentType.Immediate)
            SupplierRelationshipManager.Instance.AddPendingDebt(supplier, totalCost);

        // Înregistrăm camionul folosit
        fleetManager?.RentTruck();

        SupplierRelationshipManager.Instance.OnOrderPlaced(supplier);
        OnOrderPlaced?.Invoke(order);

        Debug.Log($"[SupplierOrder] {quantity}x {product} de la {supplier.supplierName} " +
                  $"— {totalCost} RON ({paymentType})");
        return true;
    }

    // ── Procesare zilnică ─────────────────────────────────────────────────────

    private void OnDayChanged()
    {
        int today = TimeManager.Instance.CurrentDay;

        for (int i = _activeOrders.Count - 1; i >= 0; i--)
        {
            var order = _activeOrders[i];
            if (order.Status != OrderStatus.Pending) continue;
            if (order.DeliveryDay > today) continue;

            DeliverOrder(order);

            if (order.Payment == PaymentType.OnDelivery ||
                order.Payment == PaymentType.Credit)
            {
                if (ServiceLocator.Instance.TryGet(out IMoneyService money))
                {
                    if (money.TrySpend(order.TotalCost))
                    {
                        SupplierRelationshipManager.Instance
                            .OnPaymentMade(order.Supplier, onTime: true);

                        if (FinanceManager.Instance != null)
                            FinanceManager.Instance.RegisterTransaction(
                                TransactionCategory.Marfa_Depozit, order.TotalCost);
                    }
                    else
                    {
                        SupplierRelationshipManager.Instance
                            .OnPaymentMade(order.Supplier, onTime: false);
                        Debug.LogWarning($"[SupplierOrder] Fonduri insuficiente pentru plata la livrare!");
                    }
                }
            }

            order.Status = OrderStatus.Delivered;
            fleetManager?.ReturnTruck();
            _activeOrders.RemoveAt(i);
        }
    }

    private void DeliverOrder(SupplierDeliveryOrder order)
    {
        if (ServiceLocator.Instance.TryGet(out IInventoryService inventory))
            inventory.AddStock(order.Product, order.Quantity);

        OnOrderDelivered?.Invoke(order);
        Debug.Log($"[SupplierOrder] Livrat: {order.Quantity}x {order.Product} " +
                  $"de la {order.Supplier.supplierName}");
    }

    // ── Plată manuală datorie ─────────────────────────────────────────────────

    public bool TryPayDebt(FurnizoriSO supplier)
    {
        int debt = SupplierRelationshipManager.Instance.GetPendingDebt(supplier);
        if (debt <= 0) return true;

        if (!ServiceLocator.Instance.TryGet(out IMoneyService money) ||
            !money.TrySpend(debt))
        {
            Debug.LogWarning($"[SupplierOrder] Fonduri insuficiente pentru datorie: {debt} RON");
            return false;
        }

        SupplierRelationshipManager.Instance.OnPaymentMade(supplier, onTime: false);
        return true;
    }
}