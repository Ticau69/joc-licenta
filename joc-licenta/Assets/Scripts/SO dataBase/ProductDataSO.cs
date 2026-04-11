using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "ProductDataSO", menuName = "Scriptable Objects/ProductDataSO")]
public class ProductDataSO : ScriptableObject
{
    [Tooltip("Lista tuturor produselor din joc.")]
    public List<ProductData> allProducts = new List<ProductData>();

    // ── Lookup cache (opțional, populat la runtime) ───────────────────────────

    private Dictionary<ProductType, ProductData> _cache;

    /// <summary>
    /// Caută rapid un produs după tip. Cache-ul se construiește la primul apel.
    /// </summary>
    public ProductData GetByType(ProductType type)
    {
        if (_cache == null) BuildCache();
        _cache.TryGetValue(type, out ProductData data);
        return data;
    }

    /// <summary>
    /// Returnează toate produsele compatibile cu un anumit tip de raft.
    /// Apelat de WorkStation.GetAllowedProductData() — fără switch hardcodat.
    /// </summary>
    public List<ProductData> GetProductsForShelf(ShelfType shelfType)
    {
        List<ProductData> result = new List<ProductData>();
        foreach (ProductData data in allProducts)
        {
            if (data.compatibleShelfTypes.Contains(shelfType))
                result.Add(data);
        }
        return result;
    }

    // Reconstruiește cache-ul dacă lista e modificată în Editor
    private void OnValidate() => _cache = null;

    private void BuildCache()
    {
        _cache = new Dictionary<ProductType, ProductData>(allProducts.Count);
        foreach (ProductData data in allProducts)
        {
            if (!_cache.ContainsKey(data.type))
                _cache[data.type] = data;
            else
                Debug.LogWarning($"[ProductDataSO] Tip duplicat: {data.type}");
        }
    }
}

[Serializable]
public class ProductData
{
    [Header("Identificare")]
    public string productName;   // Ex: "Pâine Rustică"
    public ProductType type;          // Cheia pentru dicționar / lookup

    [Tooltip("Pe ce tipuri de rafturi poate fi pus acest produs. " +
             "Înlocuiește switch-ul hardcodat din WorkStation.")]
    public List<ShelfType> compatibleShelfTypes = new List<ShelfType>();

    [Header("Vizual")]
    public Sprite icon;           // Iconița pentru UI
    public GameObject displayPrefab;  // Prefab-ul 3D instanțiat la displayPoint pe raft

    [Header("Economie")]
    public float baseCost;            // Preț achiziție
    public float baseSellPrice;       // Preț vânzare
}