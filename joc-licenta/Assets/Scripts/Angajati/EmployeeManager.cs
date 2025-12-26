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
        Transform targetStation = null;

        if (script.role == EmployeeRole.Cashier && cashRegisters.Count > 0)
        {
            // Simplificat: ia prima casă (poți face logică să ia una liberă)
            targetStation = cashRegisters[0].transform;
        }
        else if (script.role == EmployeeRole.Restocker && storages.Count > 0)
        {
            targetStation = storages[0].transform;
            if (shelves.Count > 0) script.secondaryTarget = shelves[0].transform;
        }

        // Dacă am găsit o stație, o asignăm
        if (targetStation != null)
        {
            script.AssignRole(script.role, targetStation);
            Debug.Log($"[MANAGER] Am asignat {script.employeeName} la {targetStation.name}");
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
            script.role = EmployeeRole.None; // Default la început

            allEmployees.Add(script);
            newObj.SetActive(false); // Așteaptă programul
        }

        return script; // <--- Returnăm scriptul ca să îl folosim în UI
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