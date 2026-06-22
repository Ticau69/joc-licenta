using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Step 3 of the supplier wizard — order confirmation, quantity slider, payment selection.
/// Plain C# class — no Unity dependencies beyond UIElements.
/// </summary>
public class SupplierStepConfirm
{
    // ── Colors ────────────────────────────────────────────────────────────────
    private static readonly Color ColorPayActive = new(0.12f, 0.50f, 0.12f);
    private static readonly Color ColorPayInactive = new(0.20f, 0.20f, 0.20f);

    // ── UI references ─────────────────────────────────────────────────────────
    private readonly Label _confirmProduct;
    private readonly Label _confirmSupplier;
    private readonly Label _confirmRelation;
    private readonly Label _confirmDiscount;
    private readonly Label _confirmDelivery;
    private readonly Label _confirmPrice;
    private readonly SliderInt _quantitySlider;
    private readonly Label _quantityLabel;
    private readonly Label _confirmTotal;
    private readonly Button _payImmBtn;
    private readonly Button _payDelBtn;
    private readonly Button _payCreditBtn;
    private readonly Label _errorLabel;
    private readonly Button _confirmOrderBtn;

    // ── TVA UI References (NOU) ───────────────────────────────────────────────
    private readonly Label _confirmPurchaseTax;
    private readonly Label _confirmPriceWithTax;
    private readonly Label _confirmTaxTotal;

    // ── Callbacks ─────────────────────────────────────────────────────────────
    private readonly Action<int> _onQuantityChanged;
    private readonly Action<PaymentType> _onPaymentSelected;
    private readonly Action _onOrderPlaced;

