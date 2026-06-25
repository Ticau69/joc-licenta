using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class LoanConfigController
{
    private VisualElement _root;
    private BankPanelUI _mainUI;

    private Label _bankNameLabel, _rateLabel, _amountValueLabel, _weeklyPreview, _totalPreview;
    private Slider _amountSlider;
    private VisualElement _termContainer;
    private Button _backBtn, _nextBtn;

    private List<Button> _termButtons = new List<Button>();

    // Stocăm configurația aici pentru a o accesa de la Pasul 2
    public float SelectedAmount { get; private set; }
    public int SelectedTermDays { get; private set; }

    public LoanConfigController(VisualElement root, BankPanelUI mainUI)
    {
        _root = root;
        _mainUI = mainUI;

        var panel = _root.Q("Step1Panel");
        if (panel == null) return;

        _bankNameLabel = panel.Q<Label>("Step1BankNameLabel");
        _rateLabel = panel.Q<Label>("Step1RateLabel");
        _amountSlider = panel.Q<Slider>("AmountSlider");
        _amountValueLabel = panel.Q<Label>("AmountValueLabel");
        _termContainer = panel.Q("TermButtonsContainer");
        _weeklyPreview = panel.Q<Label>("WeeklyPaymentPreview");
        _totalPreview = panel.Q<Label>("TotalOwedPreview");

        _backBtn = panel.Q<Button>("Step1BackBtn");
        _nextBtn = panel.Q<Button>("Step1NextBtn");

        if (_backBtn != null) _backBtn.clicked += () => _mainUI.GoToStep(0);
        if (_nextBtn != null) _nextBtn.clicked += () => _mainUI.GoToStep(2);

        if (_amountSlider != null)
        {
            _amountSlider.RegisterValueChangedCallback(evt =>
            {
                SelectedAmount = evt.newValue;
                UpdatePreview();
            });
        }
    }

    public void SetVisible(bool visible)
    {
        var panel = _root.Q("Step1Panel");
        if (panel != null) panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void Setup()
    {
        var bank = _mainUI.SelectedBank;
        if (bank == null) return;

        SelectedAmount = bank.minLoanAmount;
        SelectedTermDays = bank.availableTermDays.Length > 0 ? bank.availableTermDays[0] : 14;

        float inflation = _mainUI.inflationManager != null ? _mainUI.inflationManager.CurrentInflation : 0f;
        float currentRate = bank.GetCurrentAnnualRate(inflation);

        if (_bankNameLabel != null) _bankNameLabel.text = bank.bankName;
        if (_rateLabel != null) _rateLabel.text = $"Dobândă: {currentRate * 100f:F1}%/an";

        if (_amountSlider != null)
        {
            _amountSlider.lowValue = bank.minLoanAmount;
            _amountSlider.highValue = bank.maxLoanAmount;
            _amountSlider.value = SelectedAmount;
        }

        BuildTermButtons(bank);
        UpdatePreview();
    }

    private void BuildTermButtons(BankSO bank)
    {
        if (_termContainer == null) return;

        _termContainer.Clear();
        _termButtons.Clear();

        foreach (int days in bank.availableTermDays)
        {
            var btn = new Button { text = $"{days} zile" };
            btn.AddToClassList("term-btn");

            int captured = days;
            btn.clicked += () =>
            {
                SelectedTermDays = captured;
                UpdateTermButtonsStyle();
                UpdatePreview();
            };

            _termContainer.Add(btn);
            _termButtons.Add(btn);
        }
        UpdateTermButtonsStyle();
    }

    private void UpdateTermButtonsStyle()
    {
        foreach (var btn in _termButtons)
        {
            if (btn.text.Contains(SelectedTermDays.ToString()))
                btn.AddToClassList("term-btn-selected");
            else
                btn.RemoveFromClassList("term-btn-selected");
        }
    }

    private void UpdatePreview()
    {
        var bank = _mainUI.SelectedBank;
        if (bank == null) return;

        float inflation = _mainUI.inflationManager != null ? _mainUI.inflationManager.CurrentInflation : 0f;
        float weekly = bank.CalculateWeeklyPayment(SelectedAmount, SelectedTermDays, inflation);
        float total = bank.CalculateTotalOwed(SelectedAmount, SelectedTermDays, inflation);

        if (_amountValueLabel != null) _amountValueLabel.text = $"{SelectedAmount:F0} RON";
        if (_weeklyPreview != null) _weeklyPreview.text = $"{weekly:F2} RON";
        if (_totalPreview != null) _totalPreview.text = $"{total:F2} RON";
    }
}