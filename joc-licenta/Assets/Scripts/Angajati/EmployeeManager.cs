using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EmployeeManager : MonoBehaviour
{
    #region Singleton
    public static EmployeeManager Instance { get; private set; }
    #endregion

    #region Configuration
    [Header("Settings")]
    [SerializeField] private GameObject employeePrefab;
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
            TimeManager.Instance.OnShopClose += EndWorkDay;
        }
    }

    private void UnregisterTimeManagerEvents()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnShopOpen -= StartWorkDay;
            TimeManager.Instance.OnShopClose -= EndWorkDay;
        }
    }
    #endregion

    #region Public Methods - Station Management
    public void FindAllWorkStations()
    {
        StationRegistry.RefreshAllStations();
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
    #endregion

    #region Public Methods - Employee Management
    public Employee HireEmployee(string name)
    {
        if (!CanHireMoreEmployees())
        {
            Debug.LogWarning($"[EmployeeManager] Cannot hire {name} - max employees reached ({maxEmployees})");
            return null;
        }

        Employee newEmployee = CreateEmployee(name);

        if (newEmployee != null)
        {
            allEmployees.Add(newEmployee);
            HandleNewEmployeeShift(newEmployee);
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

    private Employee CreateEmployee(string name)
    {
        GameObject newObj = Instantiate(employeePrefab, spawnPoint.position, Quaternion.identity);
        newObj.name = $"Employee_{name}";

        Employee script = newObj.GetComponent<Employee>();
        if (script != null)
        {
            script.employeeName = name;
            script.role = EmployeeRole.None;
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
                // Janitors don't need a specific station
                Debug.Log($"[EmployeeManager] {employee.employeeName} assigned as Janitor (no station needed)");
                break;

            default:
                Debug.LogWarning($"[EmployeeManager] {employee.employeeName} has no defined role.");
                break;
        }
    }

    private void AssignCashierStation(Employee employee)
    {
        WorkStation station = StationRegistry.GetAnyCashRegister();

        if (station != null)
        {
            Transform targetPos = station.interactionPoint != null
                ? station.interactionPoint
                : station.transform;

            employee.AssignRole(EmployeeRole.Cashier, targetPos);
            Debug.Log($"[EmployeeManager] Assigned {employee.employeeName} to cash register at {targetPos.name}");
        }
        else
        {
            Debug.LogWarning($"[EmployeeManager] No available cash register for {employee.employeeName}");
        }
    }

    private void AssignRestockerStation(Employee employee)
    {
        List<WorkStation> allShelves = StationRegistry.GetAllShelves();

        if (allShelves.Count > 0)
        {
            WorkStation storage = allShelves[0]; // Assign first available shelf

            Transform targetPos = storage.interactionPoint != null
                ? storage.interactionPoint
                : storage.transform;

            employee.myWorkStation = storage.transform;
            employee.AssignRole(EmployeeRole.Restocker, targetPos);

            // Let the employee find shelves dynamically - don't assign specific shelf
            employee.secondaryTarget = null;

            Debug.Log($"[EmployeeManager] Assigned {employee.employeeName} to storage at {targetPos.name}");
        }
        else
        {
            Debug.LogWarning($"[EmployeeManager] No storage available for Restocker {employee.employeeName}!");
        }
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

    private void EndWorkDay()
    {
        Debug.Log("[EmployeeManager] Ending work day for all employees");
        foreach (var emp in allEmployees)
        {
            emp.EndShift();
        }
    }
    #endregion

    #region Public Accessors
    public int CurrentEmployeeCount => allEmployees.Count;
    public int MaxEmployeeCount => maxEmployees;
    public List<Employee> AllEmployees => new List<Employee>(allEmployees); // Return copy for safety
    #endregion
}