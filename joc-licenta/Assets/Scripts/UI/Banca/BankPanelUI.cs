using UnityEngine;
using UnityEngine.UIElements;

public class BankPanelUI : MonoBehaviour
{
    [Header("UI Documents")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIDocument mainUIDocument;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset bankCardTemplate;
    [SerializeField] private VisualTreeAsset activeLoanRowTemplate;

    [Header("References")]
    [SerializeField] public BankManager bankManager;
    [SerializeField] public InflationManager inflationManager;

    // Sub-controllers
    public BankSelectionController selectionController { get; private set; }
    public LoanConfigController configController { get; private set; }
    public LoanSummaryController summaryController { get; private set; }
    public ActiveLoansController loansController { get; private set; }

    private VisualElement _root;
    private VisualElement _creditRoot;
    private Button _creditBtn;
    private Button _exitBtn;
    private int _currentStep;

    public BankSO SelectedBank { get; set; }

    private void Awake()
    {
        if (mainUIDocument != null)
        {
            _creditBtn = mainUIDocument.rootVisualElement.Q<Button>("Credite");
            if (_creditBtn != null) _creditBtn.clicked += Open;
        }

        if (uiDocument != null)
        {
            _root = uiDocument.rootVisualElement;
            _creditRoot = _root.Q("CreditRoot");
            _exitBtn = _root.Q<Button>("ExitCreditBtn");
            if (_exitBtn != null) _exitBtn.clicked += Close;
        }

        // Inițializăm TOATE controllerele
        selectionController = new BankSelectionController(_root, this, bankCardTemplate);
        configController = new LoanConfigController(_root, this);
        summaryController = new LoanSummaryController(_root, this);
        loansController = new ActiveLoansController(_root, this, activeLoanRowTemplate);

        Close();
    }

    private void Start()
    {
        GoToStep(0);
        loansController.Refresh(); // Populăm creditele active la start
    }

    private void OnEnable()
    {
        if (bankManager != null)
        {
            bankManager.OnLoanTaken += OnLoanChanged;
            bankManager.OnLoanFullyPaid += OnLoanChanged;
            bankManager.OnPaymentMade += OnPaymentMade;
        }
    }

    private void OnDisable()
    {
        if (bankManager != null)
        {
            bankManager.OnLoanTaken -= OnLoanChanged;
            bankManager.OnLoanFullyPaid -= OnLoanChanged;
            bankManager.OnPaymentMade -= OnPaymentMade;
        }
    }

    public void Open() { if (_creditRoot != null) _creditRoot.style.display = DisplayStyle.Flex; GoToStep(0); }
    public void Close() { if (_creditRoot != null) _creditRoot.style.display = DisplayStyle.None; }

    public void GoToStep(int step)
    {
        _currentStep = step;

        selectionController.SetVisible(step == 0);
        configController.SetVisible(step == 1);
        summaryController.SetVisible(step == 2);

        SetIndicator("StepIndicator1", step >= 0);
        SetIndicator("StepIndicator2", step >= 1);
        SetIndicator("StepIndicator3", step >= 2);

        switch (step)
        {
            case 0: selectionController.Refresh(); break;
            case 1: configController.Setup(); break;
            case 2: summaryController.Populate(); break;
        }
    }

    private void SetIndicator(string name, bool active)
    {
        var ind = _root?.Q(name);
        if (ind == null) return;
        if (active) ind.AddToClassList("step-active");
        else ind.RemoveFromClassList("step-active");
    }

    private void OnLoanChanged(BankLoan loan)
    {
        loansController.Refresh();
    }

    private void OnPaymentMade(BankLoan loan, float amount)
    {
        loansController.Refresh();
    }
}