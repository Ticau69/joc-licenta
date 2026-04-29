using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "FurnizoriSO", menuName = "Scriptable Objects/FurnizoriSO")]
public class FurnizoriSO : ScriptableObject
{

    [Header("Identitate")]
    public string supplierName = "Furnizor Nou";
    public Sprite supplierIcon;
    [TextArea(2, 3)]
    public string description = "";

    [Header("Produse disponibile")]
    [Tooltip("Produsele pe care acest furnizor le poate livra.")]
    public List<SupplierProduct> products = new List<SupplierProduct>();

    [Header("Livrare")]
    [Tooltip("0 = livrare instant, 1 = a doua zi, etc.")]
    public int deliveryDays = 1;
    [Tooltip("Stoc maxim disponibil per produs per zi.")]
    public int maxStockPerDay = 200;

    [Header("Relație inițială")]
    [Range(0, 100)]
    public int startingRelationship = 50;

    [Header("Plată")]
    public bool allowsPayOnDelivery = true;
    public bool allowsCredit = false;
    [Tooltip("Dobândă credit (ex: 0.03 = 3%).")]
    [Range(0f, 0.2f)]
    public float creditInterestRate = 0.05f;

    /// <summary>Returnează datele produsului pentru un tip specific, null dacă nu e disponibil.</summary>
    public SupplierProduct GetProduct(ProductType type)
    {
        foreach (var p in products)
            if (p.productType == type) return p;
        return null;
    }
}

[System.Serializable]
public class SupplierProduct
{
    public ProductType productType;
    [Tooltip("Prețul de bază per unitate (fără modificatori de relație).")]
    public float basePricePerUnit = 1f;
    [Tooltip("Stoc zilnic maxim specific acestui produs (0 = folosește maxStockPerDay din SO).")]
    public int stockOverride = 0;
}
