using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Orchestrator for the 3-step supplier order wizard.
/// Owns the wizard state and routes navigation; no layout or business logic.
/// </summary>
public class SupplierPanelUI : MonoBehaviour
{
    // ── Serialized ────────────────────────────────────────────────────────────
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset supplierProductRowTemplate;
    [SerializeField] private VisualTreeAsset supplierCardTemplate;
    [SerializeField] private ProductDataSO productDatabase;

    // ── Wizard state ──────────────────────────────────────────────────────────
    private int _step = 0;
    private ProductType _selectedProduct = ProductType.None;
    private FurnizoriSO _selectedSupplier = null;
    private int _selectedQuantity = 10;
    private PaymentType _selectedPayment = PaymentType.Immediate;

    // ── UI references ─────────────────────────────────────────────────────────
    private VisualElement _supplierPopup;
    private VisualElement _stepProduct;
    private VisualElement _stepSupplier;
    private VisualElement _stepConfirm;
    private Button _backBtn;

    // ── Sub-panels ────────────────────────────────────────────────────────────
    private SupplierStepIndicator _stepIndicator;
    private SupplierStepProduct _productStep;
    private SupplierStepSupplier _supplierStep;
    private SupplierStepConfirm _confirmStep;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        CacheSharedElements(root);
        BuildSubPanels(root);
        RegisterSharedCallbacks(root);
    }

    // ── Public API ────────────────────────────────────────────────────────────

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

    // ── Navigation ────────────────────────────────────────────────────────────

    private void GoToStep(int step)
    {
        _step = Mathf.Clamp(step, 0, 2);

        _stepProduct?.SetDisplay(_step == 0);
        _stepSupplier?.SetDisplay(_step == 1);
        _stepConfirm?.SetDisplay(_step == 2);
        _backBtn?.SetDisplay(_step > 0);

        _stepIndicator?.Refresh(_step);

        switch (_step)
        {
            case 0: _productStep.Populate(); break;
            case 1: _supplierStep.Populate(_selectedProduct); break;
            case 2: _confirmStep.Refresh(_selectedProduct, _selectedSupplier, _selectedQuantity, _selectedPayment); break;
        }
    }

    // ── Init helpers ──────────────────────────────────────────────────────────

    private void CacheSharedElements(VisualElement root)
    {
        _supplierPopup = root.Q<VisualElement>("SupplierPopup");
        _stepProduct = root.Q<VisualElement>("SupplierStepProduct");
        _stepSupplier = root.Q<VisualElement>("SupplierStepSupplier");
        _stepConfirm = root.Q<VisualElement>("SupplierStepConfirm");
        _backBtn = root.Q<Button>("SupplierBackBtn");
    }

    private void BuildSubPanels(VisualElement root)
    {
        _stepIndicator = new SupplierStepIndicator(
            root.Q<VisualElement>("StepIndicator1"),
            root.Q<VisualElement>("StepIndicator2"),
            root.Q<VisualElement>("StepIndicator3")
        );

        _productStep = new SupplierStepProduct(
            root, supplierProductRowTemplate, productDatabase,
            onProductSelected: type =>
            {
                _selectedProduct = type;
                GoToStep(1);
            }
        );

        _supplierStep = new SupplierStepSupplier(
            root, supplierCardTemplate,
            onSupplierSelected: supplier =>
            {
                _selectedSupplier = supplier;
                _selectedPayment = PaymentType.Immediate;
                GoToStep(2);
            },
            onDebtPaid: () => _supplierStep.Populate(_selectedProduct)
        );

        _confirmStep = new SupplierStepConfirm(
            root,
            onQuantityChanged: qty =>
            {
                _selectedQuantity = qty;
                _confirmStep.Refresh(_selectedProduct, _selectedSupplier, _selectedQuantity, _selectedPayment);
            },
            onPaymentSelected: payment =>
            {
                _selectedPayment = payment;
                _confirmStep.Refresh(_selectedProduct, _selectedSupplier, _selectedQuantity, _selectedPayment);
            },
            onOrderPlaced: () =>
            {
                _selectedProduct = ProductType.None;
                _selectedSupplier = null;
                Close();
            }
        );
    }

    private void RegisterSharedCallbacks(VisualElement root)
    {
        root.Q<Button>("SupplierOrderBtn")?.RegisterCallback<ClickEvent>(_ => Open());
        root.Q<Button>("SupplierCloseBtn")?.RegisterCallback<ClickEvent>(_ => Close());
        _backBtn?.RegisterCallback<ClickEvent>(_ => GoToStep(_step - 1));
    }
}