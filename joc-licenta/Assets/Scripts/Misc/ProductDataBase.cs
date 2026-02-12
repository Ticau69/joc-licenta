using System.Collections.Generic;
using UnityEngine;

public class ProductDataBase : MonoBehaviour
{
    public static ProductDataBase Instance { get; private set; }

    [Header("Assign your ProductDataSO here")]
    [SerializeField] private ProductDataSO productDataSO;

    private Dictionary<ProductType, ProductData> _map;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildCache();
    }

    public void RebuildCache()
    {
        _map = new Dictionary<ProductType, ProductData>();

        if (productDataSO == null || productDataSO.allProducts == null)
        {
            Debug.LogWarning("[ProductDataBase] ProductDataSO is not assigned or has no products.");
            return;
        }

        foreach (var p in productDataSO.allProducts)
        {
            if (p == null) continue;
            if (p.type == ProductType.None) continue;

            if (_map.ContainsKey(p.type))
            {
                Debug.LogWarning($"[ProductDataBase] Duplicate ProductType '{p.type}' in ProductDataSO. Keeping first.");
                continue;
            }

            _map.Add(p.type, p);
        }
    }

    public bool TryGetProduct(ProductType type, out ProductData data)
    {
        data = null;

        if (_map == null)
            RebuildCache();

        return _map != null && _map.TryGetValue(type, out data) && data != null;
    }

    public bool TryGetSellPrice(ProductType type, out float price)
    {
        price = 0f;

        if (!TryGetProduct(type, out var p))
            return false;

        price = p.baseSellPrice;
        return price > 0f;
    }

    public List<ProductType> GetAllProductTypes()
    {
        if (_map == null) RebuildCache();
        return _map != null ? new List<ProductType>(_map.Keys) : new List<ProductType>();
    }

    public float GetSellPriceOrDefault(ProductType type, float fallback = 10f)
    {
        return TryGetSellPrice(type, out var price) ? price : fallback;
    }

    public bool TryGetIcon(ProductType type, out Sprite icon)
    {
        icon = null;

        if (!TryGetProduct(type, out var p))
            return false;

        icon = p.icon;
        return icon != null;
    }

    public bool TryGetName(ProductType type, out string name)
    {
        name = null;

        if (!TryGetProduct(type, out var p))
            return false;

        name = p.productName;
        return !string.IsNullOrWhiteSpace(name);
    }
}
