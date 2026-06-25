using UnityEngine;
using UnityEngine.UIElements;

public class ActiveLoansController
{
    private VisualElement _root;
    private BankPanelUI _mainUI;
    private VisualTreeAsset _rowTemplate;

    private VisualElement _activeLoansList;
    private Label _totalBurdenLabel;

    public ActiveLoansController(VisualElement root, BankPanelUI mainUI, VisualTreeAsset rowTemplate)
    {
        _root = root;
        _mainUI = mainUI;
        _rowTemplate = rowTemplate;

        _activeLoansList = _root.Q("ActiveLoansSection");
        _totalBurdenLabel = _root.Q<Label>("TotalBurdenLabel");
    }

    public void Refresh()
    {
        if (_activeLoansList == null || _mainUI.bankManager == null || _rowTemplate == null) return;

        _activeLoansList.Clear();

        // Dacă metoda ta publică din BankManager pentru a expune lista se numește diferit,
        // (ex: GetActiveLoans()), te rog să o schimbi mai jos în _mainUI.bankManager.GetActiveLoans()
        var activeLoans = _mainUI.bankManager.GetActiveLoans();
        if (activeLoans == null) return;

        foreach (var loan in activeLoans)
        {
            var row = _rowTemplate.Instantiate();

            SetLabel(row, "LoanBankNameLabel", loan.bank.bankName);
            SetLabel(row, "LoanPrincipalLabel", $"{loan.principal:F0} RON");
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
            _totalBurdenLabel.text = $"Total rate săptămânale: {_mainUI.bankManager.GetTotalMonthlyBurden():F2} RON";
    }

    private void SetLabel(VisualElement root, string name, string text)
    {
        var label = root.Q<Label>(name);
        if (label != null) label.text = text;
    }
}