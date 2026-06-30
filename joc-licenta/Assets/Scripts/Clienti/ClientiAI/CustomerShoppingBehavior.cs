using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionează logica de cumpărare a clientului:
/// generarea listei, navigarea între rafturi, verificarea prețului/bugetului
/// și popularea coșului de cumpărături.
///
/// Depinde de CustomerNavigationHelper pentru deplasare și
/// comunică cu CustomerAI prin evenimentele OnReadyForCheckout / OnLeaveEmpty.
/// </summary>
[RequireComponent(typeof(CustomerNavigationHelper))]
public class CustomerShoppingBehavior : MonoBehaviour
{
    // =========================================================================
    //  TIPURI
    // =========================================================================

    [Serializable]
    public struct ShoppingItem
    {
        public ProductType product;
        public int amount;
    }

    // =========================================================================
    //  INSPECTOR
    // =========================================================================

    [Header("Parametri Listă")]
    [SerializeField] private int minItems = 1;
    [SerializeField] private int maxItems = 4;
    [SerializeField] private int minAmountPerItem = 1;
    [SerializeField] private int maxAmountPerItem = 3;

    // =========================================================================
    //  EVENTS – ascultate de CustomerAI
    // =========================================================================

    /// <summary>Clientul a terminat lista și are produse în coș → mergi la casă.</summary>
    public event Action OnReadyForCheckout;

    /// <summary>Clientul a terminat lista dar coșul e gol → pleacă fără să plătești.</summary>
    public event Action OnLeaveEmpty;

    // =========================================================================
    //  STARE INTERNĂ
    // =========================================================================

    [SerializeField] private List<ShoppingItem> _list = new();
    private int _currentIndex = 0;

    // Ce a cumpărat efectiv (produs → cantitate)
    private readonly Dictionary<ProductType, int> _basket = new();

    private int _budget;
    private WorkStationRegistry _registry;
    private IEventBus _eventBus;
    private CustomerNavigationHelper _nav;

    // Cache servicii – populat o singură dată în Initialize pentru a evita
    // apeluri repetate la ServiceLocator în fiecare frame/produs.
    private IEconomyService _economyService;
    private InflationManager _inflationManager;

    // Cache static pentru valorile enum-ului ProductType –
    // Enum.GetValues() alocă array nou la fiecare apel.
    private static List<ProductType> _cachedEnumProductTypes;

    // =========================================================================
    //  NOTIFICĂRI – cooldown partajat între toți clienții
    // =========================================================================

    private static readonly Dictionary<ProductType, float> _notificationCooldowns = new();
    private const float NotificationCooldown = 15f;

    // =========================================================================
    //  INIȚIALIZARE
    // =========================================================================

    private void Awake()
    {
        _nav = GetComponent<CustomerNavigationHelper>();
    }

    /// <summary>
    /// Apelat de CustomerAI după spawn.
    /// </summary>
    public void Initialize(WorkStationRegistry registry, int startingBudget, IEventBus eventBus)
    {
        _registry = registry;
        _budget = startingBudget;
        _eventBus = eventBus;

        // Cache servicii o singură dată
        ServiceLocator.Instance.TryGet(out _economyService);
        ServiceLocator.Instance.TryGet(out _inflationManager);

        GenerateShoppingList();
        _currentIndex = 0;

        Debug.Log($"[ShoppingBehavior] {name} – Buget: {_budget} RON, Listă: {_list.Count} produse.");
    }

    // =========================================================================
    //  ACCES PUBLIC (folosit de CustomerAI & CashRegister)
    // =========================================================================

    public Dictionary<ProductType, int> Basket => _basket;

    /// <summary>Calculează totalul coșului în RON, aplicând inflația curentă.</summary>
    public int CalculateTotalPriceRON()
    {
        int total = 0;
        foreach (var kv in _basket)
            total += GetProductUnitPrice(kv.Key) * kv.Value;
        return total;
    }

    /// <summary>Pornește fluxul de cumpărare (primul item din listă).</summary>
    public void StartShopping() => GoNextItemOrCheckout();

    // =========================================================================
    //  GENERARE LISTĂ
    // =========================================================================

    private void GenerateShoppingList()
    {
        _list.Clear();

        var db = ProductDataBase.Instance;
        if (db == null)
        {
            Debug.LogError("[ShoppingBehavior] ProductDataBase.Instance este NULL! Folosim fallback din enum.");
            GenerateShoppingListFromEnum();
            return;
        }

        var pool = db.GetAllProductTypes();
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("[ShoppingBehavior] ProductDataBase nu returnează niciun produs! Fallback enum.");
            GenerateShoppingListFromEnum();
            return;
        }

