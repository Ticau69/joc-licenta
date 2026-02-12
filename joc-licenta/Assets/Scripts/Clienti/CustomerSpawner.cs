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

    [Header("Registry Source")]
    [SerializeField] private EmployeeManager employeeManager;

    private WorkStationRegistry _registry;
    private float _timer;

    private void Start()
    {
        // Important: în Start() e sigur că EmployeeManager.Awake() a rulat deja
        if (employeeManager == null)
            employeeManager = EmployeeManager.Instance != null
                ? EmployeeManager.Instance
                : FindFirstObjectByType<EmployeeManager>();

        if (employeeManager != null)
            _registry = employeeManager.StationRegistry;

        if (_registry == null)
        {
            Debug.LogError("[CustomerSpawner] WorkStationRegistry is missing. " +
                           "Verifică să existe EmployeeManager în scenă și să fie activ.");
            enabled = false;
            return;
        }

        _registry.RefreshAllStations();

        _timer = spawnOnStart ? 0f : spawnInterval;
    }

    private void Update()
    {
        if (customerPrefab == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (exitPoint == null) return;
        if (_registry == null) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        int alive = FindObjectsByType<CustomerAI>(FindObjectsSortMode.None).Length;
        if (alive < maxAliveCustomers)
            SpawnOne();

        _timer = spawnInterval;
    }

    private void SpawnOne()
    {
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var customer = Instantiate(customerPrefab, sp.position, sp.rotation);
        customer.Initialize(_registry, exitPoint);
    }
}
