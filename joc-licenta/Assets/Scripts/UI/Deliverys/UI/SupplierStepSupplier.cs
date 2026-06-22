using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Step 2 of the supplier wizard — shows supplier cards for the selected product.
/// Plain C# class — no Unity dependencies beyond UIElements.
/// </summary>
public class SupplierStepSupplier
{
    private readonly ScrollView _cardList;
    private readonly Label _stepTitle;
    private readonly VisualTreeAsset _cardTemplate;
    private readonly Action<FurnizoriSO> _onSupplierSelected;
    private readonly Action _onDebtPaid;

    // Cached for use in FillSupplierCard without threading issues
    private ProductType _currentProduct;

    public SupplierStepSupplier(
        VisualElement root,
        VisualTreeAsset cardTemplate,
        Action<FurnizoriSO> onSupplierSelected,
        Action onDebtPaid)
    {
        _cardList = root.Q<ScrollView>("SupplierCardList");
        _stepTitle = root.Q<Label>("SupplierStepTitle");
        _cardTemplate = cardTemplate;
        _onSupplierSelected = onSupplierSelected;
        _onDebtPaid = onDebtPaid;
    }

    public void Populate(ProductType product)
    {
        _currentProduct = product;

        if (_cardList == null || _cardTemplate == null) return;

        if (_stepTitle != null)
            _stepTitle.text = $"Furnizori pentru {product}";

        _cardList.Clear();

        var relMgr = SupplierRelationshipManager.Instance;
        if (relMgr == null) return;

        var suppliers = relMgr.allSuppliers
            .Where(s => s != null && s.GetProduct(product) != null)
            .OrderBy(s => relMgr.GetFinalPrice(s, product))
            .ToList();

        foreach (var supplier in suppliers)
        {
            var card = _cardTemplate.Instantiate();
            FillSupplierCard(card, supplier, relMgr);
            _cardList.Add(card);
        }
    }

    // ── Card filling ──────────────────────────────────────────────────────────

    private void FillSupplierCard(
        VisualElement card,
        FurnizoriSO supplier,
        SupplierRelationshipManager relMgr)
    {
        bool canOrder = relMgr.CanOrder(supplier);
        string statusTxt = relMgr.GetStatusText(supplier);
        Color statusCol = relMgr.GetStatusColor(supplier);
        float price = relMgr.GetFinalPrice(supplier, _currentProduct);
        int relation = relMgr.GetRelationship(supplier);
        int debt = relMgr.GetPendingDebt(supplier);
        var status = relMgr.GetStatus(supplier);

        SetCardBorder(card, statusCol);
        SetNameLabel(card, supplier, canOrder);
        SetStatusLabel(card, statusTxt, statusCol);
        SetRelationBar(card, relation, statusCol);
        SetPriceLabel(card, price);
        SetDeliveryLabel(card, supplier);
        SetModifierLabel(card, status, relMgr);
        SetActionButtons(card, supplier, canOrder, debt);
    }

    private static void SetCardBorder(VisualElement card, Color statusCol)
    {
        var cardRoot = card.Q<VisualElement>("SupplierCard");
        if (cardRoot != null)
            cardRoot.style.borderLeftColor = new StyleColor(statusCol);
    }

    private static void SetNameLabel(VisualElement card, FurnizoriSO supplier, bool canOrder)
    {
        var nameLbl = card.Q<Label>("SupplierNameLabel");
        if (nameLbl == null) return;

        nameLbl.text = supplier.supplierName;
        nameLbl.style.color = new StyleColor(canOrder ? Color.white : new Color(0.5f, 0.5f, 0.5f));
    }

    private static void SetStatusLabel(VisualElement card, string statusTxt, Color statusCol)
    {
        var statusLbl = card.Q<Label>("RelationshipStatusLabel");
        if (statusLbl == null) return;

        statusLbl.text = statusTxt;
        statusLbl.style.color = new StyleColor(statusCol);
        statusLbl.style.backgroundColor = new StyleColor(new Color(statusCol.r, statusCol.g, statusCol.b, 0.1f));
    }

    private static void SetRelationBar(VisualElement card, int relation, Color statusCol)
    {
        var barFill = card.Q<VisualElement>("RelationBarFill");
        if (barFill == null) return;

        barFill.style.width = Length.Percent(relation);
        barFill.style.backgroundColor = new StyleColor(statusCol);
    }

    private static void SetPriceLabel(VisualElement card, float price)
    {
        var priceLbl = card.Q<Label>("PriceLabel");
        if (priceLbl == null) return;

        priceLbl.text = $"{price:F2} RON/buc";
        priceLbl.style.color = new StyleColor(new Color(1f, 0.87f, 0.2f));
        priceLbl.style.backgroundColor = new StyleColor(new Color(1f, 0.87f, 0.2f, 0.1f));
    }

    private static void SetDeliveryLabel(VisualElement card, FurnizoriSO supplier)
    {
        var delivLbl = card.Q<Label>("DeliveryLabel");
        if (delivLbl != null)
            delivLbl.text = supplier.deliveryDays == 0 ? "Instant" : $"{supplier.deliveryDays} zi";
    }

    private static void SetModifierLabel(
        VisualElement card,
        RelationshipStatus status,
        SupplierRelationshipManager relMgr)
    {
        var modLbl = card.Q<Label>("ModifierLabel");
        if (modLbl == null) return;

        if (status == RelationshipStatus.Friendly)
        {
            modLbl.text = $"-{relMgr.friendlyDiscount * 100:F0}% discount";
            modLbl.style.color = new StyleColor(new Color(0.3f, 0.85f, 0.3f));
            modLbl.style.backgroundColor = new StyleColor(new Color(0.3f, 0.85f, 0.3f, 0.1f));
            modLbl.style.display = DisplayStyle.Flex;
        }
        else if (status == RelationshipStatus.Angry)
        {
            modLbl.text = $"+{relMgr.angryPenalty * 100:F0}% penalizare";
            modLbl.style.color = new StyleColor(new Color(0.9f, 0.3f, 0.3f));
            modLbl.style.backgroundColor = new StyleColor(new Color(0.9f, 0.3f, 0.3f, 0.1f));
            modLbl.style.display = DisplayStyle.Flex;
        }
        else
        {
            modLbl.style.display = DisplayStyle.None;
        }
    }

    private void SetActionButtons(
        VisualElement card,
        FurnizoriSO supplier,
        bool canOrder,
        int debt)
    {
        var debtLbl = card.Q<Label>("DebtLabel");
        var payDebtBtn = card.Q<Button>("PayDebtBtn");
        var selectBtn = card.Q<Button>("SelectSupplierBtn");

        if (!canOrder && debt > 0)
        {
            if (debtLbl != null) { debtLbl.text = $"Datorie neachitata: {debt} RON"; debtLbl.style.display = DisplayStyle.Flex; }
            if (payDebtBtn != null)
            {
                payDebtBtn.style.display = DisplayStyle.Flex;
                payDebtBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    if (SupplierOrderSystem.Instance.TryPayDebt(supplier))
                        _onDebtPaid?.Invoke();
                });
            }
            if (selectBtn != null) selectBtn.style.display = DisplayStyle.None;
        }
        else
        {
            if (debtLbl != null) debtLbl.style.display = DisplayStyle.None;
            if (payDebtBtn != null) payDebtBtn.style.display = DisplayStyle.None;
            if (selectBtn != null)
            {
                selectBtn.style.display = DisplayStyle.Flex;
                selectBtn.RegisterCallback<ClickEvent>(_ => _onSupplierSelected?.Invoke(supplier));
            }
        }
    }
}