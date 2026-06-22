using UnityEngine.UIElements;

/// <summary>
/// Helper static pentru afișarea TVA-ului în UI.
/// Folosit de InventoryDetailsPanel și SupplierPanelUI.
/// </summary>
public static class TaxUIHelper
{
    // ─── Inventar — panoul de detalii produs ──────────────────────────────────

    /// <summary>
    /// Cache-uiește elementele de TVA din DetailsPanel.
    /// Apelat o singură dată la Initialize.
    /// </summary>
    public static void CacheTaxElements(
        UnityEngine.UIElements.VisualElement root,
        out Label rateValue,
        out Label taxAmount,
        out Label netRevenue)
    {
        rateValue = root.Q<Label>("SalesTaxRateValue");
        taxAmount = root.Q<Label>("SalesTaxAmountLabel");
        netRevenue = root.Q<Label>("NetRevenueLabel");

        // Afișăm cota curentă o singură dată — nu se schimbă la runtime
        if (TaxManager.Instance != null && rateValue != null)
            rateValue.text = TaxManager.Instance.GetSalesTaxDisplay();
    }

    /// <summary>
    /// Actualizează afișajul TVA ori de câte ori prețul de vânzare se schimbă.
    /// </summary>
    public static void UpdateSalesTax(int salePrice, Label taxAmount, Label netRevenue)
    {
        if (TaxManager.Instance == null) return;

        int tax = TaxManager.Instance.GetSalesTaxAmount(salePrice);    // TVA extras din preț
        int net = salePrice - tax;                                       // ce rămâne jucătorului

        if (taxAmount != null) taxAmount.text = $"{tax} RON";
        if (netRevenue != null) netRevenue.text = $"{net} RON";
    }

    // ─── Furnizori — pasul de confirmare comandă ──────────────────────────────

    /// <summary>
    /// Cache-uiește elementele de TVA din SupplierStepConfirm.
    /// </summary>
    public static void CacheSupplierTaxElements(
        VisualElement root,
        out Label purchaseTax,
        out Label priceWithTax,
        out Label taxTotal)
    {
        purchaseTax = root.Q<Label>("ConfirmPurchaseTax");
        priceWithTax = root.Q<Label>("ConfirmPriceWithTax");
        taxTotal = root.Q<Label>("ConfirmTaxTotal");
    }

    /// <summary>
    /// Actualizează TVA-ul la achiziție când se schimbă prețul/furnizorul/cantitatea.
    /// </summary>
    /// <param name="pricePerUnit">Prețul de bază per unitate (fără TVA).</param>
    /// <param name="quantity">Cantitatea selectată.</param>
    public static void UpdatePurchaseTax(
        int pricePerUnit, int quantity,
        Label purchaseTax, Label priceWithTax, Label taxTotal)
    {
        if (TaxManager.Instance == null) return;

        int taxPerUnit = TaxManager.Instance.GetPurchaseTaxAmount(pricePerUnit);
        int finalPerUnit = TaxManager.Instance.ApplyPurchaseTax(pricePerUnit);
        int totalTax = taxPerUnit * quantity;

        if (purchaseTax != null)
            purchaseTax.text = $"+{taxPerUnit} RON/buc ({TaxManager.Instance.GetPurchaseTaxDisplay()})";

        if (priceWithTax != null)
            priceWithTax.text = $"{finalPerUnit} RON/buc";

        if (taxTotal != null)
            taxTotal.text = totalTax > 0 ? $"TVA achiziție: {totalTax} RON" : "";
    }
}