using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Prefab")]
    [SerializeField] private CustomerAI customerPrefab;

    [Header("Spawn/Exit Points")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform exitPoint;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 6f;
    [SerializeField] private int maxAliveCustomers = 8;
    [SerializeField] private bool spawnOnStart = true;

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

        _registry.RefreshAllStations();
        _refreshTimer = registryRefreshInterval;

        _spawnTimer = spawnOnStart ? 0f : spawnInterval;
    }

    private void Update()
    {
        if (customerPrefab == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (exitPoint == null) return;
        if (_registry == null) return;

        // refresh la registry periodic (pentru tycoon: când playerul plasează rafturi/case)
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            _registry.RefreshAllStations();
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

        int alive = FindObjectsByType<CustomerAI>(FindObjectsSortMode.None).Length;
        if (alive < maxAliveCustomers)
        {
            SpawnOne();
        }

        _spawnTimer = spawnInterval;
    }

    private bool CanSpawnCustomers()
    {
        if (requireAtLeastOneShelf)
        {
            var shelves = _registry.GetAllShelves();
            if (shelves == null || shelves.Count == 0)
                return false;
        }

        if (requireAtLeastOneCashRegister)
        {
            // prefer: caută casele care chiar au CashRegisterQueue (case funcționale)
            var queues = FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None);
            if (queues == null || queues.Length == 0)
                return false;
        }

        return true;
    }

    private void LogNoSpawnReasonOccasionally()
    {
        // log max o dată la 3 sec ca să nu spammeze
        if (Time.time - _lastNoSpawnLogTime < 3f) return;
        _lastNoSpawnLogTime = Time.time;

        string reason = "";

        if (requireAtLeastOneShelf)
        {
            var shelves = _registry.GetAllShelves();
            if (shelves == null || shelves.Count == 0)
                reason += "no shelves; ";
        }

        if (requireAtLeastOneCashRegister)
        {
            var queues = FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None);
            if (queues == null || queues.Length == 0)
                reason += "no cash registers (CashRegisterQueue); ";
        }

        Debug.Log($"[CustomerSpawner] Not spawning customers: {reason}");
    }

    private void SpawnOne()
    {
        Transform sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        var customer = Instantiate(customerPrefab, sp.position, sp.rotation);

        customer.Initialize(_registry, exitPoint);
    }
}