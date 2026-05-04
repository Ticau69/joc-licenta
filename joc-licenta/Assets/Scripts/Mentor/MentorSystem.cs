using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sistemul Mentor — personaj fictiv care ghidează jucătorul cu sfaturi educaționale.
/// Folosește scaffolding cu fading: activ intens primele 30 de minute, 
/// apoi se retrage treptat pentru a încuraja independența jucătorului.
/// </summary>
public class MentorSystem : MonoBehaviour
{
    public static MentorSystem Instance { get; private set; }

    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("Configurare Mentor")]
    [SerializeField] private MentorMessageSO messageLibrary;

    [Header("Faze de activitate (minute reale)")]
    [SerializeField] private float phase1EndMinutes = 10f;  // Foarte activ
    [SerializeField] private float phase2EndMinutes = 20f;  // Moderat
    [SerializeField] private float phase3EndMinutes = 30f;  // Rar
    // Faza 4 = 30+ min → complet silențios

    [Header("Cooldown între mesaje (secunde)")]
    [SerializeField] private float minCooldownPhase1 = 30f;
    [SerializeField] private float minCooldownPhase2 = 90f;
    [SerializeField] private float minCooldownPhase3 = 180f;

    [Header("Referințe")]
    [SerializeField] private MentorUIController mentorUI;

    // =========================================================================
    // EVENTS
    // =========================================================================

    public event Action<string, Sprite> OnMentorMessage; // (text, avatarSprite)

    // =========================================================================
    // STATE
    // =========================================================================

    private float _sessionTimeSeconds = 0f;
    private float _lastMessageTime = -999f;
    private bool _isInitialized = false;

    // Tracking pentru mesaje afișate (să nu repetăm prea des)
    private readonly Dictionary<MentorEventType, float> _lastEventTime
        = new Dictionary<MentorEventType, float>();

    private const float EVENT_REPEAT_COOLDOWN = 120f;

    // Tracker stoc epuizat per produs (pentru RepeatedOutOfStock)
    private readonly Dictionary<ProductType, int> _outOfStockCount
        = new Dictionary<ProductType, int>();
    private const int OUT_OF_STOCK_THRESHOLD = 3; // 2 minute între același tip de eveniment

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
        StartCoroutine(InitializeDelayed());
    }

    private IEnumerator InitializeDelayed()
    {
        yield return new WaitForSeconds(1f);

        SubscribeToEvents();
        _isInitialized = true;

        // Mesaj de bun venit după 2 secunde
        yield return new WaitForSeconds(2f);
        TriggerEvent(MentorEventType.Welcome);
    }

    void Update()
    {
        if (!_isInitialized) return;
        _sessionTimeSeconds += Time.unscaledDeltaTime;
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // =========================================================================
    // SUBSCRIPTIONS
    // =========================================================================

    private IEventBus _eventBus;

    private void SubscribeToEvents()
    {
        if (!ServiceLocator.Instance.TryGet(out _eventBus)) return;

        _eventBus.Subscribe<ShowNotificationEvent>(OnNotificationEvent);

        // Inflație
        if (InflationManager.Instance != null)
            InflationManager.Instance.OnShockApplied += OnInflationShock;

        // Credite
        if (BankManager.Instance != null)
        {
            BankManager.Instance.OnLoanTaken += OnLoanTaken;
            BankManager.Instance.OnPaymentMissed += OnPaymentMissed;
        }

        // TimeManager — zi nouă
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += OnNewDay;

        // Stoc epuizat repetat
        _eventBus.Subscribe<StockChangedEvent>(OnStockChanged);
    }

    private void UnsubscribeFromEvents()
    {
        if (_eventBus != null)
            _eventBus.Unsubscribe<ShowNotificationEvent>(OnNotificationEvent);

        if (InflationManager.Instance != null)
            InflationManager.Instance.OnShockApplied -= OnInflationShock;

        if (BankManager.Instance != null)
        {
            BankManager.Instance.OnLoanTaken -= OnLoanTaken;
            BankManager.Instance.OnPaymentMissed -= OnPaymentMissed;
        }

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= OnNewDay;

        if (_eventBus != null)
            _eventBus.Unsubscribe<StockChangedEvent>(OnStockChanged);
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void OnInflationShock()
    {
        float inflation = InflationManager.Instance?.CurrentInflation ?? 1f;
        if (inflation > 1.05f)
            TriggerEvent(MentorEventType.InflationSpike);
        else
            TriggerEvent(MentorEventType.Deflation);
    }

    private void OnLoanTaken(BankLoan loan)
    {
        TriggerEvent(MentorEventType.LoanTaken);
    }

    private void OnPaymentMissed(BankLoan loan)
    {
        TriggerEvent(MentorEventType.LoanMissed);
    }

    private int _dayCount = 0;
    private void OnNewDay()
    {
        _dayCount++;

        // Zi neprofitabilă / profitabilă — verificăm cash flow
        if (GameManager.Instance != null && _dayCount > 1)
        {
            // Simplificat: verificăm dacă banii sunt sub pragul de risc
            int money = GameManager.Instance.CurrentMoney;
            if (money < 500)
                TriggerEvent(MentorEventType.LowFunds);
        }
    }

    private void OnStockChanged(StockChangedEvent evt)
    {
        if (evt.NewStock > 0) return; // Ne interesează doar când ajunge la 0
        if (evt.Product == ProductType.None) return;

        if (!_outOfStockCount.ContainsKey(evt.Product))
            _outOfStockCount[evt.Product] = 0;

        _outOfStockCount[evt.Product]++;

        if (_outOfStockCount[evt.Product] >= OUT_OF_STOCK_THRESHOLD)
        {
            _outOfStockCount[evt.Product] = 0; // Reset după ce am notificat
            TriggerEvent(MentorEventType.RepeatedOutOfStock);
        }
    }

    private void OnNotificationEvent(ShowNotificationEvent evt)
    {
        // Interpretăm notificările existente ca trigger-e pentru mentor
        string title = evt.Title?.ToLower() ?? "";
        string message = evt.Message?.ToLower() ?? "";

        if (title.Contains("demisie") || message.Contains("plecat"))
            TriggerEvent(MentorEventType.EmployeeResigned);

        else if (title.Contains("sanit") && evt.Type == NotificationType.Error)
            TriggerEvent(MentorEventType.SanitaryFine);

        else if (title.Contains("sanit") && evt.Type == NotificationType.Success)
            TriggerEvent(MentorEventType.SanitaryPass);

        else if (title.Contains("indisponibil") || message.Contains("nu l-a găsit"))
            TriggerEvent(MentorEventType.OutOfStock);

        else if (message.Contains("supărat") || title.Contains("furnizor"))
            TriggerEvent(MentorEventType.SupplierAngry);
    }

    // =========================================================================
    // COMPETITOR MONITORING (apelat din InventoryUIController sau CompetitiveMarketManager)
    // =========================================================================

    /// <summary>
    /// Apelează din exterior când un competitor devine mai ieftin la un produs urmărit de jucător.
    /// </summary>
    public void NotifyCompetitorCheaper()
    {
        TriggerEvent(MentorEventType.CompetitorCheaper);
    }

    public void NotifyPlayerCheapest()
    {
        TriggerEvent(MentorEventType.PlayerCheapest);
    }

    public void NotifyFirstSupplierOrder()
    {
        TriggerEvent(MentorEventType.FirstSupplierOrder);
    }

    public void NotifyFleetFull()
    {
        TriggerEvent(MentorEventType.FleetFull);
    }

    public void NotifyBuildMenuOpened()
    {
        TriggerEvent(MentorEventType.BuildMenuOpened);
    }

    public void NotifyPriceBelowCost()
    {
        TriggerEvent(MentorEventType.PriceBelowCost);
    }

    public void NotifyMultipleLoans()
    {
        TriggerEvent(MentorEventType.MultipleLoans);
    }

    public void NotifyProfitableDay()
    {
        TriggerEvent(MentorEventType.ProfitableDay);
    }

    public void NotifyUnprofitableDay()
    {
        TriggerEvent(MentorEventType.UnprofitableDay);
    }

    // =========================================================================
    // CORE — TRIGGER & PHASE CHECK
    // =========================================================================

    public void TriggerEvent(MentorEventType eventType)
    {
        if (!_isInitialized || messageLibrary == null) return;

        // ── 1. Verificăm faza curentă ──────────────────────────────────────
        MentorPhase phase = GetCurrentPhase();
        if (phase == MentorPhase.Silent) return; // Faza 4 — mentor a ieșit din scenă

        // ── 2. Cooldown global între mesaje ──────────────────────────────────
        // Evenimentele one-time bypass-eaza cooldown-ul global
        if (!IsOneTimeEvent(eventType))
        {
            float globalCooldown = GetGlobalCooldown(phase);
            if (Time.unscaledTime - _lastMessageTime < globalCooldown) return;
        }

        // ── 3. Cooldown per tip de eveniment ──────────────────────────────
        if (_lastEventTime.TryGetValue(eventType, out float lastTime))
        {
            if (Time.unscaledTime - lastTime < EVENT_REPEAT_COOLDOWN) return;
        }

        // ── 4. Luăm mesajele pentru acest eveniment ───────────────────────
        List<MentorMessageSO.MentorMessage> pool = GetMessagePool(eventType);
        if (pool == null || pool.Count == 0) return;

        // ── 5. Filtrăm după importanță vs faza curentă ────────────────────
        List<MentorMessageSO.MentorMessage> eligible = new List<MentorMessageSO.MentorMessage>();
        foreach (var msg in pool)
        {
            if (IsMessageEligibleForPhase(msg.importance, phase))
                eligible.Add(msg);
        }

        if (eligible.Count == 0) return;

        // ── 6. Alegem random și afișăm ────────────────────────────────────
        var chosen = eligible[UnityEngine.Random.Range(0, eligible.Count)];
        ShowMessage(chosen.text, phase, eventType);

        _lastMessageTime = Time.unscaledTime;
        _lastEventTime[eventType] = Time.unscaledTime;
    }

    // =========================================================================
    // PHASE LOGIC
    // =========================================================================

    public MentorPhase GetCurrentPhase()
    {
        float minutes = _sessionTimeSeconds / 60f;

        if (minutes < phase1EndMinutes) return MentorPhase.VeryActive;
        if (minutes < phase2EndMinutes) return MentorPhase.Moderate;
        if (minutes < phase3EndMinutes) return MentorPhase.Rare;
        return MentorPhase.Silent;
    }

    /// <summary>Aceste evenimente sunt unice/rare si nu trebuie blocate de cooldown global.</summary>
    private bool IsOneTimeEvent(MentorEventType eventType)
    {
        switch (eventType)
        {
            case MentorEventType.Welcome:
            case MentorEventType.FirstSupplierOrder:
            case MentorEventType.FleetFull:
            case MentorEventType.LoanTaken:
            case MentorEventType.LoanMissed:
            case MentorEventType.BuildMenuOpened:
            case MentorEventType.MultipleLoans:
                return true;
            default:
                return false;
        }
    }

    private float GetGlobalCooldown(MentorPhase phase)
    {
        switch (phase)
        {
            case MentorPhase.VeryActive: return minCooldownPhase1;
            case MentorPhase.Moderate: return minCooldownPhase2;
            case MentorPhase.Rare: return minCooldownPhase3;
            default: return float.MaxValue;
        }
    }

    private bool IsMessageEligibleForPhase(MessageImportance importance, MentorPhase phase)
    {
        switch (importance)
        {
            case MessageImportance.Critical: return true; // mereu
            case MessageImportance.High:
                return phase == MentorPhase.VeryActive ||
                       phase == MentorPhase.Moderate ||
                       phase == MentorPhase.Rare;
            case MessageImportance.Medium:
                return phase == MentorPhase.VeryActive ||
                       phase == MentorPhase.Moderate;
            case MessageImportance.Low:
                return phase == MentorPhase.VeryActive;
            default: return false;
        }
    }

    // =========================================================================
    // MESSAGE POOL ROUTING
    // =========================================================================

    private List<MentorMessageSO.MentorMessage> GetMessagePool(MentorEventType type)
    {
        switch (type)
        {
            case MentorEventType.Welcome: return messageLibrary.welcomeMessages;
            case MentorEventType.InflationSpike: return messageLibrary.inflationSpikeMessages;
            case MentorEventType.Deflation: return messageLibrary.deflationMessages;
            case MentorEventType.CompetitorCheaper: return messageLibrary.competitorCheaperMessages;
            case MentorEventType.PlayerCheapest: return messageLibrary.playerCheapestMessages;
            case MentorEventType.EmployeeResigned: return messageLibrary.employeeResignedMessages;
            case MentorEventType.EmployeeLowMood: return messageLibrary.employeeLowMoodMessages;
            case MentorEventType.LoanTaken: return messageLibrary.loanTakenMessages;
            case MentorEventType.LoanMissed: return messageLibrary.loanMissedMessages;
            case MentorEventType.SanitaryFine: return messageLibrary.sanitaryFineMessages;
            case MentorEventType.SanitaryPass: return messageLibrary.sanitaryPassMessages;
            case MentorEventType.OutOfStock: return messageLibrary.outOfStockMessages;
            case MentorEventType.ProfitableDay: return messageLibrary.profitableDayMessages;
            case MentorEventType.UnprofitableDay: return messageLibrary.unprofitableDayMessages;
            case MentorEventType.LowFunds: return messageLibrary.lowFundsMessages;
            case MentorEventType.FirstSupplierOrder: return messageLibrary.firstSupplierOrderMessages;
            case MentorEventType.SupplierAngry: return messageLibrary.supplierAngryMessages;
            case MentorEventType.FleetFull: return messageLibrary.fleetFullMessages;
            case MentorEventType.BuildMenuOpened: return messageLibrary.buildMenuOpenedMessages;
            case MentorEventType.PriceBelowCost: return messageLibrary.priceBelowCostMessages;
            case MentorEventType.RepeatedOutOfStock: return messageLibrary.repeatedOutOfStockMessages;
            case MentorEventType.MultipleLoans: return messageLibrary.multipleLoansMessages;
            default: return null;
        }
    }

    // =========================================================================
    // SHOW
    // =========================================================================

    private void ShowMessage(string text, MentorPhase phase, MentorEventType eventType = MentorEventType.Welcome)
    {
        // Adăugăm un indiciu subtil că mentorul se retrage în fazele târzii
        string finalText = text;
        if (phase == MentorPhase.Rare)
        {
            finalText += "\n\n<i><size=10>Aproape că nu mai am nevoie să îți explic — " +
                         "devii independent! 😊</size></i>";
        }

        mentorUI?.ShowMessage(finalText, eventType);
        OnMentorMessage?.Invoke(finalText, null);

        Debug.Log($"[Mentor][{phase}][{eventType}] {text.Substring(0, Mathf.Min(60, text.Length))}...");
    }

    // =========================================================================
    // PUBLIC UTILS
    // =========================================================================

    /// <summary>Returnează cât timp (minute) mai are mentorul activ.</summary>
    public float GetRemainingActiveMinutes()
    {
        float minutesPassed = _sessionTimeSeconds / 60f;
        return Mathf.Max(0f, phase3EndMinutes - minutesPassed);
    }

    public float SessionMinutes => _sessionTimeSeconds / 60f;
}

// ─────────────────────────────────────────────────────────────────────────────

public enum MentorPhase
{
    VeryActive, // 0-10 min
    Moderate,   // 10-20 min
    Rare,       // 20-30 min
    Silent      // 30+ min
}

public enum MentorEventType
{
    Welcome,
    InflationSpike,
    Deflation,
    CompetitorCheaper,
    PlayerCheapest,
    EmployeeResigned,
    EmployeeLowMood,
    LoanTaken,
    LoanMissed,
    SanitaryFine,
    SanitaryPass,
    OutOfStock,
    ProfitableDay,
    UnprofitableDay,
    LowFunds,
    FirstSupplierOrder,
    SupplierAngry,
    FleetFull,
    BuildMenuOpened,
    PriceBelowCost,
    RepeatedOutOfStock,
    MultipleLoans
}