using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// Controller UI pentru panoul băncilor — wizard 3 pași:
/// Step 0 — Alege banca
/// Step 1 — Configurează creditul (sumă + termen)
/// Step 2 — Sumar + confirmare
/// + Secțiunea credite active (mereu vizibilă jos)
/// </summary>
public class OldBankUI : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;     // CreditUI.uxml
    [SerializeField] private UIDocument mainUIDocument; // TestUI.uxml — pentru CreditBtn

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset bankCardTemplate;
    [SerializeField] private VisualTreeAsset _activeLoanRowTemplate;

    [Header("References")]
    [SerializeField] private BankManager bankManager;
    [SerializeField] private InflationManager inflationManager;

    // =========================================================================
    // PRIVATE — UI Elements
    // =========================================================================

    private VisualElement _root;
    private VisualElement _creditRoot; // containerul principal din CreditUI
    private Button creditBtn;
    private Button exitBtn;

    private VisualElement _stepIndicator1;
    private VisualElement _stepIndicator2;
    private VisualElement _stepIndicator3;

    private VisualElement _step0Panel;
    private VisualElement _step1Panel;
    private VisualElement _step2Panel;

    // Step 0
    private VisualElement _bankCardList;
    private Label _inflationInfoLabel;

    // Step 1
    private Label _step1BankNameLabel;
    private Label _step1RateLabel;
    private Slider _amountSlider;
    private Label _amountValueLabel;
    private VisualElement _termButtonsContainer;
    private Label _weeklyPaymentPreview;
    private Label _totalOwedPreview;
    private Button _step1BackBtn;
    private Button _step1NextBtn;

    // Step 2
    private Label _summaryBankLabel;
    private Label _summaryAmountLabel;
    private Label _summaryRateLabel;
    private Label _summaryTermLabel;
    private Label _summaryWeeklyLabel;
    private Label _summaryTotalLabel;
    private Label _summaryFirstPaymentLabel;
    private Button _step2BackBtn;
    private Button _confirmBtn;

    // Credite active
    private ScrollView _activeLoansList;
    private Label _noLoansLabel;
    private Label _totalBurdenLabel;

    // =========================================================================
    // STATE
    // =========================================================================

    private int _currentStep = 0;
    private BankSO _selectedBank = null;
    private float _selectedAmount = 1000f;
    private int _selectedTermDays = 14;

    private readonly List<Button> _termButtons = new();

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void Awake()
    {
        // Butonul din TestUI trebuie înregistrat o singură dată — indiferent de starea acestui GO
        if (mainUIDocument != null)
        {
            creditBtn = mainUIDocument.rootVisualElement.Q<Button>("Credite");
            if (creditBtn != null)
                creditBtn.clicked += Open;
            else
                Debug.LogWarning("[BankPanelUI] CreditBtn nu a fost găsit în mainUIDocument!");
        }
    }

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        _root = uiDocument.rootVisualElement;

        // Ascundem panoul la start — Open() îl face vizibil
        _creditRoot = _root.Q("CreditRoot");
        if (_creditRoot != null)
            _creditRoot.style.display = DisplayStyle.None;

        BindElements();
        RegisterEvents();
        RefreshActiveLoans();
        GoToStep(0);
    }

    void OnDisable()
    {
        if (bankManager != null)
        {
            bankManager.OnLoanTaken -= OnLoanTaken;
            bankManager.OnLoanFullyPaid -= OnLoanFullyPaid;
            bankManager.OnPaymentMade -= OnPaymentMade;
            bankManager.OnRatesUpdated -= RefreshBankCards;
        }
    }

    // =========================================================================
    // BIND
    // =========================================================================

    private void BindElements()
    {
        // creditBtn e deja legat în Awake din mainUIDocument

        _stepIndicator1 = _root.Q("StepIndicator1");
        _stepIndicator2 = _root.Q("StepIndicator2");
        _stepIndicator3 = _root.Q("StepIndicator3");

        _step0Panel = _root.Q("Step0Panel");
        _step1Panel = _root.Q("Step1Panel");
        _step2Panel = _root.Q("Step2Panel");

        exitBtn = _root.Q<Button>("Exit");

        _bankCardList = _root.Q("BankCardList");
        _inflationInfoLabel = _root.Q<Label>("InflationInfoLabel");

        _step1BankNameLabel = _root.Q<Label>("Step1BankNameLabel");
        _step1RateLabel = _root.Q<Label>("Step1RateLabel");
        _amountSlider = _root.Q<Slider>("AmountSlider");
        _amountValueLabel = _root.Q<Label>("AmountValueLabel");
        _termButtonsContainer = _root.Q("TermButtonsContainer");
        _weeklyPaymentPreview = _root.Q<Label>("WeeklyPaymentPreview");
        _totalOwedPreview = _root.Q<Label>("TotalOwedPreview");
        _step1BackBtn = _root.Q<Button>("Step1BackBtn");
        _step1NextBtn = _root.Q<Button>("Step1NextBtn");

        _summaryBankLabel = _root.Q<Label>("SummaryBankLabel");
        _summaryAmountLabel = _root.Q<Label>("SummaryAmountLabel");
        _summaryRateLabel = _root.Q<Label>("SummaryRateLabel");
        _summaryTermLabel = _root.Q<Label>("SummaryTermLabel");
        _summaryWeeklyLabel = _root.Q<Label>("SummaryWeeklyLabel");
        _summaryTotalLabel = _root.Q<Label>("SummaryTotalLabel");
        _summaryFirstPaymentLabel = _root.Q<Label>("SummaryFirstPaymentLabel");
        _step2BackBtn = _root.Q<Button>("Step2BackBtn");
        _confirmBtn = _root.Q<Button>("ConfirmLoanBtn");

        _activeLoansList = _root.Q<ScrollView>("ActiveLoansList");
        _noLoansLabel = _root.Q<Label>("NoLoansLabel");
        _totalBurdenLabel = _root.Q<Label>("TotalBurdenLabel");
    }

    private void RegisterEvents()
    {
        _amountSlider?.RegisterValueChangedCallback(evt =>
        {
            _selectedAmount = Mathf.Round(evt.newValue / 100f) * 100f;
            UpdateStep1Preview();
        });

        if (_step1BackBtn != null) _step1BackBtn.clicked += () => GoToStep(0);
        if (_step1NextBtn != null) _step1NextBtn.clicked += () => GoToStep(2);
        if (_step2BackBtn != null) _step2BackBtn.clicked += () => GoToStep(1);
        if (_confirmBtn != null) _confirmBtn.clicked += ConfirmLoan;
        if (exitBtn != null) exitBtn.clicked += Close;

        if (bankManager != null)
        {
            bankManager.OnLoanTaken += OnLoanTaken;
            bankManager.OnLoanFullyPaid += OnLoanFullyPaid;
            bankManager.OnPaymentMade += OnPaymentMade;
            bankManager.OnRatesUpdated += RefreshBankCards;
        }
    }

    public void Open()
    {
        if (_creditRoot != null)
            _creditRoot.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        if (_creditRoot != null)
            _creditRoot.style.display = DisplayStyle.None;
    }

    // =========================================================================
    // NAVIGARE
    // =========================================================================

    private void GoToStep(int step)
    {
        _currentStep = step;

        ShowPanel(_step0Panel, step == 0);
        ShowPanel(_step1Panel, step == 1);
        ShowPanel(_step2Panel, step == 2);

        SetIndicator(_stepIndicator1, step >= 0);
        SetIndicator(_stepIndicator2, step >= 1);
        SetIndicator(_stepIndicator3, step >= 2);

        switch (step)
        {
            case 0: PopulateBankCards(); break;
            case 1: SetupStep1(); break;
            case 2: PopulateSummary(); break;
        }
    }

    private static void ShowPanel(VisualElement panel, bool visible)
    {
        if (panel == null) return;
        panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        panel.SetEnabled(visible);
    }

    private static void SetIndicator(VisualElement el, bool active)
    {
        if (el == null) return;
        el.EnableInClassList("step-active", active);
        el.EnableInClassList("step-inactive", !active);
    }

    // =========================================================================
    // STEP 0 — Carduri bănci
    // =========================================================================

    private void PopulateBankCards()
    {
        if (_bankCardList == null || bankCardTemplate == null) return;
        _bankCardList.Clear();

        float inflation = inflationManager != null ? inflationManager.CurrentInflation : 0f;

        if (_inflationInfoLabel != null)
            _inflationInfoLabel.text = $"Inflație curentă: {inflation:F1}% — dobânzile reflectă această valoare";

        if (bankManager?.availableBanks == null) return;

        foreach (var bank in bankManager.availableBanks)
        {
            if (bank == null) continue;

            var card = bankCardTemplate.Instantiate();
            float currentRate = bank.GetCurrentAnnualRate(inflation);
            float inflContrib = inflation / 100f * bank.inflationSensitivity * 100f;

            SetLabel(card, "BankNameLabel", bank.bankName);
            SetLabel(card, "CurrentRateLabel", $"{currentRate * 100f:F1}%");
            SetLabel(card, "BaseRateLabel", $"Bază: {bank.baseAnnualRate * 100f:F1}%");
            SetLabel(card, "InflationContribLabel", $"+Inflație: {inflContrib:F1}%");
            SetLabel(card, "LimitRangeLabel", $"{bank.minLoanAmount:F0} – {bank.maxLoanAmount:F0} RON");
            SetLabel(card, "PenaltyLabel", $"{bank.latePenaltyRate * 100f:F0}% / rată restantă");

            if (bank.availableTermDays != null)
                SetLabel(card, "TermsLabel", string.Join(" / ",
                    System.Array.ConvertAll(bank.availableTermDays, d => $"{d}z")));

            var accentBar = card.Q("BankAccentBar");
            if (accentBar != null)
                accentBar.style.backgroundColor = bank.brandColor;

            var selectBtn = card.Q<Button>("SelectBankBtn");
            if (selectBtn != null)
            {
                BankSO captured = bank;
                selectBtn.clicked += () => { _selectedBank = captured; GoToStep(1); };
            }

            _bankCardList.Add(card);
        }
    }

    private void RefreshBankCards() => PopulateBankCards();

    // =========================================================================
    // STEP 1 — Configurare
    // =========================================================================

    private void SetupStep1()
    {
        if (_selectedBank == null) { GoToStep(0); return; }

        float inflation = inflationManager != null ? inflationManager.CurrentInflation : 0f;
        float currentRate = _selectedBank.GetCurrentAnnualRate(inflation);

        if (_step1BankNameLabel != null) _step1BankNameLabel.text = _selectedBank.bankName;
        if (_step1RateLabel != null) _step1RateLabel.text = $"Dobândă: {currentRate * 100f:F1}%/an";

        if (_amountSlider != null)
        {
            _amountSlider.lowValue = _selectedBank.minLoanAmount;
            _amountSlider.highValue = _selectedBank.maxLoanAmount;
            _amountSlider.value = Mathf.Clamp(_selectedAmount,
                                         _selectedBank.minLoanAmount,
                                         _selectedBank.maxLoanAmount);
        }

        BuildTermButtons();
        UpdateStep1Preview();
    }

    private void BuildTermButtons()
    {
        if (_termButtonsContainer == null || _selectedBank == null) return;
        _termButtonsContainer.Clear();
        _termButtons.Clear();

        foreach (int days in _selectedBank.availableTermDays)
        {
            var btn = new Button { text = $"{days} zile" };

            // Layout
            btn.style.marginRight = 8;
            btn.style.marginBottom = 6;
            btn.style.paddingLeft = 20;
            btn.style.paddingRight = 20;
            btn.style.paddingTop = 8;
            btn.style.paddingBottom = 8;
            btn.style.minWidth = 90;
            btn.style.height = 36;
            // Border radius
            btn.style.borderTopLeftRadius = 8;
            btn.style.borderTopRightRadius = 8;
            btn.style.borderBottomLeftRadius = 8;
            btn.style.borderBottomRightRadius = 8;
            // Border
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            // Font
            btn.style.fontSize = 12;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Culori default (neselecțat)
            ApplyTermBtnStyle(btn, false);

            int captured = days;
            btn.clicked += () =>
            {
                _selectedTermDays = captured;
                UpdateTermButtonStyles();
                UpdateStep1Preview();
            };

            _termButtonsContainer.Add(btn);
            _termButtons.Add(btn);
        }

        if (_selectedBank.availableTermDays.Length > 0)
            _selectedTermDays = _selectedBank.availableTermDays[0];

        UpdateTermButtonStyles();
    }

    private void UpdateTermButtonStyles()
    {
        if (_selectedBank == null) return;
        for (int i = 0; i < _termButtons.Count; i++)
        {
            bool sel = _selectedBank.availableTermDays[i] == _selectedTermDays;
            ApplyTermBtnStyle(_termButtons[i], sel);
        }
    }

    private static void ApplyTermBtnStyle(Button btn, bool selected)
    {
        if (selected)
        {
            // Selectat — galben solid
            btn.style.backgroundColor = new StyleColor(new Color(1f, 0.78f, 0.2f));
            btn.style.color = new StyleColor(new Color(0.08f, 0.08f, 0.08f));
            btn.style.borderTopColor = new StyleColor(new Color(1f, 0.78f, 0.2f));
            btn.style.borderBottomColor = new StyleColor(new Color(1f, 0.78f, 0.2f));
            btn.style.borderLeftColor = new StyleColor(new Color(1f, 0.78f, 0.2f));
            btn.style.borderRightColor = new StyleColor(new Color(1f, 0.78f, 0.2f));
        }
        else
        {
            // Neselecțat — transparent cu border subtil
            btn.style.backgroundColor = new StyleColor(new Color(0.12f, 0.13f, 0.17f));
            btn.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.8f));
            btn.style.borderTopColor = new StyleColor(new Color(1f, 0.78f, 0.2f, 0.3f));
            btn.style.borderBottomColor = new StyleColor(new Color(1f, 0.78f, 0.2f, 0.3f));
            btn.style.borderLeftColor = new StyleColor(new Color(1f, 0.78f, 0.2f, 0.3f));
            btn.style.borderRightColor = new StyleColor(new Color(1f, 0.78f, 0.2f, 0.3f));
        }
    }

    private void UpdateStep1Preview()
    {
        if (_selectedBank == null) return;

        float inflation = inflationManager != null ? inflationManager.CurrentInflation : 0f;
        float weekly = _selectedBank.CalculateWeeklyPayment(_selectedAmount, _selectedTermDays, inflation);
        int weeks = Mathf.Max(1, _selectedTermDays / 7);
        float totalOwed = weekly * weeks;

        if (_amountValueLabel != null) _amountValueLabel.text = $"{_selectedAmount:F0} RON";
        if (_weeklyPaymentPreview != null) _weeklyPaymentPreview.text = $"Rată săptămânală: {weekly:F2} RON";
        if (_totalOwedPreview != null) _totalOwedPreview.text = $"Total de rambursat: {totalOwed:F2} RON";
    }

    // =========================================================================
    // STEP 2 — Sumar
    // =========================================================================

    private void PopulateSummary()
    {
        if (_selectedBank == null) { GoToStep(0); return; }

        float inflation = inflationManager != null ? inflationManager.CurrentInflation : 0f;
        float annualRate = _selectedBank.GetCurrentAnnualRate(inflation);
        float weekly = _selectedBank.CalculateWeeklyPayment(_selectedAmount, _selectedTermDays, inflation);
        int weeks = Mathf.Max(1, _selectedTermDays / 7);
        float totalOwed = weekly * weeks;
        int firstPayDay = (TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0) + 7;

        if (_summaryBankLabel != null) _summaryBankLabel.text = _selectedBank.bankName;
        if (_summaryAmountLabel != null) _summaryAmountLabel.text = $"{_selectedAmount:F0} RON";
        if (_summaryRateLabel != null) _summaryRateLabel.text = $"{annualRate * 100f:F1}% / an (fixă)";
        if (_summaryTermLabel != null) _summaryTermLabel.text = $"{_selectedTermDays} zile ({weeks} rate)";
        if (_summaryWeeklyLabel != null) _summaryWeeklyLabel.text = $"{weekly:F2} RON / săptămână";
        if (_summaryTotalLabel != null) _summaryTotalLabel.text = $"{totalOwed:F2} RON";
        if (_summaryFirstPaymentLabel != null) _summaryFirstPaymentLabel.text = $"Prima rată: Ziua {firstPayDay}";
    }

    // =========================================================================
    // CONFIRMARE
    // =========================================================================

    private void ConfirmLoan()
    {
        if (_selectedBank == null || bankManager == null) return;

        if (bankManager.TryTakeLoan(_selectedBank, _selectedAmount, _selectedTermDays, out BankLoan loan))
        {
            Debug.Log($"[BankPanelUI] Credit contractat: {loan}");
            GoToStep(0);
            RefreshActiveLoans();
        }
        else
        {
            Debug.LogWarning("[BankPanelUI] Creditul nu a putut fi acordat.");
        }
    }

    // =========================================================================
    // CREDITE ACTIVE
    // =========================================================================

    private void RefreshActiveLoans()
    {
        if (_activeLoansList == null || _activeLoanRowTemplate == null) return;
        _activeLoansList.Clear();

        var loans = bankManager?.ActiveLoans;
        bool hasLoans = loans != null && loans.Count > 0;

        if (_noLoansLabel != null) _noLoansLabel.style.display = hasLoans ? DisplayStyle.None : DisplayStyle.Flex;
        if (_activeLoansList != null) _activeLoansList.style.display = hasLoans ? DisplayStyle.Flex : DisplayStyle.None;

        if (!hasLoans)
        {
            if (_totalBurdenLabel != null) _totalBurdenLabel.text = "";
            return;
        }

        foreach (var loan in loans)
        {
            var row = _activeLoanRowTemplate.Instantiate();

            SetLabel(row, "LoanBankLabel", loan.bank.bankName);
            SetLabel(row, "LoanDetailsLabel", $"{loan.principal:F0} RON · {loan.annualRateSnapshot * 100f:F1}%/an · {loan.termDays} zile");
            SetLabel(row, "NextPaymentAmountLabel", $"{loan.weeklyPayment:F2} RON");
            SetLabel(row, "NextPaymentDayLabel", $"Ziua {loan.nextPaymentDay}");

            float progress = loan.totalPaid / Mathf.Max(1f, loan.totalOwed);
            var progressBar = row.Q("LoanProgressBar");
            if (progressBar != null)
                progressBar.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);

            SetLabel(row, "LoanProgressLabel", $"{progress * 100f:F0}%");

            _activeLoansList.Add(row);
        }

        if (_totalBurdenLabel != null)
            _totalBurdenLabel.text = $"Total rate săptămânale: {bankManager.GetTotalMonthlyBurden():F2} RON";
    }

    // =========================================================================
    // EVENTS BANKMANAGER
    // =========================================================================

    private void OnLoanTaken(BankLoan loan) => RefreshActiveLoans();
    private void OnLoanFullyPaid(BankLoan loan) => RefreshActiveLoans();
    private void OnPaymentMade(BankLoan l, float _) => RefreshActiveLoans();

    // =========================================================================
    // HELPER
    // =========================================================================

    private static void SetLabel(VisualElement root, string name, string text)
    {
        var label = root.Q<Label>(name);
        if (label != null) label.text = text;
    }
}