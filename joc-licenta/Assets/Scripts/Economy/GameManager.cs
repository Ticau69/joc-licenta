using UnityEngine;
using System;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Singleton - Accesibil de oriunde
    public static GameManager Instance { get; private set; }

    [Header("Economie")]
    [SerializeField] private int startingMoney = 5000;
    public int CurrentMoney { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI moneyText; // Ex: "Bani: 5000"

    // Eveniment pentru UI (ca să actualizăm textul doar când se schimbă banii)
    public event Action OnMoneyChanged;

    void Awake()
    {
        // Inițializăm banii
        CurrentMoney = startingMoney;
        OnMoneyChanged += UpdateMoneyUI;
        OnMoneyChanged?.Invoke(); // Actualizăm UI-ul la start
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount <= CurrentMoney)
        {
            CurrentMoney -= amount;
            OnMoneyChanged?.Invoke(); // Actualizăm UI-ul
            return true;
        }
        return false; // Nu sunt suficienți bani
    }

    public void AddMoney(int amount)
    {
        CurrentMoney += amount;
        OnMoneyChanged?.Invoke();
    }

    public void UpdateMoneyUI()
    {
        moneyText.text = "BANI: " + CurrentMoney;
    }

    void OnDestroy()
    {
        OnMoneyChanged -= UpdateMoneyUI;
    }
}
