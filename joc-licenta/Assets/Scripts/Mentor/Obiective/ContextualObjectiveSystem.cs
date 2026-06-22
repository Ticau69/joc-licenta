using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Gestionează obiectivele contextuale ale jucătorului.
/// Obiectivele apar organic pe parcursul jocului, fără tutorial forțat.
/// </summary>
public class ContextualObjectiveSystem : MonoBehaviour
{
    public static ContextualObjectiveSystem Instance { get; private set; }

    // =========================================================================
    // EVENTS
    // =========================================================================

    public event Action<Objective> OnObjectiveUnlocked;
    public event Action<Objective> OnObjectiveCompleted;
    public event Action<Objective> OnObjectiveProgressChanged;

    // =========================================================================
    // STATE
    // =========================================================================

    private readonly List<Objective> _allObjectives = new();
    private readonly List<Objective> _activeObjectives = new();   // max 3 vizibile
    private readonly List<Objective> _completedObjectives = new();

    public IReadOnlyList<Objective> ActiveObjectives => _activeObjectives;
    public IReadOnlyList<Objective> CompletedObjectives => _completedObjectives;

    private IEventBus _eventBus;

    // Tracking intern
    private int _consecutiveProfitDays = 0;
    private bool _lastDayWasProfitable = false;
    private bool _eventsSubscribed = false;

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
        BuildObjectives();
        SubscribeToEvents();

        // Activăm primele obiective
        TryActivateNextObjectives();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // =========================================================================
    // DEFINIREA OBIECTIVELOR
    // =========================================================================

    private void BuildObjectives()
    {
        _allObjectives.AddRange(new[]
        {
            // ── ZIUA 1 ────────────────────────────────────────────────────────
            new Objective(
                id: "place_shelf",
                title: "Primul Raft",
                description: "Construiește cel puțin un raft în magazin",
                icon: "🏪",
                targetDay: 1,
                maxProgress: 1
            ),
            new Objective(
                id: "place_cashier",
                title: "Casa de Marcat",
                description: "Plasează o casă de marcat și angajează un casier",
                icon: "💰",
                targetDay: 1,
                maxProgress: 2  // Pas 1: plasare casă, Pas 2: angajare casier
            ),

            // ── ZIUA 2-3 ─────────────────────────────────────────────────────
            new Objective(
                id: "first_supplier_order",
                title: "Prima Comandă",
                description: "Plasează o comandă la un furnizor",
                icon: "🚚",
                targetDay: 2,
                maxProgress: 1
            ),
            new Objective(
                id: "hire_employee",
                title: "Primul Angajat",
                description: "Angajează un Restocker sau Janitor",
                icon: "👷",
                targetDay: 2,
                maxProgress: 1
            ),

            // ── ZIUA 4-5 ─────────────────────────────────────────────────────
            new Objective(
                id: "profit_streak",
                title: "Pe Profit",
                description: "Menține profitul pozitiv 2 zile consecutive",
                icon: "📈",
                targetDay: 4,
                maxProgress: 2
            ),
            new Objective(
                id: "stock_3_products",
                title: "Diversifică Stocul",
                description: "Ai stoc din cel puțin 3 produse diferite simultan",
                icon: "📦",
                targetDay: 4,
                maxProgress: 3
            ),

            // ── ZIUA 7+ ──────────────────────────────────────────────────────
            new Objective(
                id: "upgrade_fleet",
                title: "Extinde Flota",
                description: "Fă upgrade la flotă pentru a primi mai multe comenzi",
                icon: "🚛",
                targetDay: 7,
                maxProgress: 1
            ),
            new Objective(
                id: "reach_level2",
                title: "Nivelul 2",
                description: "Atinge nivelul 2 de experiență",
                icon: "⭐",
                targetDay: 5,
                maxProgress: 1
            ),
        });
    }

    // =========================================================================
    // ABONARE EVENIMENTE
    // =========================================================================

    private void SubscribeToEvents()
    {
        if (_eventsSubscribed) return;
        _eventsSubscribed = true;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += OnDayChanged;

        if (SupplierOrderSystem.Instance != null)
            SupplierOrderSystem.Instance.OnOrderPlaced += OnSupplierOrderPlaced;

        if (PlayerXPManager.Instance != null)
            PlayerXPManager.Instance.OnLevelChanged += OnLevelChanged;

        if (ServiceLocator.Instance != null &&
            ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Subscribe<StockChangedEvent>(OnStockChanged);
        }

        // Fleet upgrade
        var fleet = FindFirstObjectByType<FleetManager>();
        if (fleet != null)
            fleet.OnFleetStatusChanged += OnFleetStatusChanged;
    }

    private void UnsubscribeFromEvents()
    {
        if (!_eventsSubscribed) return;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= OnDayChanged;

        if (SupplierOrderSystem.Instance != null)
            SupplierOrderSystem.Instance.OnOrderPlaced -= OnSupplierOrderPlaced;

        if (PlayerXPManager.Instance != null)
            PlayerXPManager.Instance.OnLevelChanged -= OnLevelChanged;

        if (ServiceLocator.Instance != null &&
            ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Unsubscribe<StockChangedEvent>(OnStockChanged);
        }
    }

