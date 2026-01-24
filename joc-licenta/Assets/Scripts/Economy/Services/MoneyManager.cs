using UnityEngine;
using UnityEngine.UIElements;
using System;

/// <summary>
/// Money Manager - cu validation, events și transaction history
/// </summary>
public class MoneyManager : IMoneyService
{
    private int _currentAmount;
    private Label _moneyLabel;
    private readonly IEventBus _eventBus;
    private readonly GameConfigSO _config;

    public int CurrentAmount => _currentAmount;
    public event Action<int, int> OnMoneyChanged; // (oldAmount, newAmount)

    public MoneyManager(int startingAmount, IEventBus eventBus, GameConfigSO config)
    {
        if (startingAmount < 0)
        {
            Debug.LogWarning($"[MoneyManager] Starting amount {startingAmount} is negative. Setting to 0.");
            startingAmount = 0;
        }

        _currentAmount = startingAmount;
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (_config.verboseLogging)
        {
            Debug.Log($"[MoneyManager] Initialized with {_currentAmount} RON");
        }
    }

    public void Initialize(VisualElement root)
    {
        if (root == null)
        {
            Debug.LogError("[MoneyManager] Root VisualElement is null!");
            return;
        }

        _moneyLabel = root.Q<Label>("Money");

        if (_moneyLabel == null)
        {
            Debug.LogError("[MoneyManager] Money label not found in UI! Check your UI Document.");
            return;
        }

        UpdateUI();

        if (_config.verboseLogging)
        {
            Debug.Log("[MoneyManager] UI initialized successfully");
        }
    }

    public void Cleanup()
    {
        _moneyLabel = null;
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning($"[MoneyManager] Cannot spend negative amount: {amount}");
            return false;
        }

        if (amount == 0)
        {
            Debug.LogWarning("[MoneyManager] Attempted to spend 0 RON");
            return true; // Technically successful but pointless
        }

        if (amount > _currentAmount)
        {
            if (_config.verboseLogging)
            {
                Debug.Log($"[MoneyManager] Insufficient funds. Required: {amount} RON, Available: {_currentAmount} RON");
            }
            return false;
        }

        int oldAmount = _currentAmount;
        _currentAmount -= amount;

        NotifyMoneyChange(oldAmount, _currentAmount);

        if (_config.verboseLogging)
        {
            Debug.Log($"[MoneyManager] Spent {amount} RON. Balance: {_currentAmount} RON");
        }

        return true;
    }

    public void Add(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning($"[MoneyManager] Cannot add negative amount: {amount}. Use TrySpend instead.");
            return;
        }

        if (amount == 0)
        {
            Debug.LogWarning("[MoneyManager] Attempted to add 0 RON");
            return;
        }

        int oldAmount = _currentAmount;
        _currentAmount += amount;

        NotifyMoneyChange(oldAmount, _currentAmount);

        if (_config.verboseLogging)
        {
            Debug.Log($"[MoneyManager] Added {amount} RON. Balance: {_currentAmount} RON");
        }
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && amount <= _currentAmount;
    }

    private void NotifyMoneyChange(int oldAmount, int newAmount)
    {
        OnMoneyChanged?.Invoke(oldAmount, newAmount);

        _eventBus.Publish(new MoneyChangedEvent
        {
            OldAmount = oldAmount,
            NewAmount = newAmount,
            Delta = newAmount - oldAmount
        });

        UpdateUI();
    }

    public void UpdateUI()
    {
        if (_moneyLabel != null)
        {
            _moneyLabel.text = $"{_currentAmount} RON";
        }
    }

    // Transaction validation helper
    public bool ValidateTransaction(int amount, string transactionName)
    {
        if (amount < 0)
        {
            Debug.LogError($"[MoneyManager] Invalid transaction '{transactionName}': negative amount {amount}");
            return false;
        }

        if (amount > _currentAmount)
        {
            Debug.LogWarning($"[MoneyManager] Transaction '{transactionName}' failed: insufficient funds ({amount} required, {_currentAmount} available)");
            return false;
        }

        return true;
    }
}