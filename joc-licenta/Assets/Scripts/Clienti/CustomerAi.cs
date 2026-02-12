using System;
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
    [SerializeField] private float arriveDistance = 0.8f;

    private NavMeshAgent _agent;
    private Transform _exitPoint;
    private WorkStationRegistry _registry;

    [SerializeField] private List<ShoppingItem> _list = new();
    private int _currentIndex = 0;

    // ce a cumpărat efectiv (produs -> cantitate)
    private readonly Dictionary<ProductType, int> _basket = new();

    private WorkStation _targetShelf;
    private CashRegisterQueue _targetRegister;
    private Transform _queueTarget;

    public State CurrentState { get; private set; } = State.Idle;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public void Initialize(WorkStationRegistry registry, Transform exitPoint)
    {
        _registry = registry;
        _exitPoint = exitPoint;

        GenerateShoppingList();
        _currentIndex = 0;

        GoNextItemOrCheckout();
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case State.GoingToShelf:
                if (IsAtDestination())
                {
                    CurrentState = State.TakingProduct;
                    TryTakeFromShelf();
                }
                break;

            case State.GoingToRegister:
                // când ajunge, încearcă să se bage în coadă (dacă nu a reușit din prima)
                if (IsAtDestination())
                {
                    if (_targetRegister != null && _targetRegister.TryEnqueue(this))
                        CurrentState = State.InQueue;
                }
                break;

            case State.InQueue:
                if (_queueTarget != null)
                    _agent.SetDestination(_queueTarget.position);
                break;

            case State.Leaving:
                // optional: destroy când a ieșit
                if (_exitPoint != null && Vector3.Distance(transform.position, _exitPoint.position) <= arriveDistance)
                    Destroy(gameObject);
                break;
        }
    }

    private void GenerateShoppingList()
    {
        _list.Clear();

        var db = ProductDataBase.Instance;
        if (db == null)
            return;

        var pool = db.GetAllProductTypes();
        if (pool.Count == 0)
            return;

        int itemCount = UnityEngine.Random.Range(minItems, maxItems + 1);
        itemCount = Mathf.Min(itemCount, pool.Count);

        for (int i = 0; i < itemCount; i++)
        {
            int pickIndex = UnityEngine.Random.Range(0, pool.Count);
            ProductType product = pool[pickIndex];

            int amount = UnityEngine.Random.Range(minAmountPerItem, maxAmountPerItem + 1);

            _list.Add(new ShoppingItem { product = product, amount = amount });

            // opțional: fără duplicate
            pool.RemoveAt(pickIndex);
            if (pool.Count == 0) break;
        }
    }


    private void GoNextItemOrCheckout()
    {
        if (_registry == null)
        {
            Debug.LogError("[CustomerAI] Registry missing!");
            return;
        }

        // sari peste iteme care deja sunt 0
        while (_currentIndex < _list.Count && _list[_currentIndex].amount <= 0)
            _currentIndex++;

        if (_currentIndex >= _list.Count)
        {
            GoToRegister();
            return;
        }

        var item = _list[_currentIndex];

        // caută rafturi cu produs în stoc
        var shelves = _registry.GetShelvesWithProductInStock(item.product);
        if (shelves.Count == 0)
        {
            // nu există în stoc => renunță la item
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        // alege raftul cel mai apropiat
        _targetShelf = GetClosestShelf(shelves);

        if (_targetShelf == null)
        {
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        Vector3 dest = _targetShelf.GetStandPosition();
        _agent.SetDestination(dest);
        CurrentState = State.GoingToShelf;
    }

    private WorkStation GetClosestShelf(List<WorkStation> shelves)
    {
        WorkStation best = null;
        float bestD = float.MaxValue;

        foreach (var s in shelves)
        {
            if (s == null) continue;
            float d = Vector3.Distance(transform.position, s.GetStandPosition());
            if (d < bestD)
            {
                bestD = d;
                best = s;
            }
        }
        return best;
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

        // re-check, alt client poate fi luat înainte
        if (_targetShelf.slot1Product != item.product || _targetShelf.slot1Stock <= 0)
        {
            // caută alt raft
            GoNextItemOrCheckout();
            return;
        }

        int taken = _targetShelf.TakeProduct(item.amount);
        if (taken > 0)
        {
            if (_basket.ContainsKey(item.product)) _basket[item.product] += taken;
            else _basket[item.product] = taken;

            item.amount -= taken;
            _list[_currentIndex] = item;
        }

        // dacă nu a luat tot, încearcă din nou alt raft; altfel trece la următorul item
        if (_list[_currentIndex].amount > 0)
        {
            GoNextItemOrCheckout();
        }
        else
        {
            _currentIndex++;
            GoNextItemOrCheckout();
        }
    }

    private void GoToRegister()
    {
        var registers = FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None);

        CashRegisterQueue best = null;
        int bestCount = int.MaxValue;
        float bestDist = float.MaxValue;

        foreach (var r in registers)
        {
            if (r == null) continue;

            int qc = r.QueueCount;
            float d = Vector3.Distance(transform.position, r.transform.position);

            if (qc < bestCount || (qc == bestCount && d < bestDist))
            {
                bestCount = qc;
                bestDist = d;
                best = r;
            }
        }

        _targetRegister = best;

        if (_targetRegister == null)
        {
            LeaveStore();
            return;
        }

        // 🔥 IMPORTANT: intră în coadă imediat
        if (_targetRegister.TryEnqueue(this))
        {
            CurrentState = State.InQueue;
            // Queue-ul va apela SetQueueTarget(...) și agentul va primi destinația corectă
            return;
        }

        // Dacă nu poate intra (coada plină), fie încearcă altă casă, fie pleacă.
        // Simplu: pleacă.
        LeaveStore();
    }


    public void SetQueueTarget(Transform t)
    {
        _queueTarget = t;
        if (_queueTarget != null)
            _agent.SetDestination(_queueTarget.position);
    }

    public bool IsAtDestination()
    {
        if (_agent.pathPending) return false;
        if (_agent.remainingDistance == Mathf.Infinity) return false;
        return _agent.remainingDistance <= Mathf.Max(arriveDistance, _agent.stoppingDistance);
    }

    public void OnCheckoutComplete()
    {
        LeaveStore();
    }

    private void LeaveStore()
    {
        if (_exitPoint != null)
        {
            _agent.SetDestination(_exitPoint.position);
            CurrentState = State.Leaving;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // === PREȚURI ===
    public int CalculateTotalPriceRON()
    {
        int total = 0;

        foreach (var kv in _basket)
        {
            if (ProductDataBase.Instance != null &&
                ProductDataBase.Instance.TryGetSellPrice(kv.Key, out float price))
            {
                int unitPrice = Mathf.RoundToInt(price);
                // alternative: FloorToInt / CeilToInt

                total += unitPrice * kv.Value;
            }
            else
            {
                total += 10 * kv.Value; // fallback
            }
        }

        return total;
    }


    private bool TryGetUnitPriceRON(ProductType product, IEconomyService economyService, out int price)
    {
        price = 0;
        if (economyService == null) return false;

        // economyService.MarketData probabil e Dictionary<ProductType, ProductEconomics>
        var mdProp = economyService.GetType().GetProperty("MarketData", BindingFlags.Public | BindingFlags.Instance);
        if (mdProp == null) return false;

        object marketDataObj = mdProp.GetValue(economyService);
        if (marketDataObj == null) return false;

        // Try get indexer: marketData[product]
        var idx = marketDataObj.GetType().GetProperty("Item");
        if (idx == null) return false;

        object econ = null;
        try
        {
            econ = idx.GetValue(marketDataObj, new object[] { product });
        }
        catch { return false; }

        if (econ == null) return false;

        // caută câmpuri/proprietăți comune
        string[] candidates = { "sellPrice", "SellPrice", "price", "Price", "basePrice", "BasePrice" };

        foreach (var name in candidates)
        {
            var pProp = econ.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (pProp != null && pProp.PropertyType == typeof(int))
            {
                price = (int)pProp.GetValue(econ);
                return price > 0;
            }

            var fField = econ.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (fField != null && fField.FieldType == typeof(int))
            {
                price = (int)fField.GetValue(econ);
                return price > 0;
            }
        }

        return false;
    }
}