        int itemCount = Mathf.Min(UnityEngine.Random.Range(minItems, maxItems + 1), pool.Count);

        for (int i = 0; i < itemCount; i++)
        {
            int pickIndex = UnityEngine.Random.Range(0, pool.Count);
            _list.Add(new ShoppingItem
            {
                product = pool[pickIndex],
                amount = UnityEngine.Random.Range(minAmountPerItem, maxAmountPerItem + 1)
            });

            pool.RemoveAt(pickIndex);
            if (pool.Count == 0) break;
        }
    }

    /// <summary>
    /// Fallback: folosește enum-ul ProductType direct dacă ProductDataBase lipsește.
    /// Lista de valori enum este cache-uită static pentru a evita alocări repetate.
    /// </summary>
    private void GenerateShoppingListFromEnum()
    {
        if (_cachedEnumProductTypes == null)
        {
            _cachedEnumProductTypes = new List<ProductType>();
            foreach (ProductType pt in Enum.GetValues(typeof(ProductType)))
                if (pt != ProductType.None)
                    _cachedEnumProductTypes.Add(pt);
        }

        if (_cachedEnumProductTypes.Count == 0) return;

        // Copiem lista cache-uită ca să o putem shuffle local fără a modifica originalul
        var allTypes = new List<ProductType>(_cachedEnumProductTypes);
        int itemCount = Mathf.Min(UnityEngine.Random.Range(minItems, maxItems + 1), allTypes.Count);

        for (int i = 0; i < itemCount; i++)
        {
            int pickIndex = UnityEngine.Random.Range(0, allTypes.Count);
            _list.Add(new ShoppingItem
            {
                product = allTypes[pickIndex],
                amount = UnityEngine.Random.Range(minAmountPerItem, maxAmountPerItem + 1)
            });
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
            Debug.LogError("[ShoppingBehavior] Registry lipsește! Nu pot cumpăra nimic.");
            OnLeaveEmpty?.Invoke();
            return;
        }

        // Sari peste itemele deja finalizate (amount == 0)
        while (_currentIndex < _list.Count && _list[_currentIndex].amount <= 0)
            _currentIndex++;

        if (_currentIndex >= _list.Count)
        {
            if (_basket.Count == 0)
            {
                Debug.Log($"[ShoppingBehavior] {name} – Coș gol. Plec fără să plătesc.");
                OnLeaveEmpty?.Invoke();
            }
            else
            {
                Debug.Log($"[ShoppingBehavior] {name} – Listă terminată. Merg la casă.");
                OnReadyForCheckout?.Invoke();
            }
            return;
        }

        var item = _list[_currentIndex];
        var shelves = _registry.GetShelvesForProduct(item.product);

        if (shelves.Count == 0)
        {
            Debug.Log($"[ShoppingBehavior] {name} – Niciun raft pentru {item.product}. Sar peste.");
            NotifyProductNotFound(item.product);
            _currentIndex++;
            StartCoroutine(GoNextItemNextFrame());
            return;
        }

        var targetShelf = _nav.GetClosestShelf(shelves);
        if (targetShelf == null)
        {
            _currentIndex++;
            StartCoroutine(GoNextItemNextFrame());
            return;
        }

        Debug.Log($"[ShoppingBehavior] {name} – Merg la raftul pentru {item.product}.");
        _nav.NavigateToShelf(targetShelf, () => TryTakeFromShelf(targetShelf));
    }

    private IEnumerator GoNextItemNextFrame()
    {
        yield return null; // Evităm stack overflow în recursivitate
        GoNextItemOrCheckout();
    }

    // =========================================================================
    //  LUARE PRODUS DE PE RAFT
    // =========================================================================

    private void TryTakeFromShelf(WorkStation shelf)
    {
        if (shelf == null)
        {
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        var item = _list[_currentIndex];

        // Raftul e gol sau nu are produsul dorit
        if (shelf.slotProduct != item.product || shelf.slotStock <= 0)
        {
            Debug.Log($"[ShoppingBehavior] {name} – Raftul e gol pentru {item.product}. Sar peste.");
            NotifyProductNotFound(item.product);
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        int unitPrice = GetProductUnitPrice(item.product);

        // ── 1. Verificare preț față de concurență ────────────────────────────
        float buyChance = CalculateBuyChance(item.product, unitPrice);

        if (UnityEngine.Random.value > buyChance)
        {
            Debug.Log($"[ShoppingBehavior] {name} – Refuză {item.product}: prea scump " +
                      $"(șansă cumpărare: {buyChance * 100:F0}%)");
            _eventBus?.Publish(new ProductPricedTooHighEvent(item.product));

            // Delegăm emote-ul către CustomerAI prin event sau direct
            GetComponent<CustomerEmoteController>()?.ShowAngryEmote();

            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        // ── 2. Verificare buget ───────────────────────────────────────────────
        int affordableAmount = Mathf.FloorToInt((float)_budget / unitPrice);
        int desiredAmount = Mathf.Min(item.amount, affordableAmount);

        if (desiredAmount <= 0)
        {
            Debug.Log($"[ShoppingBehavior] {name} – Insuficient buget pentru {item.product} " +
                      $"(preț: {unitPrice}, buget rămas: {_budget}). Sar peste.");
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        // ── 3. Cumpărare efectivă ─────────────────────────────────────────────
        int taken = shelf.TakeProduct(desiredAmount);

        if (taken > 0)
        {
            int cost = taken * unitPrice;
            _budget -= cost;

            _basket.TryGetValue(item.product, out int existing);
            _basket[item.product] = existing + taken;

            item.amount -= taken;
            _list[_currentIndex] = item;

            Debug.Log($"[ShoppingBehavior] {name} – Luat {taken}x {item.product} " +
                      $"(cost: {cost} RON, buget rămas: {_budget} RON).");
        }

        if (_budget <= 0)
        {
            Debug.Log($"[ShoppingBehavior] {name} – Buget epuizat! Merg direct la casă.");
            _currentIndex = _list.Count; // Sari la final
        }
        else
        {
            _currentIndex++;
        }

        GoNextItemOrCheckout();
    }

    // =========================================================================
    //  PREȚURI & PIAȚĂ
    // =========================================================================

    /// <summary>
    /// Returnează prețul unitar al unui produs aplicând inflația curentă.
    /// Folosește serviciile cache-uite din Initialize – nu mai apelează
    /// ServiceLocator la fiecare calcul de preț.
    /// </summary>
    private int GetProductUnitPrice(ProductType productType)
    {
        float inflation = _inflationManager != null ? _inflationManager.CurrentInflation : 1.0f;

        if (_economyService != null && _economyService.TryGetProductData(productType, out var data))
            return Mathf.RoundToInt(data.sellingPrice * inflation);

        if (ProductDataBase.Instance != null &&
            ProductDataBase.Instance.TryGetSellPrice(productType, out float basePrice))
            return Mathf.RoundToInt(basePrice * inflation);

        return 10; // fallback
    }

    /// <summary>
    /// Returnează prețul "corect" (fără markup) al produsului, folosit ca referință
    /// pentru comparația cu concurenții.
    /// </summary>
    private float GetFairPrice(ProductType productType)
    {
        float inflation = _inflationManager != null ? _inflationManager.CurrentInflation : 1.0f;

        if (ProductDataBase.Instance != null &&
            ProductDataBase.Instance.TryGetSellPrice(productType, out float basePrice))
            return basePrice * inflation;

        return 10f; // fallback
    }

    /// <summary>
    /// Calculează șansa de cumpărare [0.05, 1.0] luând în calcul
    /// raportul preț/piață și eventualul modifier de la CompetitiveMarketManager.
    /// </summary>
    private float CalculateBuyChance(ProductType product, int unitPrice)
    {
        float buyChance = 1.0f;

        if (CompetitiveMarketManager.Instance != null)
        {
            float marketModifier = CompetitiveMarketManager.Instance.GetBuyChanceModifier(product, unitPrice);
            buyChance *= marketModifier;

            if (marketModifier < 0.8f)
                Debug.Log($"[ShoppingBehavior] {name} – Concurentul e mai ieftin la {product}! " +
                          $"Șansă cumpărare: {buyChance * 100:F0}%");
        }

        return Mathf.Clamp(buyChance, 0.05f, 1.0f);
    }

    // =========================================================================
    //  NOTIFICĂRI
    // =========================================================================

    private void NotifyProductNotFound(ProductType product)
    {
        if (_eventBus == null) return;

        float now = Time.time;
        if (_notificationCooldowns.TryGetValue(product, out float lastTime) &&
            now - lastTime < NotificationCooldown)
            return;

        _notificationCooldowns[product] = now;

        _eventBus.Publish(new ShowNotificationEvent(
            title: "Produs indisponibil",
            message: $"Un client a căutat {product} dar nu l-a găsit în magazin.",
            type: NotificationType.Warning,
            duration: 5f
        ));
    }

    // =========================================================================
    //  CLEANUP
    // =========================================================================

    private void OnDestroy()
    {
        // Curățăm cooldown-urile statice la distrugerea scenei
        // pentru a evita memory leak-uri între sesiuni de play.
        // Apelăm doar dacă suntem ultimul client activ.
        if (FindObjectsByType<CustomerShoppingBehavior>(FindObjectsSortMode.None).Length <= 1)
            _notificationCooldowns.Clear();
    }
}