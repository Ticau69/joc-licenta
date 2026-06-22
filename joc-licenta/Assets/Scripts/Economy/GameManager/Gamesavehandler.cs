using UnityEngine;

/// <summary>
/// Gestionează salvarea și încărcarea datelor de joc.
/// Adăugat dinamic de GameManager în Start.
/// </summary>
public class GameSaveHandler : MonoBehaviour
{
    private IMoneyService _money;
    private IEventBus _eventBus;

    private string _pendingInventory;
    private string _pendingEmployees;
    private string _pendingShelves;

    public void Initialize(IMoneyService money, IEventBus eventBus)
    {
        _money = money;
        _eventBus = eventBus;

        _eventBus.Subscribe<GameDataLoadedEvent>(OnGameDataLoaded);
    }

    // ─── Load ─────────────────────────────────────────────────────────────────

    private void OnGameDataLoaded(GameDataLoadedEvent e)
    {
        Debug.Log($"[GameSaveHandler] Date primite — buget: {e.CurrentMoney} RON, ziua: {e.CurrentDay}");

        // 1. Aplicăm lucrurile care NU depind de fizica magazinului
        ApplyMoney(e.CurrentMoney);
        ApplyObjectives(e.ObjectivesJson);
        TimeManager.Instance.SetDay(e.CurrentDay);
        PlayerXPManager.Instance.SetLevel(e.PlayerLevel);

        if (BankManager.Instance != null)
            BankManager.Instance.RestoreFromSave(e.BankLoansJson);

        // 2. Memoram JSON-urile pentru mai târziu
        _pendingInventory = e.InventoryJson;
        _pendingEmployees = e.EmployeesJson;
        _pendingShelves = e.ShelvesJson;

        // 3. Pornim construcția magazinului (și corutina de 0.5s)
        ApplyLayout(e.ShopLayoutJson);
    }

    private System.Collections.IEnumerator DelayedLoad(float delay)
    {
        yield return new WaitForSeconds(delay);

        EmployeeManager.Instance?.RefreshStations();
        if (ServiceLocator.Instance.TryGet(out IInventoryService inv))
            ((InventoryService)inv).ForceRefreshCache();

        // 1. APLICĂM RAFTURILE PRIMELE (ca angajații și inventarul să le "vadă" pline)
        ApplyShelves(_pendingShelves);

        // 2. Apoi aplicăm restul
        ApplyInventory(_pendingInventory);
        ApplyEmployees(_pendingEmployees);

        if (_eventBus != null)
        {
            _eventBus.Publish(new GameUIRefreshEvent());
            Debug.Log("[GameSaveHandler] Restaurare completă. Am trimis GameUIRefreshEvent către UI.");
        }

        Debug.Log("[GameSaveHandler] Layout aplicat, rafturi populate, restaurare finalizată!");
    }

    private void ApplyShelves(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "{}") return;

        ShelvesSaveState saveState = JsonUtility.FromJson<ShelvesSaveState>(json);
        var currentShelvesInScene = FindObjectsByType<WorkStation>(FindObjectsSortMode.None);

        int matchedCount = 0;

