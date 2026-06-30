using UnityEngine;
using System.Collections.Generic;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Prefabs")]
    [Tooltip("Adaugă aici toate modelele 3D de clienți (băieți, fete, diferiți etc.)")]
    [SerializeField] private List<CustomerAI> customerPrefabs = new List<CustomerAI>();
    private List<CustomerAI> _customerPool = new List<CustomerAI>();

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

    public static event System.Action OnStoreCompletelyEmpty;

    private WorkStationRegistry _registry;
    private float _spawnTimer;
    private float _refreshTimer;
    private float _lastNoSpawnLogTime;

    // OPTIMIZARE: Ținem minte clienții activi ca să nu mai folosim FindObjectsByType
    private List<CustomerAI> _activeCustomers = new List<CustomerAI>();

    // OPTIMIZARE: Ținem minte dacă avem case de marcat pentru a nu căuta constant prin scenă
    private bool _isStoreReady = false;
    private bool _endOfDayTriggered = false;
    private bool _hasOpenedToday = false;
    private float _forceEvictTimer = 0f;
    private const float FORCE_EVICT_AFTER = 15f; // secunde după închidere

    private void Start()
    {
        if (employeeManager == null)
            employeeManager = EmployeeManager.Instance != null
                ? EmployeeManager.Instance
                : FindFirstObjectByType<EmployeeManager>();

        if (employeeManager != null)
            _registry = employeeManager.StationRegistry;

        _registry = WorkStationRegistry.Instance;
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

        // 1. Refresh periodic
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            RefreshRegistryAndCache();



            _refreshTimer = registryRefreshInterval;
        }

        // 3. --- LOGICA DE ÎNCHIDERE (Mutată deasupra acelui 'return' fatal) ---
        if (TimeManager.Instance != null)
        {
            int currentHour = TimeManager.Instance.CurrentHour;
            int openH = TimeManager.Instance.openHour;
            int closeH = TimeManager.Instance.closeHour;

            // 3.1 Cât timp e ziuă (ex: 08:00 - 21:59), magazinul este oficial DESCHIS
            if (currentHour >= openH && currentHour < closeH)
            {
                _hasOpenedToday = true;
                _endOfDayTriggered = false; // Resetăm trigger-ul pentru raport
            }

            // 3.2 E timpul închiderii? (Trecut de 22:00 SAU între 00:00 și 08:00)
            bool isClosedTime = (currentHour >= closeH || currentHour < openH);

            // 3.3 Dacă e ora de închidere, magazinul A FOST deschis azi, și nu mai sunt clienți
            if (isClosedTime && _hasOpenedToday)
            {
                if (_activeCustomers.Count == 0 && !_endOfDayTriggered)
                {
                    _endOfDayTriggered = true;
                    _hasOpenedToday = false;
                    _forceEvictTimer = 0f;
                    OnStoreCompletelyEmpty?.Invoke();
                }
                else if (_activeCustomers.Count > 0 && !_endOfDayTriggered)
                {
                    // Clienți blocați — numărăm timeout-ul
                    _forceEvictTimer += Time.deltaTime;

                    if (_forceEvictTimer >= FORCE_EVICT_AFTER)
                    {
                        Debug.LogWarning($"[CustomerSpawner] {_activeCustomers.Count} clienți blocați după închidere — forțăm ieșirea!");

                        // Forțăm toți clienții să iasă
                        foreach (var c in _activeCustomers)
                        {
                            if (c != null && c.gameObject.activeInHierarchy)
                            {
                                // Trimitem clientul direct la exit
                                var ai = c.GetComponent<CustomerAI>();
                                if (ai != null) ai.ForceExit();
                                else c.gameObject.SetActive(false);
                            }
                        }
                        _activeCustomers.Clear();
                        // Trigger-ul va fi detectat în frame-ul următor
                    }
                }
            }
        }

        // 4. Verificăm dacă mai avem voie să spawnăm
        if (!CanSpawnCustomers())
        {
            LogNoSpawnReasonOccasionally();
            return; // Dacă e închis, se oprește aici (dar abia DUPĂ ce a verificat dacă au ieșit clienții)
        }

        // 5. Spawnăm clienți
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            if (_activeCustomers.Count < maxAliveCustomers)
            {
                SpawnOne();
            }
            _spawnTimer = spawnInterval;
        }
    }

    private void RefreshRegistryAndCache()
    {
        _registry.RefreshAllStations();

        // 1. Aflăm ce avem efectiv în magazin
        bool hasRegisters = _registry.GetAnyCashRegister() != null;

        var shelves = _registry.GetAllShelves();
        bool hasShelves = shelves != null && shelves.Count > 0;

        // 2. Trecem prin "filtrele" din Inspector
        bool registerOk = !requireAtLeastOneCashRegister || hasRegisters;
        bool shelfOk = !requireAtLeastOneShelf || hasShelves;

        // 3. Salvăm verdictul final! Magazinul e gata DOAR dacă ambele condiții sunt îndeplinite.
        _isStoreReady = registerOk && shelfOk;

        for (int i = _activeCustomers.Count - 1; i >= 0; i--)
        {
            if (_activeCustomers[i] == null || !_activeCustomers[i].gameObject.activeInHierarchy)
            {
                _activeCustomers.RemoveAt(i);
            }
        }
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

        if (requireAtLeastOneCashRegister && !_isStoreReady)
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

        if (requireAtLeastOneCashRegister && !_isStoreReady)
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
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        CustomerAI customerToSpawn = null;

        // 1. CĂUTĂM UN CLIENT RECICLABIL
        // Verificăm dacă avem vreun client invizibil/dezactivat în pool
        foreach (var c in _customerPool)
        {
            if (c != null && !c.gameObject.activeInHierarchy)
            {
                customerToSpawn = c;
                break;
            }
        }

        // 2. CREĂM UNUL NOU (Doar dacă e absolut necesar)
        if (customerToSpawn == null)
        {
            CustomerAI selectedPrefab = customerPrefabs[Random.Range(0, customerPrefabs.Count)];
            GameObject go = Instantiate(selectedPrefab.gameObject, sp.position, sp.rotation);
            customerToSpawn = go.GetComponent<CustomerAI>();

            // Îl adăugăm în lista generală ca să îl putem recicla mai târziu
            _customerPool.Add(customerToSpawn);
        }

        // 3. PREGĂTIM CLIENTUL PENTRU NOUA ZI DE CUMPĂRĂTURI
        if (customerToSpawn != null)
        {
            // Dacă folosești NavMeshAgent, trebuie dezactivat înainte de teleportare, 
            // altfel agentul îl va trage înapoi la vechea poziție!
            UnityEngine.AI.NavMeshAgent agent = customerToSpawn.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            customerToSpawn.transform.position = sp.position;
            customerToSpawn.transform.rotation = sp.rotation;
            customerToSpawn.gameObject.SetActive(true);

            if (agent != null) agent.enabled = true;

            int randomBudget = Random.Range(minCustomerBudget, maxCustomerBudget + 1);

            // Initialize va "curăța" mintea AI-ului exact cum ai programat tu
            customerToSpawn.Initialize(_registry, exitPoint, randomBudget);

            // Îl trecem la lista de clienți activi (care sunt în magazin)
            if (!_activeCustomers.Contains(customerToSpawn))
            {
                _activeCustomers.Add(customerToSpawn);
            }
        }
    }
}