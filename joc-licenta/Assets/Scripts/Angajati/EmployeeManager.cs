using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Necesar pentru a căuta liste

public class EmployeeManager : MonoBehaviour
{
    public static EmployeeManager Instance { get; private set; }

    [Header("Setări")]
    [SerializeField] private GameObject employeePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int maxEmployees = 10;

    private List<Employee> allEmployees = new List<Employee>();

    // Liste cu punctele de lucru din scenă
    private List<WorkStation> cashRegisters = new List<WorkStation>();
    private List<WorkStation> storages = new List<WorkStation>();
    private List<WorkStation> shelves = new List<WorkStation>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnShopOpen += StartWorkDay;
            TimeManager.Instance.OnShopClose += EndWorkDay;
        }

        // Găsim toate stațiile de lucru din scenă la început
        FindAllWorkStations();
    }

    public void FindAllWorkStations()
    {
        var stations = FindObjectsByType<WorkStation>(FindObjectsSortMode.None);

        cashRegisters = stations.Where(x => x.stationType == StationType.CashRegister).ToList();
        storages = stations.Where(x => x.stationType == StationType.Storage).ToList();
        shelves = stations.Where(x => x.stationType == StationType.Shelf).ToList();

        // --- DEBUG ---
        Debug.Log($"[MANAGER] Am găsit {stations.Length} stații total.");
        Debug.Log($" -> Case de marcat: {cashRegisters.Count}");
        Debug.Log($" -> Depozite: {storages.Count}");
        // -------------
    }

    public void RefreshStations()
    {
        // 1. Căutăm din nou toate stațiile
        FindAllWorkStations();

        // 2. Verificăm toți angajații care NU au stație setată
        foreach (var emp in allEmployees)
        {
            if (emp.myWorkStation == null)
            {
                // Încercăm să le găsim loc acum
                AssignStationToEmployee(emp);
            }
        }
    }

    private void AssignStationToEmployee(Employee script)
    {
        Transform targetPos = null;

        if (script.role == EmployeeRole.Cashier)
        {
            // --- FIX: Curățăm lista de obiecte șterse ---
            // RemoveAll(x => x == null) va șterge automat din listă orice casă distrusă
            cashRegisters.RemoveAll(x => x == null);

            if (cashRegisters.Count > 0)
            {
                WorkStation station = cashRegisters[0];
                targetPos = station.interactionPoint != null ? station.interactionPoint : station.transform;
            }
        }
        else if (script.role == EmployeeRole.Restocker)
        {
            // --- FIX: Curățăm lista de depozite ---
            storages.RemoveAll(x => x == null);

            if (storages.Count > 0)
            {
                WorkStation storage = storages[0];
                targetPos = storage.interactionPoint != null ? storage.interactionPoint : storage.transform;

                // --- FIX: Curățăm lista de rafturi ---
                shelves.RemoveAll(x => x == null);

                if (shelves.Count > 0)
                {
                    // Alegem un raft valid (aici luăm primul, dar logic ar fi unul random sau gol)
                    WorkStation shelf = shelves[0];
                    script.secondaryTarget = shelf.interactionPoint != null ? shelf.interactionPoint : shelf.transform;
                }
            }
        }

        if (targetPos != null)
        {
            script.AssignRole(script.role, targetPos);
            Debug.Log($"[MANAGER] Am asignat {script.employeeName} la {targetPos.name}");
        }
        else
        {
            // Opțional: Dacă nu am găsit stație, anunțăm
            Debug.LogWarning($"[MANAGER] Nu am găsit o stație validă pentru {script.employeeName} ({script.role})");
        }
    }

    public Employee HireEmployee(string name)
    {
        if (allEmployees.Count >= maxEmployees) return null;

        GameObject newObj = Instantiate(employeePrefab, spawnPoint.position, Quaternion.identity);
        newObj.name = "Angajat_" + name;

        Employee script = newObj.GetComponent<Employee>();
        if (script != null)
        {
            script.employeeName = name;
            script.role = EmployeeRole.None;
            allEmployees.Add(script);

            // --- FIXUL ESTE AICI ---
            // Verificăm dacă magazinul este DEJA deschis
            bool isShopOpen = false;
            if (TimeManager.Instance != null)
            {
                int currentH = TimeManager.Instance.CurrentHour;
                int openH = TimeManager.Instance.openHour;
                int closeH = TimeManager.Instance.closeHour;

                // Verificăm intervalul orar
                if (currentH >= openH && currentH < closeH)
                {
                    isShopOpen = true;
                }
            }

            if (isShopOpen)
            {
                // Dacă magazinul e deschis, îl punem la treabă imediat!
                script.StartShift(spawnPoint.position);
                Debug.Log($"[MANAGER] {name} a fost angajat în timpul programului și începe munca!");
            }
            else
            {
                // Dacă e noapte, îl dezactivăm până dimineața
                newObj.SetActive(false);
                Debug.Log($"[MANAGER] {name} a fost angajat, dar așteaptă deschiderea magazinului.");
            }
        }

        return script;
    }

    public void FireEmployee(Employee employee)
    {
        if (allEmployees.Contains(employee))
        {
            allEmployees.Remove(employee);
            Destroy(employee.gameObject); // Îl ștergem fizic din lume
            Debug.Log($"[MANAGER] {employee.employeeName} a fost concediat.");
        }
    }

    // Metodă ajutătoare pentru Dropdown-ul din UI
    public void ChangeEmployeeRole(Employee emp, EmployeeRole newRole)
    {
        // 1. Îi scoatem rolul vechi (dacă avea stație, o eliberăm?)
        // (Aici poți adăuga logică să eliberezi stația veche dacă e cazul)

        // 2. Setăm noul rol
        emp.role = newRole;
        emp.myWorkStation = null; // Resetăm stația
        emp.secondaryTarget = null;

        // 3. Încercăm să îi găsim o stație nouă imediat
        AssignStationToEmployee(emp);

        Debug.Log($"[MANAGER] Rol schimbat pentru {emp.employeeName}: {newRole}");
    }

    private void StartWorkDay()
    {
        foreach (var emp in allEmployees) emp.StartShift(spawnPoint.position);
    }

    private void EndWorkDay()
    {
        foreach (var emp in allEmployees) emp.EndShift();
    }
}