        foreach (var savedShelf in saveState.ActiveShelves)
        {
            // Căutăm raftul fizic care a fost instanțiat fix la coordonatele salvate
            foreach (var physicalShelf in currentShelvesInScene)
            {
                if (physicalShelf.stationType == StationType.Shelf &&
                    physicalShelf.transform.position.ToString() == savedShelf.PositionXYZ)
                {
                    // "BINGO!" - Am găsit mobila potrivită. Îi dăm datele înapoi.
                    physicalShelf.slotProduct = savedShelf.ConfiguredProduct;
                    physicalShelf.pendingProduct = savedShelf.PendingProduct;

                    // Folosim funcția existentă ca să actualizeze și vizualurile
                    physicalShelf.AddProduct(savedShelf.CurrentStock);

                    matchedCount++;
                    break;
                }
            }
        }
        Debug.Log($"[GameSaveHandler] {matchedCount} rafturi au fost re-populate cu marfă!");
    }

    private void ApplyMoney(int amount)
    {
        if (_money == null) return;
        _money.TrySpend(_money.CurrentAmount); // resetăm la 0
        _money.SetMoney(amount);
        (_money as MoneyManager)?.UpdateUI();
    }

    private void ApplyLayout(string json)
    {
        var ps = FindFirstObjectByType<PlacementSystem>();
        if (ps != null)
        {
            ps.ReconstructShop(json);
            StartCoroutine(DelayedLoad(0.5f)); // Pauza
        }
    }

    private void ApplyObjectives(string json)
    {
        ContextualObjectiveSystem.Instance?.RestoreFromSave(json);
    }

    private void ApplyInventory(string json)
    {
        if (ServiceLocator.Instance.TryGet(out IInventoryService inv))
            ((InventoryService)inv).RestoreFromSave(json);
    }

    private void ApplyEmployees(string json)
    {
        if (EmployeeManager.Instance != null)
            EmployeeManager.Instance.RestoreFromSave(json);
        else
            Debug.LogWarning("[GameSaveHandler] EmployeeManager negăsit — angajații nu au putut fi restaurați.");
    }

    // ─── SALVARE RAFTURI ──────────────────────────────────────────

    private string CollectShelves()
    {
        ShelvesSaveState saveState = new ShelvesSaveState();

        // Găsim toate rafturile de tip "Shelf" din scenă
        var allShelves = FindObjectsByType<WorkStation>(FindObjectsSortMode.None);

        foreach (var shelf in allShelves)
        {
            if (shelf.stationType == StationType.Shelf)
            {
                saveState.ActiveShelves.Add(new ShelfSaveData
                {
                    // Convertim poziția exactă la string (ex: "10.0, 0.0, -5.0")
                    PositionXYZ = shelf.transform.position.ToString(),
                    ConfiguredProduct = shelf.slotProduct,
                    PendingProduct = shelf.pendingProduct,
                    CurrentStock = shelf.slotStock
                });
            }
        }

        return JsonUtility.ToJson(saveState);
    }

    // ─── Save ─────────────────────────────────────────────────────────────────

    public void TriggerSave()
    {
        Debug.Log("[GameSaveHandler] Salvare inițiată.");

        var saveEvent = new GameSaveDataEvent
        {
            CurrentDay = TimeManager.Instance.CurrentDay,
            CurrentMoney = _money?.CurrentAmount ?? 0,
            PlayerLevel = PlayerXPManager.Instance.Level,
            ShopLayoutJson = CollectLayout(),
            ObjectivesJson = CollectObjectives(),
            InventoryJson = CollectInventory(),
            EmployeesJson = CollectEmployees(),
            ShelvesJson = CollectShelves(),
            BankLoansJson = BankManager.Instance != null ? BankManager.Instance.GenerateSaveJson() : "{}"
        };

        if (_eventBus != null)
        {
            Debug.Log($"[GameSaveHandler] Publicăm evenimentul de salvare. Layout: {saveEvent.ShopLayoutJson.Length} chars.");
            _eventBus.Publish(saveEvent);
        }
        else
        {
            Debug.LogError("[GameSaveHandler] EventBus este null — salvarea a eșuat.");
        }
    }

    private string CollectLayout()
    {
        var ps = FindFirstObjectByType<PlacementSystem>();
        if (ps != null) return ps.GenerateShopLayoutJson();

        Debug.LogError("[GameSaveHandler] PlacementSystem negăsit — layout JSON va fi gol.");
        return "{}";
    }

    private string CollectObjectives()
        => ContextualObjectiveSystem.Instance?.GenerateSaveJson() ?? "{}";

    private string CollectInventory()
    {
        if (ServiceLocator.Instance.TryGet(out IInventoryService inv))
            return ((InventoryService)inv).GenerateSaveJson();
        return "{}";
    }

    private string CollectEmployees()
        => EmployeeManager.Instance?.GenerateSaveJson() ?? "{}";

    // ─── Cleanup ──────────────────────────────────────────────────────────────

    void OnDestroy()
    {
        if (_eventBus == null) return;
        _eventBus.Unsubscribe<GameDataLoadedEvent>(OnGameDataLoaded);
    }
}