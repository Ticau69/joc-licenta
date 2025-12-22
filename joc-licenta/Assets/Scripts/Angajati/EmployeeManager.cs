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

        // TEST: Angajăm 3 oameni diferiți
        HireEmployee("Maria", EmployeeRole.Cashier);
        HireEmployee("Ion", EmployeeRole.Janitor);
        HireEmployee("Alex", EmployeeRole.Restocker);
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

    public void HireEmployee(string name, EmployeeRole role)
    {
        if (allEmployees.Count >= maxEmployees) return;

        GameObject newObj = Instantiate(employeePrefab, spawnPoint.position, Quaternion.identity);
        newObj.name = role.ToString() + "_" + name;

        Employee script = newObj.GetComponent<Employee>();
        if (script != null)
        {
            script.employeeName = name;
            script.role = role; // Setăm rolul, dar stația o căutăm mai jos

            // Încercăm să asignăm stația (dacă există)
            AssignStationToEmployee(script);

            allEmployees.Add(script);

            // Dacă e în timpul programului, îl activăm
            // (Verifică logica ta cu TimeManager aici, pentru simplitate îl las inactiv inițial)
            newObj.SetActive(false);
        }
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