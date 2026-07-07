using UnityEngine;
using System.Collections.Generic;

public class EmployeeManager : MonoBehaviour
{
    #region Singleton
    public static EmployeeManager Instance { get; private set; }
    #endregion

    #region Configuration
    [Header("Settings")]
    [SerializeField] private List<GameObject> malePrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> femalePrefabs = new List<GameObject>();
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int maxEmployees = 10;
    #endregion

    #region Private Fields
    private List<Employee> allEmployees = new List<Employee>();
    public WorkStationRegistry StationRegistry { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        StationRegistry = new WorkStationRegistry();
        StationRegistry.RefreshAllStations();
    }

    private void Start()
    {
        RegisterTimeManagerEvents();
        StationRegistry.RefreshAllStations();
    }

    private void OnDestroy()
    {
        UnregisterTimeManagerEvents();
    }
    #endregion

    #region Time Manager Integration
    private void RegisterTimeManagerEvents()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnShopOpen += StartWorkDay;
        }
    }

    private void UnregisterTimeManagerEvents()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnShopOpen -= StartWorkDay;
        }
    }
    #endregion

    #region Public Methods - Station Management
    public void FindAllWorkStations()
    {
        StationRegistry.RefreshAllStations();
    }

    public void SetMaxEmployees(int newLimit)
    {
        maxEmployees = newLimit;
        Debug.Log($"[EmployeeManager] Limita maximă de angajați a fost actualizată la {maxEmployees}.");
    }

    public void RefreshStations()
    {
        StationRegistry.RefreshAllStations();

        // Reassign stations to employees without one
        foreach (var emp in allEmployees)
        {
            if (emp.myWorkStation == null)
            {
                AssignStationToEmployee(emp);
            }
        }
    }

    // =========================================================================
    // SALVARE ȘI ÎNCĂRCARE ANGAJAȚI
    // =========================================================================

    public string GenerateSaveJson()
    {
        EmployeesSaveState saveState = new EmployeesSaveState();

        foreach (var emp in allEmployees)
        {
            saveState.HiredEmployees.Add(new EmployeeSaveData
            {
                Name = emp.employeeName,
                Role = emp.role,
                // Notă: Dacă în scriptul Employee.cs ai adăugat public EmployeeGender gender,
                // poți pune aici emp.gender. Momentan, salvăm un default pentru a preveni erorile.
                Gender = emp.gender
            });
        }

        return JsonUtility.ToJson(saveState);
    }
    public void RestoreFromSave(string json)
    {
        Debug.Log($"--- [LOAD EMPLOYEES 1] Începem restaurarea. JSON: {json} ---");
        if (string.IsNullOrEmpty(json) || json == "{}") return;

        try
        {
            EmployeesSaveState saveState = JsonUtility.FromJson<EmployeesSaveState>(json);
            Debug.Log($"[LOAD EMPLOYEES 2] Deserializare reușită! Găsiți {saveState.HiredEmployees.Count} angajați în memorie.");

            // Curățenie
            for (int i = allEmployees.Count - 1; i >= 0; i--)
            {
                if (allEmployees[i] != null) { Destroy(allEmployees[i].gameObject); }
            }
            allEmployees.Clear();

            // Angajare
            foreach (var savedEmp in saveState.HiredEmployees)
            {
                Debug.Log($"[LOAD EMPLOYEES 3] Încercăm angajarea: {savedEmp.Name} (Rol: {savedEmp.Role})...");

                int currentMax = maxEmployees;
                if (allEmployees.Count >= maxEmployees) maxEmployees = allEmployees.Count + 1;

                Employee newEmp = HireEmployee(savedEmp.Name, savedEmp.Gender);

                if (newEmp != null)
                {
                    Debug.Log($"[LOAD EMPLOYEES 4] {savedEmp.Name} instanțiat. Asignăm rolul...");
                    ChangeEmployeeRole(newEmp, savedEmp.Role);

                    // NOU: Forțăm refacerea statisticilor salvate
                    // (dacă le ai setate public în scriptul Employee)
                    // newEmp.CurrentMood = savedEmp.CurrentMood; 
                }
                else
                {
                    Debug.LogError($"[LOAD EMPLOYEES EROARE] HireEmployee a returnat NULL pentru {savedEmp.Name}!");
                }

                maxEmployees = currentMax;
            }

            Debug.Log($"--- [LOAD EMPLOYEES 5] SUCCES! Echipa a fost reîncărcată fizic! ---");
            RefreshStations();
            WorkStationRegistry.Instance.ResetAssignments();
        }
        catch (System.Exception ex)
        {
            // Prindem absolut orice eroare de conversie
            Debug.LogError($"[LOAD EMPLOYEES EROARE CRITICĂ] {ex.Message} \n {ex.StackTrace}");
        }
    }
    #endregion

    #region Public Methods - Employee Management
    public Employee HireEmployee(string name, EmployeeGender gender)
    {
        if (!CanHireMoreEmployees())
        {
            Debug.LogWarning($"[EmployeeManager] Cannot hire {name} - max employees reached ({maxEmployees})");
            return null;
        }

        Employee newEmployee = CreateEmployee(name, gender);

        if (newEmployee != null)
        {
            allEmployees.Add(newEmployee);
            HandleNewEmployeeShift(newEmployee);
        }

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new ScoreGainedEvent { Amount = 20, Source = "Angajat Nou" });
        }

        return newEmployee;
    }

    public void FireEmployee(Employee employee)
    {
        if (employee == null || !allEmployees.Contains(employee))
        {
            Debug.LogWarning("[EmployeeManager] Attempted to fire employee that doesn't exist");
            return;
        }

        allEmployees.Remove(employee);
        Destroy(employee.gameObject);
        Debug.Log($"[EmployeeManager] {employee.employeeName} has been fired.");
    }

    public void ChangeEmployeeRole(Employee emp, EmployeeRole newRole)
    {
        if (emp == null)
        {
            Debug.LogWarning("[EmployeeManager] Cannot change role of null employee");
            return;
        }

        // Reset current assignment
        emp.role = newRole;
        emp.myWorkStation = null;
        emp.secondaryTarget = null;

        // Assign new station based on new role
        AssignStationToEmployee(emp);

        Debug.Log($"[EmployeeManager] Role changed for {emp.employeeName}: {newRole}");
    }
    #endregion

    #region Private Methods - Employee Creation
    private bool CanHireMoreEmployees()
    {
        return allEmployees.Count < maxEmployees;
    }

    private Employee CreateEmployee(string name, EmployeeGender gender)
    {
        // 1. Alegem lista corectă de prefabs
        List<GameObject> targetList = (gender == EmployeeGender.Male) ? malePrefabs : femalePrefabs;

        // Siguranță: Verificăm dacă ai pus modele în Inspector
        if (targetList == null || targetList.Count == 0)
        {
            Debug.LogError($"[EmployeeManager] Nu ai setat niciun prefab pentru genul {gender} în Inspector!");
            return null;
        }

        // 2. Alegem un model la întâmplare din acea listă (dacă ai mai multe variante de tricouri, fețe etc.)
        GameObject selectedPrefab = targetList[Random.Range(0, targetList.Count)];

        // 3. Instanțiem
        GameObject newObj = Instantiate(selectedPrefab, spawnPoint.position, Quaternion.identity);
        newObj.name = $"Employee_{name}";

        Employee script = newObj.GetComponent<Employee>();
        if (script != null)
        {
            script.employeeName = name;
            script.role = EmployeeRole.None;
            // Opțional, poți salva și genul în scriptul Employee dacă vrei să-l folosești mai târziu
            script.gender = gender;
        }
        else
        {
            Debug.LogError("[EmployeeManager] Employee prefab missing Employee component!");
            Destroy(newObj);
            return null;
        }

        return script;
    }

    private void HandleNewEmployeeShift(Employee employee)
    {
        if (IsShopCurrentlyOpen())
        {
            employee.StartShift(spawnPoint.position);
            Debug.Log($"[EmployeeManager] {employee.employeeName} hired during work hours - starting shift immediately!");
        }
        else
        {
            employee.gameObject.SetActive(false);
            Debug.Log($"[EmployeeManager] {employee.employeeName} hired - waiting for shop to open.");
        }
    }

    private bool IsShopCurrentlyOpen()
    {
        if (TimeManager.Instance == null) return false;

        int currentHour = TimeManager.Instance.CurrentHour;
        int openHour = TimeManager.Instance.openHour;
        int closeHour = TimeManager.Instance.closeHour;

        return currentHour >= openHour && currentHour < closeHour;
    }
    #endregion

    #region Private Methods - Station Assignment
    private void AssignStationToEmployee(Employee employee)
    {
        switch (employee.role)
        {
            case EmployeeRole.Cashier:
                AssignCashierStation(employee);
                break;

            case EmployeeRole.Restocker:
                AssignRestockerStation(employee);
                break;

            case EmployeeRole.Janitor:
                Debug.Log($"[EmployeeManager] {employee.employeeName} assigned as Janitor (no station needed)");
                ContextualObjectiveSystem.Instance?.NotifyEmployeeHired();
                break;

            default:
                Debug.LogWarning($"[EmployeeManager] {employee.employeeName} has no defined role.");
                break;
        }
    }

    private void AssignCashierStation(Employee employee)
    {
        // Cauta o casa fara casier asignat
        var allQueues = FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None);

        CashRegisterQueue freeRegister = null;

        foreach (var queue in allQueues)
        {
            // ELIMINAT: Verificarea de activeInHierarchy. 
            // Acum verificăm STRICT dacă locul este complet gol.
            if (queue.AssignedCashier == null)
            {
                freeRegister = queue;
                break;
            }
        }

        if (freeRegister == null)
        {
            Debug.LogWarning($"[EmployeeManager] Nu există case libere pentru {employee.employeeName}!");
            return;
        }

        WorkStation station = freeRegister.GetComponent<WorkStation>();
        Transform targetPos = station.interactionPoint != null
            ? station.interactionPoint
            : station.transform;

        employee.AssignRole(EmployeeRole.Cashier, targetPos);
        freeRegister.AssignCashier(employee);

        Debug.Log($"[EmployeeManager] {employee.employeeName} asignat la casa {freeRegister.name}");

        // Pas 2 din obiectivul "Casa de Marcat"
        ContextualObjectiveSystem.Instance?.NotifyCashierHired();
    }

    private void AssignRestockerStation(Employee employee)
    {
        // Căutăm direct StorageRacks în scenă în loc de WorkStation cu tip Storage
        StorageRacks[] allRacks = Object.FindObjectsByType<StorageRacks>(FindObjectsSortMode.None);

        // Notificăm obiectivul indiferent dacă există rack sau nu
        // (angajatul a fost angajat, chiar dacă nu are stație momentan)
        ContextualObjectiveSystem.Instance?.NotifyEmployeeHired();

        if (allRacks.Length == 0)
        {
            Debug.LogWarning($"[EmployeeManager] Nu există StorageRacks în scenă pentru {employee.employeeName}!");
            return;
        }

        StorageRacks targetRack = allRacks[0];
        employee.myWorkStation = targetRack.transform;
        employee.AssignRole(EmployeeRole.Restocker, targetRack.transform);
        employee.secondaryTarget = null;

        Debug.Log($"[EmployeeManager] {employee.employeeName} asignat la StorageRacks: {targetRack.name}");
    }
    #endregion

    #region Private Methods - Shift Management
    private void StartWorkDay()
    {
        Debug.Log("[EmployeeManager] Starting work day for all employees");
        foreach (var emp in allEmployees)
        {
            emp.StartShift(spawnPoint.position);
        }
    }

    public void EndWorkDay()
    {
        Debug.Log("[EmployeeManager] Ending work day for all employees");

        // Folosim un FOR INVERS pentru a preveni crash-urile dacă cineva își dă demisia
        for (int i = allEmployees.Count - 1; i >= 0; i--)
        {
            if (allEmployees[i] != null)
            {
                allEmployees[i].EndShift();
            }
        }
    }
    #endregion

    #region Public Accessors
    public int CurrentEmployeeCount => allEmployees.Count;
    public int MaxEmployeeCount => maxEmployees;
    public List<Employee> AllEmployees => new List<Employee>(allEmployees); // Return copy for safety
    #endregion
}

public enum EmployeeGender
{
    Male,
    Female
}