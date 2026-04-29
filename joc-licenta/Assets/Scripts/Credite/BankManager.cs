using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Gestionează toate băncile și creditele active ale jucătorului.
/// Se abonează la TimeManager pentru rate săptămânale
/// și la InflationManager pentru actualizarea dobânzilor afișate.
/// </summary>
public class BankManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static BankManager Instance { get; private set; }

    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("Bănci disponibile")]
    [SerializeField] public BankSO[] availableBanks;

    [Header("Referințe")]
    [SerializeField] private InflationManager inflationManager;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>Declanșat când se ia un credit nou.</summary>
    public event Action<BankLoan> OnLoanTaken;

    /// <summary>Declanșat când se plătește o rată (loan, amountPaid).</summary>
    public event Action<BankLoan, float> OnPaymentMade;

    /// <summary>Declanșat când un credit e achitat complet.</summary>
    public event Action<BankLoan> OnLoanFullyPaid;

    /// <summary>Declanșat când rata e restantă (jucătorul nu a avut bani).</summary>
    public event Action<BankLoan> OnPaymentMissed;

    /// <summary>Declanșat când se schimbă dobânzile (inflație nouă).</summary>
    public event Action OnRatesUpdated;

    // =========================================================================
    // STATE
    // =========================================================================

    private readonly List<BankLoan> _activeLoans = new();
    public IReadOnlyList<BankLoan> ActiveLoans => _activeLoans;

    private float _currentInflation = 0f;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Abonare inflație
        if (inflationManager != null)
        {
            _currentInflation = inflationManager.CurrentInflation;
            inflationManager.OnInflationChanged += OnInflationChanged;
        }
        else
        {
            Debug.LogWarning("[BankManager] InflationManager nu e asignat — dobânzile nu vor fluctua.");
        }

        // Abonare zile — pentru plata ratelor
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += OnDayChanged;
    }

    void OnDestroy()
    {
        if (inflationManager != null)
            inflationManager.OnInflationChanged -= OnInflationChanged;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= OnDayChanged;
    }

    // =========================================================================
    // API PUBLIC
    // =========================================================================

    /// <summary>
    /// Returnează dobânda anuală curentă a unei bănci (cu inflația inclusă).
    /// Folosit de UI pentru afișare în timp real.
    /// </summary>
    public float GetCurrentRate(BankSO bank)
        => bank.GetCurrentAnnualRate(_currentInflation);

    /// <summary>
    /// Verifică dacă jucătorul poate lua un credit (sumă în limite, etc).
    /// </summary>
    public bool CanTakeLoan(BankSO bank, float amount, out string reason)
    {
        if (amount < bank.minLoanAmount)
        {
            reason = $"Suma minimă este {bank.minLoanAmount:F0} RON.";
            return false;
        }
        if (amount > bank.maxLoanAmount)
        {
            reason = $"Suma maximă este {bank.maxLoanAmount:F0} RON.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Jucătorul contractează un credit.
    /// </summary>
    public bool TryTakeLoan(BankSO bank, float amount, int termDays, out BankLoan loan)
    {
        loan = null;

        if (!CanTakeLoan(bank, amount, out string reason))
        {
            Debug.Log($"[BankManager] Credit refuzat: {reason}");
            return false;
        }

        int currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;

        loan = new BankLoan(bank, amount, termDays, _currentInflation, currentDay);
        _activeLoans.Add(loan);

        // Virăm banii în contul jucătorului
        if (GameManager.Instance != null)
            GameManager.Instance.AddMoney(Mathf.RoundToInt(amount));

        Debug.Log($"[BankManager] Credit luat: {loan}");
        OnLoanTaken?.Invoke(loan);
        return true;
    }

    // =========================================================================
    // PLATA RATELOR (automată, declanșată de TimeManager.OnDayChanged)
    // =========================================================================

    private void OnDayChanged()
    {
        int currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;

        for (int i = _activeLoans.Count - 1; i >= 0; i--)
        {
            BankLoan loan = _activeLoans[i];

            if (currentDay < loan.nextPaymentDay) continue;

            ProcessPayment(loan);

            if (loan.IsFullyPaid)
            {
                Debug.Log($"[BankManager] Credit achitat complet: {loan.bank.bankName}");
                OnLoanFullyPaid?.Invoke(loan);
                _activeLoans.RemoveAt(i);
            }
        }
    }

    private void ProcessPayment(BankLoan loan)
    {
        // Ultima rată poate fi mai mică (rest)
        float due = Mathf.Min(loan.weeklyPayment, loan.RemainingBalance);

        if (GameManager.Instance != null && GameManager.Instance.TrySpendMoney(Mathf.RoundToInt(due)))
        {
            loan.totalPaid += due;
            loan.weeksRemaining = Mathf.Max(0, loan.weeksRemaining - 1);
            loan.nextPaymentDay += 7;

            Debug.Log($"[BankManager] Rată plătită: {due:F2} RON → {loan.bank.bankName} | Sold rămas: {loan.RemainingBalance:F2} RON");
            OnPaymentMade?.Invoke(loan, due);
        }
        else
        {
            // Nu are bani — penalizare
            float penalty = loan.weeklyPayment * loan.bank.latePenaltyRate;
            loan.totalOwed += Mathf.RoundToInt(penalty);
            loan.nextPaymentDay += 7; // încearcă din nou săptămâna viitoare

            Debug.LogWarning($"[BankManager] Rată restantă! Penalizare: {penalty:F2} RON → {loan.bank.bankName}");
            OnPaymentMissed?.Invoke(loan);
        }
    }

    // =========================================================================
    // INFLAȚIE
    // =========================================================================

    private void OnInflationChanged(float newInflation)
    {
        _currentInflation = newInflation;
        // Ratele creditelor existente sunt FIXE (contractate la dobânda veche)
        // Dobânzile afișate pentru credite NOI se actualizează via GetCurrentRate()
        OnRatesUpdated?.Invoke();

        Debug.Log($"[BankManager] Inflație nouă: {newInflation:F2}% — dobânzile afișate actualizate.");
    }

    // =========================================================================
    // UTILITAR
    // =========================================================================

    /// <summary>Total de plătit pe toate creditele active.</summary>
    public float GetTotalMonthlyBurden()
    {
        float total = 0f;
        foreach (var loan in _activeLoans)
            total += loan.weeklyPayment;
        return total;
    }
}