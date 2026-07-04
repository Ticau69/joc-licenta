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

    public static event System.Action OnStoreCompletelyEmpty;

    private WorkStationRegistry _registry;
    private float _spawnTimer;
    private float _refreshTimer;
    private float _lastNoSpawnLogTime;

    // Clienți activi (în magazin acum). Evită FindObjectsByType.
    private readonly List<CustomerAI> _activeCustomers = new List<CustomerAI>();

    // OPTIMIZARE: coadă de clienți inactivi gata de reciclare — extragere O(1),
    // în loc de scanarea liniară a întregului pool la fiecare spawn.
    private readonly Queue<CustomerAI> _inactivePool = new Queue<CustomerAI>();

    // OPTIMIZARE: NavMeshAgent cache-uit per client, ca să nu mai facem
    // GetComponent la fiecare reciclare (constant în timp, dar inutil repetat).
    private readonly Dictionary<CustomerAI, UnityEngine.AI.NavMeshAgent> _agentCache =
        new Dictionary<CustomerAI, UnityEngine.AI.NavMeshAgent>();

    // Cache-uit o dată pe frame, ca să nu apelăm GetAllShelves() de 2 ori
    // (o dată în CanSpawnCustomers, o dată în LogNoSpawnReasonOccasionally).
    private IReadOnlyList<WorkStation> _shelvesCacheThisFrame;
    private int _shelvesCacheFrame = -1;

    private bool _isStoreReady;
    private bool _endOfDayTriggered;
    private bool _hasOpenedToday;
    private float _forceEvictTimer;
    private const float FORCE_EVICT_AFTER = 15f; // secunde după închidere

    private void Start()
    {
        if (employeeManager == null)
            employeeManager = EmployeeManager.Instance != null
                ? EmployeeManager.Instance
                : FindFirstObjectByType<EmployeeManager>();

        // Sursa de adevăr e mereu WorkStationRegistry.Instance; nu mai citim
        // inutil employeeManager.StationRegistry doar ca să-l suprascriem imediat.
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

        // 2. --- LOGICA DE ÎNCHIDERE ---
        if (TimeManager.Instance != null)
        {
            int currentHour = TimeManager.Instance.CurrentHour;
            int openH = TimeManager.Instance.openHour;
            int closeH = TimeManager.Instance.closeHour;

            if (currentHour >= openH && currentHour < closeH)
            {
                _hasOpenedToday = true;
                _endOfDayTriggered = false;
            }

            bool isClosedTime = (currentHour >= closeH || currentHour < openH);

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
                    _forceEvictTimer += Time.deltaTime;

                    if (_forceEvictTimer >= FORCE_EVICT_AFTER)
                    {
                        Debug.LogWarning($"[CustomerSpawner] {_activeCustomers.Count} clienți blocați după închidere — forțăm ieșirea!");

                        foreach (var c in _activeCustomers)
                        {
                            if (c != null && c.gameObject.activeInHierarchy)
                            {
                                // 'c' e deja CustomerAI — GetComponent<CustomerAI>() aici
                                // doar ar fi întors aceeași referință, degeaba.
                                c.ForceExit();
                            }
                        }
                        _activeCustomers.Clear();
                    }
                }
            }
        }

        // 3. Verificăm dacă mai avem voie să spawnăm
        if (!CanSpawnCustomers())
        {
            LogNoSpawnReasonOccasionally();
            return;
        }

        // 4. Spawnăm clienți
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

        bool hasRegisters = _registry.GetAnyCashRegister() != null;

        var shelves = GetShelvesCached();
        bool hasShelves = shelves != null && shelves.Count > 0;

        bool registerOk = !requireAtLeastOneCashRegister || hasRegisters;
        bool shelfOk = !requireAtLeastOneShelf || hasShelves;

        _isStoreReady = registerOk && shelfOk;

        for (int i = _activeCustomers.Count - 1; i >= 0; i--)
        {
            var c = _activeCustomers[i];
            if (c == null || !c.gameObject.activeInHierarchy)
            {
                _activeCustomers.RemoveAt(i);

                // Dacă a fost dezactivat "din afară" (nu prin fluxul nostru de
                // reciclare), îl băgăm înapoi în pool ca să nu se piardă.
                if (c != null && !_inactivePool.Contains(c))
                    _inactivePool.Enqueue(c);
            }
        }
    }

    // Cache simplu per-frame pentru lista de rafturi, ca să nu o cerem de
    // 2 ori în același frame (CanSpawnCustomers + LogNoSpawnReasonOccasionally).
    private IReadOnlyList<WorkStation> GetShelvesCached()
    {
        if (_shelvesCacheFrame != Time.frameCount)
        {
            _shelvesCacheThisFrame = _registry.GetAllShelves();
            _shelvesCacheFrame = Time.frameCount;
        }
        return _shelvesCacheThisFrame;
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
            var shelves = GetShelvesCached();
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
            var shelves = GetShelvesCached();
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
        CustomerAI customerToSpawn;

        // 1. Extragere O(1) dintr-un client reciclabil, dacă există.
        // (Curățăm și eventuale referințe distruse între timp, apărute din
        // scene reload sau Destroy extern.)
        customerToSpawn = null;
        while (_inactivePool.Count > 0)
        {
            var candidate = _inactivePool.Dequeue();
            if (candidate != null)
            {
                customerToSpawn = candidate;
                break;
            }
        }

        // 2. Creăm unul nou doar dacă pool-ul nu avea niciunul disponibil.
        if (customerToSpawn == null)
        {
            CustomerAI selectedPrefab = customerPrefabs[Random.Range(0, customerPrefabs.Count)];
            GameObject go = Instantiate(selectedPrefab.gameObject, sp.position, sp.rotation);
            customerToSpawn = go.GetComponent<CustomerAI>();

            // Cache-uim NavMeshAgent o singură dată, la creare — nu la fiecare reciclare.
            _agentCache[customerToSpawn] = customerToSpawn.GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        if (customerToSpawn == null) return;

        // 3. Pregătim clientul pentru noua zi de cumpărături.
        _agentCache.TryGetValue(customerToSpawn, out var agent);

        // Dezactivăm agentul înainte de teleportare, altfel te trage înapoi
        // la vechea poziție.
        if (agent != null) agent.enabled = false;

        customerToSpawn.transform.SetPositionAndRotation(sp.position, sp.rotation);
        customerToSpawn.gameObject.SetActive(true);

        if (agent != null) agent.enabled = true;

        int randomBudget = Random.Range(minCustomerBudget, maxCustomerBudget + 1);
        customerToSpawn.Initialize(_registry, exitPoint, randomBudget);

        _activeCustomers.Add(customerToSpawn);
    }

    /// <summary>
    /// Apelată de CustomerAI (sau de sistemul care îl dezactivează) când
    /// clientul termină și iese din magazin — îl întoarce în pool imediat,
    /// în loc să aștepte următorul RefreshRegistryAndCache().
    /// Opțional: leagă acest apel de un eveniment OnCustomerExited din CustomerAI
    /// dacă vrei reciclare instant fără să aștepți refresh-ul periodic.
    /// </summary>
    public void ReturnToPool(CustomerAI customer)
    {
        if (customer == null) return;

        _activeCustomers.Remove(customer);

        if (!_inactivePool.Contains(customer))
            _inactivePool.Enqueue(customer);
    }
}