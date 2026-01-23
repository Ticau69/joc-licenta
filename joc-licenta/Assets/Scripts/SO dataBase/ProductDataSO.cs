using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "ProductDataSO", menuName = "Scriptable Objects/ProductDataSO")]
public class ProductDataSO : ScriptableObject
{
    public List<ProductData> allProducts;
}
[Serializable]
public class ProductData
{
    [Header("Identificare")]
    public string productName;       // Ex: "Pâine Rustică"
    public ProductType type;         // Enum-ul existent (cheia pentru dicționar)
    public Sprite icon;              // Iconița pentru UI

    [Header("Economie")]
    public float baseCost;           // Preț achiziție (Inflație)
    public float defaultSellingPrice; // Preț vânzare recomandat
}
