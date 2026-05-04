// FIX #5: Scos `using System.Collections.Generic` — List<> nu e folosit în acest fișier
using UnityEngine;

/// <summary>
/// Comandă plasată la un furnizor specific.
/// </summary>
[System.Serializable]
public class SupplierDeliveryOrder
{
    public string OrderId;
    public FurnizoriSO Supplier;
    public ProductType Product;
    public int Quantity;
    public int TotalCost;
    public PaymentType Payment;
    public int OrderDay;
    public int DeliveryDay;
    public OrderStatus Status;
}

public enum PaymentType { Immediate, OnDelivery, Credit }
public enum OrderStatus { Pending, Delivered, Failed }