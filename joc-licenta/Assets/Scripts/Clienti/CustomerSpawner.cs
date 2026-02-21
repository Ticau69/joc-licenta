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

        // Abonăm evenimentele de deschis/închis
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnShopOpen += OnShopOpened;
            TimeManager.Instance.OnShopClose += OnShopClosed;
        }
        else
        {
            Debug.LogWarning("[CustomerSpawner] TimeManager.Instance e null. " +
                             "Clienții vor spawna indiferent de orar.");
        }

        _timer = spawnOnStart ? 0f : spawnInterval;
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnShopOpen -= OnShopOpened;
            TimeManager.Instance.OnShopClose -= OnShopClosed;
        }
    }

    private void Update()
    {
        if (customerPrefab == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (exitPoint == null) return;
        if (_registry == null) return;

        // Nu spawna dacă magazinul e închis
        if (!IsShopOpen()) return;

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

    /// <summary>
    /// Returnează true dacă magazinul e deschis conform TimeManager.
    /// Dacă TimeManager lipsește, considerăm magazinul mereu deschis (fallback).
    /// </summary>
    private bool IsShopOpen()
    {
        if (TimeManager.Instance == null) return true;

        int hour = TimeManager.Instance.CurrentHour;
        int open = TimeManager.Instance.openHour;
        int close = TimeManager.Instance.closeHour;

        // Același calcul ca în TimeManager.IsWithinShopHours()
        int effectiveClose = Mathf.Clamp(close, 0, 24);

        if (open < effectiveClose)
            return hour >= open && hour < effectiveClose;

        if (open > effectiveClose)
            return hour >= open || hour < effectiveClose;

        return false;
    }

    // ── Opțional: log când magazinul se deschide/închide ────────────────────
    private void OnShopOpened()
    {
        Debug.Log("[CustomerSpawner] Magazin deschis — clienții pot intra.");
        _timer = 0f; // spawn imediat la deschidere
    }

    private void OnShopClosed()
    {
        Debug.Log("[CustomerSpawner] Magazin închis — nu mai spawnam clienți.");
    }
}