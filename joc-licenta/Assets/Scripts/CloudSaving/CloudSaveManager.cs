using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;

/// <summary>
/// Gestionează sincronizarea stării jocului cu Firebase Firestore.
/// Persistă între scene (DontDestroyOnLoad).
///
/// Flux:
///   GameInitializer.OnUserAuthenticated → SetUserIdAndLoad(userId)
///     → LoadGameStateAsync → publică GameDataLoadedEvent pe EventBus global
///   GameSaveHandler.TriggerSave → publică GameSaveDataEvent
///     → OnSaveGameState → SaveFullGameStateAsync
/// </summary>
public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance { get; private set; }

    // ─── Constante Firestore (un singur loc — nu mai există typo-uri) ─────────

    private const string COLLECTION_USERS = "Users";
    private const string COLLECTION_SAVES = "SaveStates";
    private const string COLLECTION_REPORTS = "FinancialReports";
    private const string DOC_CURRENT_SAVE = "CurrentSave";

    private const string FIELD_DAY = "current_day";
    private const string FIELD_MONEY = "current_money";
    private const string FIELD_LEVEL = "player_level";
    private const string FIELD_LAYOUT = "shop_layout_json";
    private const string FIELD_OBJECTIVES = "objectives_json";
    private const string FIELD_INVENTORY = "inventory_json";
    private const string FIELD_EMPLOYEES = "employees_json";
    private const string FIELD_BANK_LOANS = "bank_loans_json";
    private const string FIELD_SHLVES = "shelves_json";
    private const string FIELD_LAST_SAVED = "last_saved";

    private const string FIELD_RPT_DAY = "day_number";
    private const string FIELD_RPT_REVENUE = "total_revenue";
    private const string FIELD_RPT_COSTS = "fixed_costs";
    private const string FIELD_RPT_FINES = "fines";
    private const string FIELD_RPT_PROFIT = "net_profit";
    private const string FIELD_RPT_TIMESTAMP = "timestamp";

    // ─── State ────────────────────────────────────────────────────────────────

    private IEventBus _eventBus;
    private string _userId;
    private FirebaseFirestore _db;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            _db = FirebaseFirestore.DefaultInstance;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CloudSaveManager] Nu s-a putut inițializa Firestore: {ex.Message}");
            _db = null; // IsReady() va bloca operațiile în siguranță, fără crash
        }
    }

    void OnDestroy()
    {
        if (_eventBus == null) return;
        _eventBus.Unsubscribe<DayEndedEvent>(OnDayEnded);
        _eventBus.Unsubscribe<GameSaveDataEvent>(OnSaveGameState);
    }

    // ─── Inițializare ─────────────────────────────────────────────────────────

    /// <summary>
    /// Apelat de GameInitializer cu EventBus-ul global.
    /// CloudSaveManager se abonează la evenimentele de save.
    /// </summary>
    public void Initialize(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<DayEndedEvent>(OnDayEnded);
        _eventBus.Subscribe<GameSaveDataEvent>(OnSaveGameState);

        Debug.Log("[CloudSaveManager] Inițializat și abonat la EventBus.");
    }

    /// <summary>
    /// Apelat de GameInitializer după autentificare.
    /// Declanșează descărcarea datelor din Firestore.
    /// </summary>
    public void SetUserIdAndLoad(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[CloudSaveManager] SetUserIdAndLoad: userId gol!");
            return;
        }

        _userId = userId;
        Debug.Log($"[CloudSaveManager] Utilizator autentificat: {_userId}. Începem descărcarea...");
        _ = LoadGameStateAsync();
    }

    // ─── Load ─────────────────────────────────────────────────────────────────

    public async Task LoadGameStateAsync()
    {
        if (!IsReady("Load")) return;

        try
        {
            var snapshot = await GetSaveDocRef().GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                Debug.Log("[CloudSaveManager] Nicio salvare găsită. Se pornește un joc nou.");
                return;
            }

            var e = new GameDataLoadedEvent
            {
                CurrentDay = snapshot.GetValue<int>(FIELD_DAY),
                CurrentMoney = snapshot.GetValue<int>(FIELD_MONEY),
                PlayerLevel = ReadIntSafe(snapshot, FIELD_LEVEL, defaultVal: 1),
                ShopLayoutJson = snapshot.GetValue<string>(FIELD_LAYOUT),
                ObjectivesJson = ReadStringSafe(snapshot, FIELD_OBJECTIVES),
                InventoryJson = ReadStringSafe(snapshot, FIELD_INVENTORY),
                EmployeesJson = ReadStringSafe(snapshot, FIELD_EMPLOYEES),
                BankLoansJson = ReadStringSafe(snapshot, FIELD_BANK_LOANS),
                ShelvesJson = ReadStringSafe(snapshot, FIELD_SHLVES),
            };

            Debug.Log($"[CloudSaveManager] Date încărcate — ziua {e.CurrentDay}, {e.CurrentMoney} RON.");
            _eventBus.Publish(e);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CloudSaveManager] Eroare la încărcare: {ex.Message}");
        }
    }

    // ─── Save ─────────────────────────────────────────────────────────────────

    private void OnDayEnded(DayEndedEvent e) => _ = SaveFinancialReportAsync(e);
    private void OnSaveGameState(GameSaveDataEvent e) => _ = SaveFullGameStateAsync(e);

    private async Task SaveFullGameStateAsync(GameSaveDataEvent e)
    {
        if (!IsReady("Save")) return;

        try
        {
            var data = new Dictionary<string, object>
            {
                { FIELD_DAY,        e.CurrentDay },
                { FIELD_MONEY,      e.CurrentMoney },
                { FIELD_LEVEL,      e.PlayerLevel },
                { FIELD_LAYOUT,     e.ShopLayoutJson },
                { FIELD_OBJECTIVES, e.ObjectivesJson  ?? "{}" },
                { FIELD_INVENTORY,  e.InventoryJson   ?? "{}" },
                { FIELD_EMPLOYEES,  e.EmployeesJson   ?? "{}" },
                { FIELD_BANK_LOANS, e.BankLoansJson   ?? "{}" },
                { FIELD_SHLVES,     e.ShelvesJson     ?? "{}" },
                { FIELD_LAST_SAVED, DateTime.UtcNow.ToString("o") },
            };

            await GetSaveDocRef().SetAsync(data);
            Debug.Log($"[CloudSaveManager] Starea jocului salvată pentru: {_userId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CloudSaveManager] Eroare la salvare: {ex.Message}");
        }
    }

    private async Task SaveFinancialReportAsync(DayEndedEvent e)
    {
        if (!IsReady("Raport financiar")) return;

        try
        {
            var data = new Dictionary<string, object>
            {
                { FIELD_RPT_DAY,       e.DayNumber },
                { FIELD_RPT_REVENUE,   e.TotalRevenue },
                { FIELD_RPT_COSTS,     e.FixedCosts },
                { FIELD_RPT_FINES,     e.Fines },
                { FIELD_RPT_PROFIT,    e.NetProfit },
                { FIELD_RPT_TIMESTAMP, DateTime.UtcNow.ToString("o") },
            };

            await _db.Collection(COLLECTION_USERS)
                     .Document(_userId)
                     .Collection(COLLECTION_REPORTS)
                     .Document($"Day_{e.DayNumber}")
                     .SetAsync(data);

            Debug.Log($"[CloudSaveManager] Raport ziua {e.DayNumber} salvat.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CloudSaveManager] Eroare la salvarea raportului: {ex.Message}");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private DocumentReference GetSaveDocRef()
        => _db.Collection(COLLECTION_USERS)
              .Document(_userId)
              .Collection(COLLECTION_SAVES)
              .Document(DOC_CURRENT_SAVE);

    private bool IsReady(string operation)
    {
        if (_db != null && !string.IsNullOrEmpty(_userId)) return true;

        Debug.LogError($"[CloudSaveManager] {operation} anulat — " +
                       (_db == null ? "Firebase neconectat. " : "") +
                       (string.IsNullOrEmpty(_userId) ? "Utilizator neautentificat." : ""));
        return false;
    }

    private static int ReadIntSafe(DocumentSnapshot snap, string field, int defaultVal = 0)
        => snap.ContainsField(field) ? (int)(long)snap.GetValue<object>(field) : defaultVal;

    private static string ReadStringSafe(DocumentSnapshot snap, string field)
        => snap.ContainsField(field) ? snap.GetValue<string>(field) : "{}";
}