using UnityEngine;
using System;
using UnityEngine.UIElements;

public class GameManager : MonoBehaviour
{
    // Singleton - Accesibil de oriunde
    public static GameManager Instance { get; private set; }

    [Header("Economie")]
    [SerializeField] private int startingMoney = 5000;
    public int CurrentMoney { get; private set; }

    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    private Label moneyText;

    // Eveniment pentru UI (ca să actualizăm textul doar când se schimbă banii)
    public event Action OnMoneyChanged;

    void Awake()
    {
        // Inițializăm banii
        CurrentMoney = startingMoney;
        OnMoneyChanged += UpdateMoneyUI;
    }

    void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        var hotbar = root.Q<VisualElement>("HotBar");

        moneyText = root.Q<Label>("Money");
        Debug.Log("Money Text Found: " + (moneyText != null));
    }

    void Start()
    {
        // Inițializăm UI-ul la start
        UpdateMoneyUI();
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
        if (moneyText != null)
        {
            moneyText.text = $"{CurrentMoney}";
        }
    }

    void OnDestroy()
    {
        OnMoneyChanged -= UpdateMoneyUI;
    }
}
