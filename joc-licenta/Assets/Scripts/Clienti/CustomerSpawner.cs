using UnityEngine;
using System.Collections.Generic;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Prefabs")]
    [Tooltip("Adaugă aici toate modelele 3D de clienți (băieți, fete, diferiți etc.)")]
    [SerializeField] private List<CustomerAI> customerPrefabs = new List<CustomerAI>();

    [Header("Spawn/Exit Points")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform exitPoint;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 6f;
    [SerializeField] private int maxAliveCustomers = 8;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Economy")]
    [SerializeField] private int minCustomerBudget = 50;
    [SerializeField] private int maxCustomerBudget = 250;

    [Header("References")]
    [SerializeField] private EmployeeManager employeeManager;

    [Header("Conditions")]
    [SerializeField] private bool requireAtLeastOneCashRegister = true;
    [SerializeField] private bool requireAtLeastOneShelf = true;
    [SerializeField] private float registryRefreshInterval = 2f;

    private WorkStationRegistry _registry;
    private float _spawnTimer;
    private float _refreshTimer;
    private float _lastNoSpawnLogTime;

    // OPTIMIZARE: Ținem minte clienții activi ca să nu mai folosim FindObjectsByType
    private List<CustomerAI> _activeCustomers = new List<CustomerAI>();

    // OPTIMIZARE: Ținem minte dacă avem case de marcat pentru a nu căuta constant prin scenă
    private bool _hasCashRegistersCached = false;

    private void Start()
    {
        if (employeeManager == null)
            employeeManager = EmployeeManager.Instance != null
                ? EmployeeManager.Instance
                : FindFirstObjectByType<EmployeeManager>();

        if (employeeManager != null)
            _registry = employeeManager.StationRegistry;

        if (_registry == null)
        {
            Debug.LogError("[CustomerSpawner] Registry missing. Spawner disabled.");
            enabled = false;
            return;
        }

        RefreshRegistryAndCache();
        _refreshTimer = registryRefreshInterval;

        _spawnTimer = spawnOnStart ? 0f : spawnInterval;
    }

    private void Update()
    {
        if (customerPrefabs == null || customerPrefabs.Count == 0) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (exitPoint == null) return;
        if (_registry == null) return;

        // Refresh periodic pentru stații și case de marcat
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            RefreshRegistryAndCache();
            _refreshTimer = registryRefreshInterval;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;

        if (!CanSpawnCustomers())
        {
            LogNoSpawnReasonOccasionally();
            _spawnTimer = spawnInterval;
            return;
        }

        // OPTIMIZARE: Curățăm lista de clienți care au fost distruși (au plecat acasă)
        _activeCustomers.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        if (_activeCustomers.Count < maxAliveCustomers)
        {
            SpawnOne();
        }

        _spawnTimer = spawnInterval;
    }

    private void RefreshRegistryAndCache()
    {
        _registry.RefreshAllStations();

        // Căutăm casele de marcat o singură dată la refresh, nu la fiecare spawn încercat
        _hasCashRegistersCached = FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None).Length > 0;
    }

    private bool CanSpawnCustomers()
    {
        if (TimeManager.Instance != null)
        {
            int currentHour = TimeManager.Instance.CurrentHour;
            if (currentHour < TimeManager.Instance.openHour || currentHour >= TimeManager.Instance.closeHour)
            {
                return false;
            }
        }

        if (requireAtLeastOneShelf)
        {
            var shelves = _registry.GetAllShelves();
            if (shelves == null || shelves.Count == 0)
                return false;
        }

        if (requireAtLeastOneCashRegister && !_hasCashRegistersCached)
        {
            return false;
        }

        return true;
    }

    private void LogNoSpawnReasonOccasionally()
    {
        if (Time.time - _lastNoSpawnLogTime < 3f) return;
        _lastNoSpawnLogTime = Time.time;

        string reason = "";

        if (TimeManager.Instance != null)
        {
            int currentHour = TimeManager.Instance.CurrentHour;
            if (currentHour < TimeManager.Instance.openHour || currentHour >= TimeManager.Instance.closeHour)
            {
                reason += $"shop is closed (Hour: {currentHour}); ";
            }
        }

        if (requireAtLeastOneShelf)
        {
            var shelves = _registry.GetAllShelves();
            if (shelves == null || shelves.Count == 0)
                reason += "no shelves; ";
        }

        if (requireAtLeastOneCashRegister && !_hasCashRegistersCached)
        {
            reason += "no cash registers; ";
        }

        if (!string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[CustomerSpawner] Not spawning customers: {reason}");
        }
    }

    private void SpawnOne()
    {
        // Alegem un model random din lista de prefabs
        CustomerAI selectedPrefab = customerPrefabs[Random.Range(0, customerPrefabs.Count)];

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject go = Instantiate(selectedPrefab.gameObject, sp.position, sp.rotation);

        var customer = go.GetComponent<CustomerAI>();

        if (customer != null)
        {
            int randomBudget = Random.Range(minCustomerBudget, maxCustomerBudget + 1);
            customer.Initialize(_registry, exitPoint, randomBudget);

            // Îl adăugăm în lista locală ca să îl contorizăm eficient
            _activeCustomers.Add(customer);
        }
    }
}