    // ── Live state (needed for PlaceOrder) ────────────────────────────────────
    private ProductType _product;
    private FurnizoriSO _supplier;
    private int _quantity;
    private PaymentType _payment;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SupplierStepConfirm(
        VisualElement root,
        Action<int> onQuantityChanged,
        Action<PaymentType> onPaymentSelected,
        Action onOrderPlaced)
    {
        _onQuantityChanged = onQuantityChanged;
        _onPaymentSelected = onPaymentSelected;
        _onOrderPlaced = onOrderPlaced;

        _confirmProduct = root.Q<Label>("ConfirmProduct");
        _confirmSupplier = root.Q<Label>("ConfirmSupplier");
        _confirmRelation = root.Q<Label>("ConfirmRelation");
        _confirmDiscount = root.Q<Label>("ConfirmDiscount");
        _confirmDelivery = root.Q<Label>("ConfirmDelivery");
        _confirmPrice = root.Q<Label>("ConfirmPrice");
        _quantitySlider = root.Q<SliderInt>("ConfirmQuantitySlider");
        _quantityLabel = root.Q<Label>("ConfirmQuantityLabel");
        _confirmTotal = root.Q<Label>("ConfirmTotal");
        _payImmBtn = root.Q<Button>("PayImmediateBtn");
        _payDelBtn = root.Q<Button>("PayOnDeliveryBtn");
        _payCreditBtn = root.Q<Button>("PayCreditBtn");
        _errorLabel = root.Q<Label>("SupplierErrorLabel");
        _confirmOrderBtn = root.Q<Button>("ConfirmSupplierOrderBtn");

        // NOU: Căutăm etichetele de TVA folosind Helper-ul tău
        TaxUIHelper.CacheSupplierTaxElements(root, out _confirmPurchaseTax, out _confirmPriceWithTax, out _confirmTaxTotal);

        RegisterCallbacks();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Refresh(
        ProductType product,
        FurnizoriSO supplier,
        int quantity,
        PaymentType payment)
    {
        if (supplier == null) return;

        _product = product;
        _supplier = supplier;
        _quantity = quantity;
        _payment = payment;

        var relMgr = SupplierRelationshipManager.Instance;
        float priceUnitFloat = relMgr.GetFinalPrice(supplier, product);
        int basePriceUnit = Mathf.RoundToInt(priceUnitFloat);

        // NOU: Actualizăm textele de TVA pe baza prețului net
        TaxUIHelper.UpdatePurchaseTax(basePriceUnit, quantity, _confirmPurchaseTax, _confirmPriceWithTax, _confirmTaxTotal);

        // NOU: Calculăm Totalul Final INCLUSIV TVA, pentru a fi scăzut corect din cont
        int finalPriceUnit = TaxManager.Instance != null ? TaxManager.Instance.ApplyPurchaseTax(basePriceUnit) : basePriceUnit;
        int total = finalPriceUnit * quantity;

        var status = relMgr.GetStatus(supplier);

        SetSummaryLabels(supplier, product, priceUnitFloat, quantity);
        SetRelationLabels(relMgr, supplier, status);
        SetTotalLabel(total, supplier, payment); // Transmitem totalul CU TVA mai departe

        _payCreditBtn?.SetEnabled(supplier.allowsCredit && status == RelationshipStatus.Friendly);
        _payDelBtn?.SetEnabled(supplier.allowsPayOnDelivery);

        HighlightPaymentButtons(payment);

        if (_errorLabel != null)
            _errorLabel.style.display = DisplayStyle.None;
    }

    // ── Private: UI setup ─────────────────────────────────────────────────────

    private void RegisterCallbacks()
    {
        if (_quantitySlider != null)
        {
            _quantitySlider.lowValue = 1;
            _quantitySlider.highValue = 100;
            _quantitySlider.RegisterValueChangedCallback(evt => _onQuantityChanged?.Invoke(evt.newValue));
        }

        _payImmBtn?.RegisterCallback<ClickEvent>(_ => _onPaymentSelected?.Invoke(PaymentType.Immediate));
        _payDelBtn?.RegisterCallback<ClickEvent>(_ => _onPaymentSelected?.Invoke(PaymentType.OnDelivery));
        _payCreditBtn?.RegisterCallback<ClickEvent>(_ => _onPaymentSelected?.Invoke(PaymentType.Credit));
        _confirmOrderBtn?.RegisterCallback<ClickEvent>(_ => PlaceOrder());
    }

    // ── Private: label helpers ────────────────────────────────────────────────

    private void SetSummaryLabels(FurnizoriSO supplier, ProductType product, float priceUnit, int quantity)
    {
        if (_confirmProduct != null) _confirmProduct.text = product.ToString();
        if (_confirmSupplier != null) _confirmSupplier.text = supplier.supplierName;
        if (_confirmDelivery != null) _confirmDelivery.text = supplier.deliveryDays == 0
            ? "Instant"
            : $"Peste {supplier.deliveryDays} zile";
        if (_confirmPrice != null) _confirmPrice.text = $"{priceUnit:F2} RON/buc";

        if (_quantitySlider != null) _quantitySlider.SetValueWithoutNotify(quantity);
        if (_quantityLabel != null) _quantityLabel.text = $"{quantity} buc";
    }

    private void SetRelationLabels(SupplierRelationshipManager relMgr, FurnizoriSO supplier, RelationshipStatus status)
    {
        if (_confirmRelation != null)
        {
            _confirmRelation.text = relMgr.GetStatusText(supplier);
            _confirmRelation.style.color = new StyleColor(relMgr.GetStatusColor(supplier));
        }

        if (_confirmDiscount == null) return;

        if (status == RelationshipStatus.Friendly)
        {
            _confirmDiscount.text = $"-{relMgr.friendlyDiscount * 100:F0}% discount aplicat";
            _confirmDiscount.style.color = new StyleColor(new Color(0.3f, 0.85f, 0.3f));
            _confirmDiscount.style.display = DisplayStyle.Flex;
        }
        else if (status == RelationshipStatus.Angry)
        {
            _confirmDiscount.text = $"+{relMgr.angryPenalty * 100:F0}% penalizare";
            _confirmDiscount.style.color = new StyleColor(new Color(0.9f, 0.3f, 0.3f));
            _confirmDiscount.style.display = DisplayStyle.Flex;
        }
        else
        {
            _confirmDiscount.style.display = DisplayStyle.None;
        }
    }

    private void SetTotalLabel(int baseTotal, FurnizoriSO supplier, PaymentType payment)
    {
        if (_confirmTotal == null) return;

        if (payment == PaymentType.Credit)
        {
            int creditTotal = Mathf.RoundToInt(baseTotal * (1f + supplier.creditInterestRate));
            _confirmTotal.text = $"{creditTotal} RON";
        }
        else
        {
            _confirmTotal.text = $"{baseTotal} RON";
        }
    }

    private void HighlightPaymentButtons(PaymentType selected)
    {
        if (_payImmBtn != null) _payImmBtn.style.backgroundColor = new StyleColor(selected == PaymentType.Immediate ? ColorPayActive : ColorPayInactive);
        if (_payDelBtn != null) _payDelBtn.style.backgroundColor = new StyleColor(selected == PaymentType.OnDelivery ? ColorPayActive : ColorPayInactive);
        if (_payCreditBtn != null) _payCreditBtn.style.backgroundColor = new StyleColor(selected == PaymentType.Credit ? ColorPayActive : ColorPayInactive);
    }

    // ── Place order ───────────────────────────────────────────────────────────

    private void PlaceOrder()
    {
        if (_errorLabel != null)
            _errorLabel.style.display = DisplayStyle.None;

        bool ok = SupplierOrderSystem.Instance.TryPlaceOrder(
            _supplier, _product, _quantity, _payment, out string err);

        if (ok)
        {
            _onOrderPlaced?.Invoke();
        }
        else if (_errorLabel != null)
        {
            _errorLabel.text = err;
            _errorLabel.style.display = DisplayStyle.Flex;
        }
    }
}