using UnityEngine;
using UnityEngine.UIElements;

public class LoanSummaryController
{
    private VisualElement _root;
    private BankPanelUI _mainUI;

    private Label _bankLabel, _amountLabel, _rateLabel, _termLabel, _weeklyLabel, _totalLabel, _firstPaymentLabel;
    private Button _backBtn, _confirmBtn;

    public LoanSummaryController(VisualElement root, BankPanelUI mainUI)
    {
        _root = root;
        _mainUI = mainUI;

        var panel = _root.Q("Step2Panel");
        if (panel == null) return;

        _bankLabel = panel.Q<Label>("SummaryBankLabel");
        _amountLabel = panel.Q<Label>("SummaryAmountLabel");
        _rateLabel = panel.Q<Label>("SummaryRateLabel");
        _termLabel = panel.Q<Label>("SummaryTermLabel");
        _weeklyLabel = panel.Q<Label>("SummaryWeeklyLabel");
        _totalLabel = panel.Q<Label>("SummaryTotalLabel");
        _firstPaymentLabel = panel.Q<Label>("SummaryFirstPaymentLabel");

        _backBtn = panel.Q<Button>("Step2BackBtn");
        _confirmBtn = panel.Q<Button>("ConfirmLoanBtn");

        if (_backBtn != null) _backBtn.clicked += () => _mainUI.GoToStep(1);
        if (_confirmBtn != null) _confirmBtn.clicked += Confirm;
    }

    public void SetVisible(bool visible)
    {
        var panel = _root.Q("Step2Panel");
        if (panel != null) panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void Populate()
    {
        var bank = _mainUI.SelectedBank;
        var config = _mainUI.configController;
        if (bank == null || config == null) return;

        float amount = config.SelectedAmount;
        int termDays = config.SelectedTermDays;
        float inflation = _mainUI.inflationManager != null ? _mainUI.inflationManager.CurrentInflation : 0f;

        float currentRate = bank.GetCurrentAnnualRate(inflation);
        float weekly = bank.CalculateWeeklyPayment(amount, termDays, inflation);
        float total = bank.CalculateTotalOwed(amount, termDays, inflation);

        if (_bankLabel != null) _bankLabel.text = bank.bankName;
        if (_amountLabel != null) _amountLabel.text = $"{amount:F0} RON";
        if (_rateLabel != null) _rateLabel.text = $"{currentRate * 100f:F1}%/an";
        if (_termLabel != null) _termLabel.text = $"{termDays} zile";
        if (_weeklyLabel != null) _weeklyLabel.text = $"{weekly:F2} RON";
        if (_totalLabel != null) _totalLabel.text = $"{total:F2} RON";

        // Optional, dacă folosești un TimeManager:
        // int nextDay = TimeManager.Instance.CurrentDay + 7;
        // if (_firstPaymentLabel != null) _firstPaymentLabel.text = $"Ziua {nextDay}";
    }

    private void Confirm()
    {
        var bank = _mainUI.SelectedBank;
        var config = _mainUI.configController;
        if (bank == null || config == null || _mainUI.bankManager == null) return;

        bool success = _mainUI.bankManager.TryTakeLoan(bank, config.SelectedAmount, config.SelectedTermDays, out BankLoan loan);
        if (success)
        {
            Debug.Log("Credit acordat cu succes!");
            _mainUI.GoToStep(0); // Revine la meniul principal
        }
        else
        {
            Debug.LogWarning("Nu s-a putut acorda creditul (Limita este probabil depășită).");
        }
    }
}