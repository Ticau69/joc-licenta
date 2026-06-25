using UnityEngine.UIElements;

public class BankSelectionController
{
    private VisualElement _root;
    private BankPanelUI _mainUI;
    private VisualTreeAsset _bankCardTemplate;

    private VisualElement _bankCardList;
    private Label _inflationLabel;

    public BankSelectionController(VisualElement root, BankPanelUI mainUI, VisualTreeAsset template)
    {
        _root = root;
        _mainUI = mainUI;
        _bankCardTemplate = template;

        _bankCardList = _root.Q("BankCardList");
        _inflationLabel = _root.Q<Label>("InflationInfoLabel");
    }

    public void SetVisible(bool visible)
    {
        var panel = _root.Q("Step0Panel");
        if (panel != null) panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void Refresh()
    {
        if (_bankCardList == null || _mainUI.bankManager == null) return;

        _bankCardList.Clear();

        float inflation = _mainUI.inflationManager != null ? _mainUI.inflationManager.CurrentInflation : 0f;
        if (_inflationLabel != null)
            _inflationLabel.text = $"Inflație curentă: {inflation:F1}% — dobânzile reflectă această valoare";

        if (_mainUI.bankManager.availableBanks == null) return;

        foreach (var bank in _mainUI.bankManager.availableBanks)
        {
            if (bank == null) continue;

            var card = _bankCardTemplate.Instantiate();
            var selectBtn = card.Q<Button>("SelectBankBtn");

            // Exemplu de populare a datelor (le poți adăuga și pe restul pe care le aveai)
            var nameLabel = card.Q<Label>("BankNameLabel");
            if (nameLabel != null) nameLabel.text = bank.bankName;

            if (selectBtn != null)
            {
                selectBtn.clicked += () =>
                {
                    _mainUI.SelectedBank = bank;
                    // Trecem la pasul 1 doar dacă selectăm o bancă
                    _mainUI.GoToStep(1);
                };
            }

            _bankCardList.Add(card);
        }
    }
}