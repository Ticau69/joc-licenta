using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SupplierPanelUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset supplierProductRowTemplate;
    [SerializeField] private VisualTreeAsset supplierCardTemplate;
    [SerializeField] private ProductDataSO productDatabase;

    // ── Stare ─────────────────────────────────────────────────────────────────
    private int _step = 0;
    private ProductType _selectedProduct = ProductType.None;
    private FurnizoriSO _selectedSupplier = null;
    private int _selectedQuantity = 10;
    private PaymentType _selectedPayment = PaymentType.Immediate;

    // ── UI References ─────────────────────────────────────────────────────────
    private VisualElement _supplierPopup;
    private VisualElement _stepProduct;
    private VisualElement _stepSupplier;
    private VisualElement _stepConfirm;

    // Sidebar step indicators
    private VisualElement _stepInd1;
    private VisualElement _stepInd2;
    private VisualElement _stepInd3;

    private Button _backBtn;
    private Button _closeBtn;

    // Step 1
    private ScrollView _productList;

    // Step 2
    private Label _supplierStepTitle;
    private ScrollView _supplierCardList;

    // Step 3
    private Label _confirmProduct;
    private Label _confirmSupplier;
    private Label _confirmRelation;
    private Label _confirmDiscount;
    private Label _confirmDelivery;
    private Label _confirmPrice;
    private SliderInt _quantitySlider;
    private Label _quantityLabel;
    private Label _confirmTotal;
    private Button _payImmBtn;
    private Button _payDelBtn;
    private Button _payCreditBtn;
    private Label _errorLabel;
    private Button _confirmOrderBtn;

    // ─────────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        _supplierPopup = root.Q<VisualElement>("SupplierPopup");
        _stepProduct = root.Q<VisualElement>("SupplierStepProduct");
        _stepSupplier = root.Q<VisualElement>("SupplierStepSupplier");
        _stepConfirm = root.Q<VisualElement>("SupplierStepConfirm");

        _stepInd1 = root.Q<VisualElement>("StepIndicator1");
        _stepInd2 = root.Q<VisualElement>("StepIndicator2");
        _stepInd3 = root.Q<VisualElement>("StepIndicator3");

        _backBtn = root.Q<Button>("SupplierBackBtn");
        _closeBtn = root.Q<Button>("SupplierCloseBtn");

        _productList = root.Q<ScrollView>("SupplierProductList");

        _supplierStepTitle = root.Q<Label>("SupplierStepTitle");
        _supplierCardList = root.Q<ScrollView>("SupplierCardList");

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

        root.Q<Button>("SupplierOrderBtn")?.RegisterCallback<ClickEvent>(_ => Open());
        _closeBtn?.RegisterCallback<ClickEvent>(_ => Close());
        _backBtn?.RegisterCallback<ClickEvent>(_ => GoToStep(_step - 1));

        if (_quantitySlider != null)
        {
            _quantitySlider.lowValue = 1;
            _quantitySlider.highValue = 100;
            _quantitySlider.RegisterValueChangedCallback(evt =>
            {
                _selectedQuantity = evt.newValue;
                RefreshConfirmStep();
            });
        }

        _payImmBtn?.RegisterCallback<ClickEvent>(_ => SelectPayment(PaymentType.Immediate));
        _payDelBtn?.RegisterCallback<ClickEvent>(_ => SelectPayment(PaymentType.OnDelivery));
        _payCreditBtn?.RegisterCallback<ClickEvent>(_ => SelectPayment(PaymentType.Credit));
        _confirmOrderBtn?.RegisterCallback<ClickEvent>(_ => PlaceOrder());
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public void Open()
    {
        if (_supplierPopup == null) return;
        _supplierPopup.style.display = DisplayStyle.Flex;
        GoToStep(0);
    }

    public void Close()
    {
        if (_supplierPopup == null) return;
        _supplierPopup.style.display = DisplayStyle.None;
    }

    // ── Navigatie ─────────────────────────────────────────────────────────────

    private void GoToStep(int step)
    {
        _step = Mathf.Clamp(step, 0, 2);

        _stepProduct?.SetDisplay(_step == 0);
        _stepSupplier?.SetDisplay(_step == 1);
        _stepConfirm?.SetDisplay(_step == 2);
        _backBtn?.SetDisplay(_step > 0);

        UpdateStepIndicators();

        switch (_step)
        {
            case 0: PopulateProductList(); break;
            case 1: PopulateSupplierList(); break;
            case 2: RefreshConfirmStep(); break;
        }
    }

    // ── Sidebar step indicators ───────────────────────────────────────────────

    private void UpdateStepIndicators()
    {
        UpdateIndicator(_stepInd1, 0);
        UpdateIndicator(_stepInd2, 1);
        UpdateIndicator(_stepInd3, 2);
    }

    private void UpdateIndicator(VisualElement indicator, int stepIndex)
    {
        if (indicator == null) return;

        // Primul child e cercul, al doilea e label-ul textului
        var circle = indicator.ElementAt(0);
        var label = indicator.ElementAt(1);

        bool isActive = _step == stepIndex;
        bool isComplete = _step > stepIndex;

        if (circle != null)
        {
            if (isComplete)
                circle.style.backgroundColor = new StyleColor(new Color(0.3f, 0.7f, 0.3f));
            else if (isActive)
                circle.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 1f));
            else
                circle.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        }

        if (label != null)
        {
            label.style.color = isActive || isComplete
                ? new StyleColor(Color.white)
                : new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        }
    }

    // ── Step 1: Produse disponibile ───────────────────────────────────────────

    private void PopulateProductList()
    {
        if (_productList == null || supplierProductRowTemplate == null) return;
        _productList.Clear();

        if (productDatabase == null)
        {
            Debug.LogWarning("[SupplierPanelUI] productDatabase nu e asignat!");
            return;
        }

        var relMgr = SupplierRelationshipManager.Instance;
        if (relMgr == null) return;

        // ── Construim un index rapid: ProductType → cel mai mic preț din furnizori ──
        var bestPrices = new Dictionary<ProductType, float>();
        foreach (var supplier in relMgr.allSuppliers)
            foreach (var p in supplier.products)
            {
                float price = relMgr.GetFinalPrice(supplier, p.productType);
                if (!bestPrices.ContainsKey(p.productType) || price < bestPrices[p.productType])
                    bestPrices[p.productType] = price;
            }

        // ── Iterăm SO-ul — ordinea din SO e ordinea din UI ────────────────────────
        bool anyAvailable = false;

        foreach (var productData in productDatabase.allProducts)
        {
            if (productData == null || productData.type == ProductType.None) continue;

            bool avail = bestPrices.ContainsKey(productData.type);

            // Dacă nu are furnizori — sărim peste, nu afișăm "Indisponibil"
            if (!avail) continue;

            anyAvailable = true;

            var row = supplierProductRowTemplate.Instantiate();
            var nameLbl = row.Q<Label>("ProductNameLabel");
            var priceLbl = row.Q<Label>("BestPriceLabel");
            var btn = row.Q<Button>("SelectProductBtn");

            // Folosim numele din SO, nu ToString() pe enum
            if (nameLbl != null) nameLbl.text = productData.productName;
            if (priceLbl != null) priceLbl.text = $"de la {bestPrices[productData.type]:F2} RON/buc";
            if (btn != null)
            {
                ProductType captured = productData.type;
                btn.RegisterCallback<ClickEvent>(_ =>
                {
                    _selectedProduct = captured;
                    GoToStep(1);
                });
            }

            _productList.Add(row);
        }

        if (!anyAvailable)
        {
            var empty = new Label("Niciun produs disponibil. Configureaza furnizori in SupplierRelationshipManager.");
            empty.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            empty.style.marginTop = 20;
            empty.style.whiteSpace = WhiteSpace.Normal;
            _productList.Add(empty);
        }
    }

    // ── Step 2: Furnizori ─────────────────────────────────────────────────────

    private void PopulateSupplierList()
    {
        if (_supplierCardList == null || supplierCardTemplate == null) return;

        if (_supplierStepTitle != null)
            _supplierStepTitle.text = $"Furnizori pentru {_selectedProduct}";

        _supplierCardList.Clear();

        var relMgr = SupplierRelationshipManager.Instance;
        if (relMgr == null) return;

        var suppliers = relMgr.allSuppliers
            .Where(s => s != null && s.GetProduct(_selectedProduct) != null)
            .OrderBy(s => relMgr.GetFinalPrice(s, _selectedProduct))
            .ToList();

        foreach (var supplier in suppliers)
        {
            var card = supplierCardTemplate.Instantiate();
            FillSupplierCard(card, supplier, relMgr);
            _supplierCardList.Add(card);
        }
    }

    private void FillSupplierCard(VisualElement card, FurnizoriSO supplier,
                                   SupplierRelationshipManager relMgr)
    {
        bool canOrder = relMgr.CanOrder(supplier);
        string statusTxt = relMgr.GetStatusText(supplier);
        Color statusCol = relMgr.GetStatusColor(supplier);
        float price = relMgr.GetFinalPrice(supplier, _selectedProduct);
        int relation = relMgr.GetRelationship(supplier);
        int debt = relMgr.GetPendingDebt(supplier);
        var status = relMgr.GetStatus(supplier);

        // Border color pe card bazat pe status relatie
        var cardRoot = card.Q<VisualElement>("SupplierCard");
        if (cardRoot != null)
            cardRoot.style.borderLeftColor = new StyleColor(statusCol);

        var nameLbl = card.Q<Label>("SupplierNameLabel");
        if (nameLbl != null)
        {
            nameLbl.text = supplier.supplierName;
            nameLbl.style.color = canOrder
                ? new StyleColor(Color.white)
                : new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        }

        var statusLbl = card.Q<Label>("RelationshipStatusLabel");
        if (statusLbl != null)
        {
            statusLbl.text = statusTxt;
            statusLbl.style.color = new StyleColor(statusCol);
            statusLbl.style.backgroundColor = new StyleColor(new Color(statusCol.r, statusCol.g, statusCol.b, 0.1f));
        }

        var barFill = card.Q<VisualElement>("RelationBarFill");
        if (barFill != null)
        {
            barFill.style.width = Length.Percent(relation);
            barFill.style.backgroundColor = new StyleColor(statusCol);
        }

        var priceLbl = card.Q<Label>("PriceLabel");
        if (priceLbl != null)
        {
            priceLbl.text = $"{price:F2} RON/buc";
            priceLbl.style.color = new StyleColor(new Color(1f, 0.87f, 0.2f));
            priceLbl.style.backgroundColor = new StyleColor(new Color(1f, 0.87f, 0.2f, 0.1f));
        }

        var delivLbl = card.Q<Label>("DeliveryLabel");
        if (delivLbl != null)
            delivLbl.text = supplier.deliveryDays == 0 ? "Instant" : $"{supplier.deliveryDays} zi";

        var modLbl = card.Q<Label>("ModifierLabel");
        if (modLbl != null)
        {
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
                        PopulateSupplierList();
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
                selectBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    _selectedSupplier = supplier;
                    _selectedPayment = PaymentType.Immediate;
                    GoToStep(2);
                });
            }
        }
    }

    // ── Step 3: Confirmare ────────────────────────────────────────────────────

    private void RefreshConfirmStep()
    {
        if (_selectedSupplier == null) return;

        var relMgr = SupplierRelationshipManager.Instance;
        float priceUnit = relMgr.GetFinalPrice(_selectedSupplier, _selectedProduct);
        int total = Mathf.RoundToInt(priceUnit * _selectedQuantity);
        var status = relMgr.GetStatus(_selectedSupplier);

        if (_confirmProduct != null) _confirmProduct.text = _selectedProduct.ToString();
        if (_confirmSupplier != null) _confirmSupplier.text = _selectedSupplier.supplierName;
        if (_confirmDelivery != null) _confirmDelivery.text = _selectedSupplier.deliveryDays == 0 ? "Instant" : $"Peste {_selectedSupplier.deliveryDays} zile";
        if (_confirmPrice != null) _confirmPrice.text = $"{priceUnit:F2} RON/buc";

        if (_quantitySlider != null) _quantitySlider.SetValueWithoutNotify(_selectedQuantity);
        if (_quantityLabel != null) _quantityLabel.text = $"{_selectedQuantity} buc";

        if (_confirmRelation != null)
        {
            _confirmRelation.text = relMgr.GetStatusText(_selectedSupplier);
            _confirmRelation.style.color = new StyleColor(relMgr.GetStatusColor(_selectedSupplier));
        }

        if (_confirmDiscount != null)
        {
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

        if (_confirmTotal != null)
        {
            if (_selectedPayment == PaymentType.Credit)
            {
                int creditTotal = Mathf.RoundToInt(total * (1f + _selectedSupplier.creditInterestRate));
                _confirmTotal.text = $"{creditTotal} RON";
            }
            else
            {
                _confirmTotal.text = $"{total} RON";
            }
        }

        _payCreditBtn?.SetEnabled(_selectedSupplier.allowsCredit &&
                                   status == RelationshipStatus.Friendly);
        _payDelBtn?.SetEnabled(_selectedSupplier.allowsPayOnDelivery);

        HighlightPaymentBtn();

        if (_errorLabel != null)
            _errorLabel.style.display = DisplayStyle.None;
    }

    private void SelectPayment(PaymentType type)
    {
        _selectedPayment = type;
        HighlightPaymentBtn();
        RefreshConfirmStep();
    }

    private void HighlightPaymentBtn()
    {
        var active = new Color(0.12f, 0.50f, 0.12f);
        var inactive = new Color(0.20f, 0.20f, 0.20f);

        if (_payImmBtn != null) _payImmBtn.style.backgroundColor = _selectedPayment == PaymentType.Immediate ? new StyleColor(active) : new StyleColor(inactive);
        if (_payDelBtn != null) _payDelBtn.style.backgroundColor = _selectedPayment == PaymentType.OnDelivery ? new StyleColor(active) : new StyleColor(inactive);
        if (_payCreditBtn != null) _payCreditBtn.style.backgroundColor = _selectedPayment == PaymentType.Credit ? new StyleColor(active) : new StyleColor(inactive);
    }

    private void PlaceOrder()
    {
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;

        bool ok = SupplierOrderSystem.Instance.TryPlaceOrder(
            _selectedSupplier, _selectedProduct,
            _selectedQuantity, _selectedPayment,
            out string err);

        if (ok)
        {
            _selectedProduct = ProductType.None;
            _selectedSupplier = null;
            Close();
        }
        else if (_errorLabel != null)
        {
            _errorLabel.text = err;
            _errorLabel.style.display = DisplayStyle.Flex;
        }
    }
}

public static class SupplierUIExtensions
{
    public static void SetDisplay(this VisualElement el, bool visible)
    {
        if (el != null)
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}