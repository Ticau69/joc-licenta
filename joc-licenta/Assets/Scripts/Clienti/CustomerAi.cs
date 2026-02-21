using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAI : MonoBehaviour
{
    [Serializable]
    public struct ShoppingItem
    {
        public ProductType product;
        public int amount;
    }

    public enum State
    {
        Idle,
        GoingToShelf,
        TakingProduct,
        GoingToRegister,
        InQueue,
        Leaving
    }

    [Header("Shopping")]
    [SerializeField] private int minItems = 1;
    [SerializeField] private int maxItems = 4;
    [SerializeField] private int minAmountPerItem = 1;
    [SerializeField] private int maxAmountPerItem = 3;

    [Header("Movement")]
    [SerializeField] private float arriveDistance = 1f;

    private NavMeshAgent _agent;
    private Transform _exitPoint;
    private WorkStationRegistry _registry;
    private IEventBus _eventBus;

    [SerializeField] private List<ShoppingItem> _list = new();
    private int _currentIndex = 0;

    // Ce a cumpărat efectiv (produs -> cantitate)
    private readonly Dictionary<ProductType, int> _basket = new();

    private WorkStation _targetShelf;
    private CashRegisterQueue _targetRegister;
    private Transform _queueTarget;

    // Guard împotriva lui IsAtDestination() care returnează true în primul frame
    private bool _destinationSet = false;
    private float _destinationSetTime = 0f;
    private const float DestinationSettleDelay = 0.1f;

    [SerializeField]
    private State currentState;

    public State CurrentState
    {
        get => currentState;
        private set => currentState = value;
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public void Initialize(WorkStationRegistry registry, Transform exitPoint)
    {
        _registry = registry;
        _exitPoint = exitPoint;
        ServiceLocator.Instance.TryGet(out _eventBus);

        GenerateShoppingList();
        _currentIndex = 0;

        // ─── DEBUG: afișăm lista generată ───────────────────────────────────
        if (_list.Count == 0)
            Debug.LogWarning($"[CustomerAI] {name} - Lista de cumpărături e GOALĂ! " +
                             "Verifică ProductDataBase.Instance și că are produse înregistrate.");
        else
            Debug.Log($"[CustomerAI] {name} - Lista generată: {_list.Count} iteme.");
        // ────────────────────────────────────────────────────────────────────

        GoNextItemOrCheckout();
    }

    private void Update()
    {
        switch (CurrentState)
        {
            // ── Merge spre raft ──────────────────────────────────────────────
            case State.GoingToShelf:
                if (HasDestinationSettled() && IsAtDestination())
                {
                    CurrentState = State.TakingProduct;
                    TryTakeFromShelf();
                }
                break;

            // ── Merge spre casa de marcat ────────────────────────────────────
            case State.GoingToRegister:
                if (HasDestinationSettled() && IsAtDestination())
                {
                    TryJoinQueue();
                }
                break;

            // ── Stă la coadă și urmează spot-ul alocat ───────────────────────
            case State.InQueue:
                if (_queueTarget != null)
                    _agent.SetDestination(_queueTarget.position);
                break;

            // ── Pleacă din magazin ───────────────────────────────────────────
            case State.Leaving:
                if (_exitPoint != null &&
                    Vector3.Distance(transform.position, _exitPoint.position) <= arriveDistance)
                    Destroy(gameObject);
                break;
        }
    }

    // =========================================================================
    //  GENERARE LISTĂ
    // =========================================================================

    private void GenerateShoppingList()
    {
        _list.Clear();

        var db = ProductDataBase.Instance;
        if (db == null)
        {
            Debug.LogError("[CustomerAI] ProductDataBase.Instance este NULL! " +
                           "Asigură-te că există un ProductDataBase în scenă.");
            // Fallback: folosim enum-ul direct dacă db lipsește
            GenerateShoppingListFromEnum();
            return;
        }

        var pool = db.GetAllProductTypes();
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[CustomerAI] ProductDataBase nu returnează niciun produs! " +
                             "Folosim fallback din enum.");
            GenerateShoppingListFromEnum();
            return;
        }

        int itemCount = UnityEngine.Random.Range(minItems, maxItems + 1);
        itemCount = Mathf.Min(itemCount, pool.Count);

        for (int i = 0; i < itemCount; i++)
        {
            int pickIndex = UnityEngine.Random.Range(0, pool.Count);
            ProductType product = pool[pickIndex];
            int amount = UnityEngine.Random.Range(minAmountPerItem, maxAmountPerItem + 1);

            _list.Add(new ShoppingItem { product = product, amount = amount });

            pool.RemoveAt(pickIndex);
            if (pool.Count == 0) break;
        }
    }

    /// <summary>
    /// Fallback: generează lista direct din valorile enum-ului ProductType,
    /// ignorând ProductType.None.
    /// </summary>
    private void GenerateShoppingListFromEnum()
    {
        var allTypes = new List<ProductType>();
        foreach (ProductType pt in Enum.GetValues(typeof(ProductType)))
        {
            if (pt != ProductType.None)
                allTypes.Add(pt);
        }

        if (allTypes.Count == 0) return;

        int itemCount = UnityEngine.Random.Range(minItems, maxItems + 1);
        itemCount = Mathf.Min(itemCount, allTypes.Count);

        for (int i = 0; i < itemCount; i++)
        {
            int pickIndex = UnityEngine.Random.Range(0, allTypes.Count);
            ProductType product = allTypes[pickIndex];
            int amount = UnityEngine.Random.Range(minAmountPerItem, maxAmountPerItem + 1);

            _list.Add(new ShoppingItem { product = product, amount = amount });

            allTypes.RemoveAt(pickIndex);
        }
    }

    // =========================================================================
    //  FLOW PRINCIPAL
    // =========================================================================

    private void GoNextItemOrCheckout()
    {
        if (_registry == null)
        {
            Debug.LogError("[CustomerAI] Registry lipsește! Nu pot cumpăra nimic.");
            LeaveStore();
            return;
        }

        // Sari peste iteme deja finalizate (amount == 0)
        while (_currentIndex < _list.Count && _list[_currentIndex].amount <= 0)
            _currentIndex++;

        // Am terminat lista → verifică dacă a luat ceva
        if (_currentIndex >= _list.Count)
        {
            if (_basket.Count == 0)
            {
                Debug.Log($"[CustomerAI] {name} - Lista terminată dar coșul e gol. Plec fără să plătesc.");
                LeaveStore();
            }
            else
            {
                Debug.Log($"[CustomerAI] {name} - Lista terminată, merg la casă.");
                GoToRegister();
            }
            return;
        }

        var item = _list[_currentIndex];

        // Caută rafturi DESTINATE acestui produs, fără să verifice stocul.
        // Clientul nu știe dinainte ce e în stoc — va descoperi când ajunge la raft.
        var shelves = _registry.GetShelvesForProduct(item.product);
        if (shelves.Count == 0)
        {
            // Nu există niciun raft pentru acest produs în magazin → sari peste item
            Debug.Log($"[CustomerAI] {name} - Nu există raft pentru {item.product} în magazin. Sar peste.");
            NotifyProductNotFound(item.product);
            _currentIndex++;
            StartCoroutine(GoNextItemNextFrame());
            return;
        }

        _targetShelf = GetClosestShelf(shelves);

        if (_targetShelf == null)
        {
            _currentIndex++;
            StartCoroutine(GoNextItemNextFrame());
            return;
        }

        Debug.Log($"[CustomerAI] {name} - Merg la raftul pentru {item.product}.");

        SetDestination(_targetShelf.GetStandPosition());
        CurrentState = State.GoingToShelf;
    }

    private IEnumerator GoNextItemNextFrame()
    {
        yield return null; // Așteptăm un frame pentru a evita stack overflow în recursivitate
        GoNextItemOrCheckout();
    }

    private void TryTakeFromShelf()
    {
        if (_targetShelf == null)
        {
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        var item = _list[_currentIndex];

        // Clientul a ajuns la raft și vede că produsul nu mai este
        if (_targetShelf.slot1Product != item.product || _targetShelf.slot1Stock <= 0)
        {
            Debug.Log($"[CustomerAI] {name} - Raftul pentru {item.product} e gol. Renunț la acest item.");
            NotifyProductNotFound(item.product);
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        int taken = _targetShelf.TakeProduct(item.amount);
        if (taken > 0)
        {
            if (_basket.ContainsKey(item.product))
                _basket[item.product] += taken;
            else
                _basket[item.product] = taken;

            item.amount -= taken;
            _list[_currentIndex] = item;

            Debug.Log($"[CustomerAI] {name} - Am luat {taken}x {item.product}. " +
                      $"Mai vreau: {item.amount}");
        }

        // Trece la itemul următor indiferent dacă a luat tot sau nu
        // (clientul nu știe că mai există alt raft cu același produs)
        _currentIndex++;
        GoNextItemOrCheckout();
    }

    // =========================================================================
    //  CASA DE MARCAT
    // =========================================================================

    private void GoToRegister()
    {
        // Folosim registry dacă e disponibil, altfel FindObjectsByType ca fallback
        List<CashRegisterQueue> registers;
        if (_registry != null)
            registers = _registry.GetAllCashRegisterQueues();
        else
            registers = new List<CashRegisterQueue>(
                FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None));

        if (registers == null || registers.Count == 0)
        {
            Debug.LogWarning($"[CustomerAI] {name} - Nu există case de marcat! Plec.");
            LeaveStore();
            return;
        }

        // Alege casa cu coada cea mai scurtă; la egalitate, cea mai apropiată
        CashRegisterQueue best = null;
        int bestCount = int.MaxValue;
        float bestDist = float.MaxValue;

        foreach (var r in registers)
        {
            if (r == null) continue;
            if (r.QueueCount >= r.MaxQueueSize) continue; // sări peste casele pline

            int qc = r.QueueCount;

            // Distanța calculată față de primul spot din coadă (unde s-ar așeza clientul),
            // nu față de poziția angajatului/casei
            Vector3 queuePos = r.GetNextQueuePosition();
            float d = Vector3.Distance(transform.position, queuePos);

            if (qc < bestCount || (qc == bestCount && d < bestDist))
            {
                bestCount = qc;
                bestDist = d;
                best = r;
            }
        }

        // Dacă toate casele sunt pline, alege oricum cea mai puțin aglomerată
        if (best == null)
        {
            foreach (var r in registers)
            {
                if (r == null) continue;
                int qc = r.QueueCount;
                float d = Vector3.Distance(transform.position, r.transform.position);
                if (qc < bestCount || (qc == bestCount && d < bestDist))
                {
                    bestCount = qc; bestDist = d; best = r;
                }
            }
        }

        if (best == null)
        {
            Debug.LogWarning($"[CustomerAI] {name} - Nu am găsit nicio casă disponibilă. Plec.");
            LeaveStore();
            return;
        }

        _targetRegister = best;

        Debug.Log($"[CustomerAI] {name} - Merg spre casa de marcat: {best.name}");

        // ── FIX PRINCIPAL: setăm GoingToRegister și navigăm spre casă ──────
        // NU intrăm în coadă aici! Intrăm în coadă abia când ajungem (în Update).
        SetDestination(_targetRegister.transform.position);
        CurrentState = State.GoingToRegister;
    }

    /// <summary>
    /// Apelat din Update când clientul a ajuns la casa de marcat.
    /// Încearcă să intre în coadă; dacă nu reușește, caută altă casă.
    /// </summary>
    private void TryJoinQueue()
    {
        if (_targetRegister == null)
        {
            GoToRegister();
            return;
        }

        if (_targetRegister.TryEnqueue(this))
        {
            Debug.Log($"[CustomerAI] {name} - Am intrat în coada la {_targetRegister.name}");
            CurrentState = State.InQueue;
            // CashRegisterQueue.UpdateQueueDestinations() va apela SetQueueTarget() automat
        }
        else
        {
            // Coada e plină → încearcă altă casă
            Debug.Log($"[CustomerAI] {name} - Coada la {_targetRegister.name} e plină. Caut alta.");
            _targetRegister = null;
            GoToRegister();
        }
    }

    // =========================================================================
    //  NOTIFICĂRI
    // =========================================================================

    // Static: cooldown-ul e partajat între TOȚI clienții pentru același produs
    private static readonly Dictionary<ProductType, float> _notificationCooldowns = new();
    private const float NotificationCooldown = 15f; // secunde între notificări pentru același produs

    private void NotifyProductNotFound(ProductType product)
    {
        if (_eventBus == null) return;

        float now = Time.time;
        if (_notificationCooldowns.TryGetValue(product, out float lastTime) &&
            now - lastTime < NotificationCooldown)
            return; // prea devreme, ignorăm

        _notificationCooldowns[product] = now;

        _eventBus.Publish(new ShowNotificationEvent(
            title: "Produs indisponibil",
            message: $"Un client a căutat {product} dar nu l-a găsit în magazin.",
            type: NotificationType.Warning,
            duration: 5f
        ));
    }

    // =========================================================================
    //  HELPERS NAVIGAȚIE
    // =========================================================================

    private void SetDestination(Vector3 destination)
    {
        _agent.SetDestination(destination);
        _destinationSet = true;
        _destinationSetTime = Time.time;
    }

    /// <summary>
    /// Garantează că NavMeshAgent-ul a avut timp să calculeze path-ul
    /// înainte să verificăm dacă am ajuns la destinație.
    /// </summary>
    private bool HasDestinationSettled()
    {
        if (!_destinationSet) return false;
        return Time.time >= _destinationSetTime + DestinationSettleDelay;
    }

    public bool IsAtDestination()
    {
        if (_agent.pathPending) return false;
        if (_agent.remainingDistance == Mathf.Infinity) return false;
        return _agent.remainingDistance <= Mathf.Max(arriveDistance, _agent.stoppingDistance);
    }

    private WorkStation GetClosestShelf(List<WorkStation> shelves)
    {
        WorkStation best = null;
        float bestD = float.MaxValue;

        foreach (var s in shelves)
        {
            if (s == null) continue;
            float d = Vector3.Distance(transform.position, s.GetStandPosition());
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    // =========================================================================
    //  CHECKOUT & PLECARE
    // =========================================================================

    public void SetQueueTarget(Transform t)
    {
        _queueTarget = t;
        if (_queueTarget != null)
            _agent.SetDestination(_queueTarget.position);
    }

    public void OnCheckoutComplete()
    {
        Debug.Log($"[CustomerAI] {name} - Plătit! Total coș: {_basket.Count} produse.");
        LeaveStore();
    }

    private void LeaveStore()
    {
        if (_exitPoint != null)
        {
            SetDestination(_exitPoint.position);
            CurrentState = State.Leaving;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // =========================================================================
    //  PREȚURI
    // =========================================================================

    public int CalculateTotalPriceRON()
    {
        int total = 0;

        foreach (var kv in _basket)
        {
            if (ProductDataBase.Instance != null &&
                ProductDataBase.Instance.TryGetSellPrice(kv.Key, out float price))
            {
                total += Mathf.RoundToInt(price) * kv.Value;
            }
            else
            {
                total += 10 * kv.Value; // fallback
            }
        }

        return total;
    }

    // =========================================================================
    //  METODA VECHE (kept for backwards compat, nu mai e folosită intern)
    // =========================================================================

    private bool TryGetUnitPriceRON(ProductType product, IEconomyService economyService, out int price)
    {
        price = 0;
        if (economyService == null) return false;

        var mdProp = economyService.GetType().GetProperty("MarketData",
            BindingFlags.Public | BindingFlags.Instance);
        if (mdProp == null) return false;

        object marketDataObj = mdProp.GetValue(economyService);
        if (marketDataObj == null) return false;

        var idx = marketDataObj.GetType().GetProperty("Item");
        if (idx == null) return false;

        object econ = null;
        try { econ = idx.GetValue(marketDataObj, new object[] { product }); }
        catch { return false; }

        if (econ == null) return false;

        string[] candidates = { "sellPrice", "SellPrice", "price", "Price", "basePrice", "BasePrice" };
        foreach (var candidateName in candidates)
        {
            var pProp = econ.GetType().GetProperty(candidateName,
                BindingFlags.Public | BindingFlags.Instance);
            if (pProp != null && pProp.PropertyType == typeof(int))
            {
                price = (int)pProp.GetValue(econ);
                return price > 0;
            }

            var fField = econ.GetType().GetField(candidateName,
                BindingFlags.Public | BindingFlags.Instance);
            if (fField != null && fField.FieldType == typeof(int))
            {
                price = (int)fField.GetValue(econ);
                return price > 0;
            }
        }

        return false;
    }
}