    // =========================================================================
    // NOU: SISTEM DE SALVARE / ÎNCĂRCARE
    // =========================================================================

    public string GenerateSaveJson()
    {
        ObjectivesSaveState saveState = new ObjectivesSaveState();

        foreach (var obj in _allObjectives)
        {
            saveState.SavedObjectives.Add(new ObjectiveSaveData
            {
                Id = obj.Id,
                CurrentProgress = obj.CurrentProgress,
                IsUnlocked = obj.IsUnlocked,
                IsCompleted = obj.IsCompleted
            });
        }

        return JsonUtility.ToJson(saveState);
    }

    public void RestoreFromSave(string json)
    {
        Debug.Log($"--- [LOAD OBJECTIVES 1] Se începe restaurarea. JSON primit: {json} ---");

        // 1. Verificăm dacă primim date valide
        if (string.IsNullOrEmpty(json) || json == "{}" || json == "null")
        {
            Debug.LogWarning("[LOAD OBJECTIVES] Abort: JSON-ul primit este gol! Verifică CloudSaveManager dacă citește corect câmpul 'objectives_json'.");
            return;
        }

        try
        {
            // 2. Deserializarea
            ObjectivesSaveState saveState = JsonUtility.FromJson<ObjectivesSaveState>(json);
            Debug.Log($"[LOAD OBJECTIVES 2] Deserializare perfectă! Am găsit {saveState.SavedObjectives.Count} obiective în memoria salvată.");

            // Curățăm listele interne actuale pentru a face loc celor din cloud
            _activeObjectives.Clear();
            _completedObjectives.Clear();

            // 3. Procesarea fiecărui obiectiv
            foreach (var savedData in saveState.SavedObjectives)
            {
                Objective realObj = GetObjective(savedData.Id);
                if (realObj != null)
                {
                    // Suprascriem starea
                    realObj.CurrentProgress = savedData.CurrentProgress;
                    realObj.IsUnlocked = savedData.IsUnlocked;
                    realObj.IsCompleted = savedData.IsCompleted;

                    // Repartizăm în liste și FORȚĂM actualizarea interfeței (UI)
                    if (realObj.IsCompleted)
                    {
                        _completedObjectives.Add(realObj);

                        // Dacă UI-ul avea obiectivul pe ecran, îi dăm semnal să îl bifeze/șteargă
                        OnObjectiveCompleted?.Invoke(realObj);
                    }
                    else if (realObj.IsUnlocked)
                    {
                        _activeObjectives.Add(realObj);

                        // Forțăm UI-ul să deseneze obiectivul și să aplice bara de progres descărcată
                        OnObjectiveUnlocked?.Invoke(realObj);
                        OnObjectiveProgressChanged?.Invoke(realObj);
                    }
                }
            }

            // 4. Verificăm dacă, prin completarea celor vechi, am deblocat spațiu pentru altele noi
            TryActivateNextObjectives();

            Debug.Log($"--- [LOAD OBJECTIVES 3] SUCCES TOTAL! Memoria restaurată. Active: {_activeObjectives.Count}, Completate: {_completedObjectives.Count} ---");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOAD OBJECTIVES EROARE CRITICĂ] Textul JSON nu a putut fi transformat: {ex.Message}");
        }
    }

    // =========================================================================
    // HANDLERS
    // =========================================================================

    private void OnDayChanged()
    {
        int today = TimeManager.Instance.CurrentDay;

        // Activăm obiective care corespund zilei curente
        TryActivateNextObjectives();

        // Check profit streak
        CheckProfitStreak();
    }

    private void OnSupplierOrderPlaced(SupplierDeliveryOrder order)
    {
        CompleteObjective("first_supplier_order");
    }

    private void OnLevelChanged(int oldLevel, int newLevel)
    {
        if (newLevel >= 2)
            CompleteObjective("reach_level2");
    }

    private void OnStockChanged(StockChangedEvent evt)
    {
        // Verificăm câte produse diferite au stoc > 0
        CheckProductDiversity();


    }

    private void OnFleetStatusChanged()
    {
        // Fleet upgrade = CurrentMaxTrucks a crescut
        var fleet = FindFirstObjectByType<FleetManager>();
        if (fleet != null && fleet.CurrentMaxTrucks > 2) // peste capacitatea inițială
            CompleteObjective("upgrade_fleet");
    }

    // =========================================================================
    // NOTIFICĂRI EXTERNE (apelate din alte sisteme)
    // =========================================================================

    /// <summary>Apelat când se plasează un raft (Shelf).</summary>
    public void NotifyShelfPlaced()
    {
        CompleteObjective("place_shelf");
    }

    /// <summary>Apelat când se plasează o casă de marcat.</summary>
    public void NotifyCashRegisterPlaced()
    {
        UpdateProgress("place_cashier", 1); // Pas 1 din 2
    }

    /// <summary>Apelat când se angajează un casier.</summary>
    public void NotifyCashierHired()
    {
        // Pas 2 din 2 — doar dacă casa a fost deja plasată (progress >= 1)
        var obj = GetObjective("place_cashier");
        if (obj != null && obj.CurrentProgress >= 1)
            CompleteObjective("place_cashier");
    }

    /// <summary>Apelat când se angajează primul angajat non-casier.</summary>
    public void NotifyEmployeeHired()
    {
        CompleteObjective("hire_employee");
    }

    /// <summary>Apelat din FinanceManager la end-of-day cu profitul zilei.</summary>
    public void NotifyDayProfit(int profit)
    {
        var obj = GetObjective("profit_streak");
        if (obj == null || obj.IsCompleted) return;

        if (profit > 0)
        {
            _consecutiveProfitDays++;
            UpdateProgress("profit_streak", _consecutiveProfitDays);

            if (_consecutiveProfitDays >= 2)
                CompleteObjective("profit_streak");
        }
        else
        {
            // Resetăm streak-ul
            _consecutiveProfitDays = 0;
            UpdateProgress("profit_streak", 0);
        }
    }

    // =========================================================================
    // LOGICĂ INTERNĂ
    // =========================================================================

    private void CheckProductDiversity()
    {
        if (!ServiceLocator.Instance.TryGet(out IInventoryService inventory)) return;
        if (!ServiceLocator.Instance.TryGet(out IEconomyService economy)) return;

        int productsWithStock = 0;
        foreach (ProductType type in System.Enum.GetValues(typeof(ProductType)))
        {
            if (type == ProductType.None) continue;
            if (inventory.GetStock(type) > 0)
                productsWithStock++;
        }

        UpdateProgress("stock_3_products", Mathf.Min(productsWithStock, 3));

        if (productsWithStock >= 3)
            CompleteObjective("stock_3_products");
    }

    private void CheckProfitStreak()
    {
        // Dacă FinanceManager e disponibil, verificăm profitul zilei anterioare
        if (FinanceManager.Instance == null) return;

        int todayProfit = FinanceManager.Instance.GetTodayProfit();
        NotifyDayProfit(todayProfit);
    }

    private void TryActivateNextObjectives()
    {
        int today = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        foreach (var obj in _allObjectives)
        {
            if (obj.IsUnlocked || obj.IsCompleted) continue;
            if (obj.TargetDay > today) continue;
            if (_activeObjectives.Count >= 3) break;

            obj.IsUnlocked = true;
            _activeObjectives.Add(obj);
            OnObjectiveUnlocked?.Invoke(obj);

            Debug.Log($"[Objectives] Obiectiv nou: {obj.Title}");
        }
    }

    private void CompleteObjective(string id)
    {
        var obj = GetObjective(id);
        if (obj == null || obj.IsCompleted || !obj.IsUnlocked) return;

        obj.IsCompleted = true;
        obj.CurrentProgress = obj.MaxProgress;

        _activeObjectives.Remove(obj);
        _completedObjectives.Add(obj);

        OnObjectiveCompleted?.Invoke(obj);

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new ScoreGainedEvent { Amount = 50, Source = "Obiectiv Completat" });
        }

        Debug.Log($"[Objectives] ✅ Completat: {obj.Title}");

        // Activăm imediat obiectivul următor dacă e loc
        TryActivateNextObjectives();
    }

    private void UpdateProgress(string id, int progress)
    {
        var obj = GetObjective(id);
        if (obj == null || obj.IsCompleted) return;

        if (obj.CurrentProgress == progress) return;
        obj.CurrentProgress = Mathf.Clamp(progress, 0, obj.MaxProgress);
        OnObjectiveProgressChanged?.Invoke(obj);
    }

    private Objective GetObjective(string id)
    {
        return _allObjectives.Find(o => o.Id == id);
    }

    // =========================================================================
    // UTILITAR
    // =========================================================================

    public int GetCompletedCount() => _completedObjectives.Count;
    public int GetTotalCount() => _allObjectives.Count;
}

// =============================================================================
// DATA CLASS
// =============================================================================

[Serializable]
public class Objective
{
    public string Id;
    public string Title;
    public string Description;
    public string Icon;
    public int TargetDay;      // De la ce zi devine activ
    public int MaxProgress;
    public int CurrentProgress;
    public bool IsUnlocked;
    public bool IsCompleted;

    public Objective(string id, string title, string description,
                     string icon, int targetDay, int maxProgress)
    {
        Id = id;
        Title = title;
        Description = description;
        Icon = icon;
        TargetDay = targetDay;
        MaxProgress = maxProgress;
        CurrentProgress = 0;
        IsUnlocked = false;
        IsCompleted = false;
    }

    public bool HasProgress => MaxProgress > 1;
    public string ProgressText => $"{CurrentProgress}/{MaxProgress}";
    public float ProgressFraction => MaxProgress > 0 ? (float)CurrentProgress / MaxProgress : 0f;
}