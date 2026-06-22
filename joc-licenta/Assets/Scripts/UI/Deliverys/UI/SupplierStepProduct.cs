using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Step 1 of the supplier wizard — shows available products with best price.
/// Plain C# class — no Unity dependencies beyond UIElements.
/// </summary>
public class SupplierStepProduct
{
    private readonly ScrollView _productList;
    private readonly VisualTreeAsset _rowTemplate;
    private readonly ProductDataSO _productDB;
    private readonly Action<ProductType> _onProductSelected;

    public SupplierStepProduct(
        VisualElement root,
        VisualTreeAsset rowTemplate,
        ProductDataSO productDB,
        Action<ProductType> onProductSelected)
    {
        _productList = root.Q<ScrollView>("SupplierProductList");
        _rowTemplate = rowTemplate;
        _productDB = productDB;
        _onProductSelected = onProductSelected;
    }

    public void Populate()
    {
        if (_productList == null || _rowTemplate == null) return;
        _productList.Clear();

        if (_productDB == null)
        {
            Debug.LogWarning("[SupplierStepProduct] productDatabase nu e asignat!");
            return;
        }

        var relMgr = SupplierRelationshipManager.Instance;
        if (relMgr == null) return;

        var bestPrices = BuildBestPriceIndex(relMgr);

        bool anyAvailable = false;

        foreach (var productData in _productDB.allProducts)
        {
            if (productData == null || productData.type == ProductType.None) continue;
            if (!bestPrices.TryGetValue(productData.type, out float bestPrice)) continue;

            anyAvailable = true;

            var row = _rowTemplate.Instantiate();
            var nameLbl = row.Q<Label>("ProductNameLabel");
            var priceLbl = row.Q<Label>("BestPriceLabel");
            var btn = row.Q<Button>("SelectProductBtn");

            if (nameLbl != null) nameLbl.text = productData.productName;
            if (priceLbl != null) priceLbl.text = $"de la {bestPrice:F2} RON/buc";

            if (btn != null)
            {
                ProductType captured = productData.type;
                btn.RegisterCallback<ClickEvent>(_ => _onProductSelected?.Invoke(captured));
            }

            _productList.Add(row);
        }

        if (!anyAvailable)
        {
            var empty = new Label("Niciun produs disponibil. Configureaza furnizori in SupplierRelationshipManager.")
            {
                style =
                {
                    color      = new StyleColor(new Color(0.6f, 0.6f, 0.6f)),
                    marginTop  = 20,
                    whiteSpace = WhiteSpace.Normal,
                }
            };
            _productList.Add(empty);
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>Builds a ProductType → lowest-available-price index across all suppliers.</summary>
    private static Dictionary<ProductType, float> BuildBestPriceIndex(SupplierRelationshipManager relMgr)
    {
        var bestPrices = new Dictionary<ProductType, float>();

        foreach (var supplier in relMgr.allSuppliers)
        {
            foreach (var p in supplier.products)
            {
                float price = relMgr.GetFinalPrice(supplier, p.productType);

                if (!bestPrices.TryGetValue(p.productType, out float current) || price < current)
                    bestPrices[p.productType] = price;
            }
        }

        return bestPrices;
    }
}