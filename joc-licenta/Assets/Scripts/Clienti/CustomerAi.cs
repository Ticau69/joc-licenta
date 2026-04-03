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

    [Header("Checkout")]
    [SerializeField] private float cashierDetectRadius = 1.5f;
    [SerializeField] private float checkoutCooldown = 0.25f; // Pauza între clienți

    [Header("Emotes / Feedback Vizual")]
    [SerializeField] private SpriteRenderer emoteRenderer;
    [SerializeField] private Sprite angryPriceSprite;

    // --- ADAUGĂ ASTA ---
    [SerializeField] private float checkoutDuration = 2.0f; // Cât durează plata efectivă
    private bool _isProcessing = false;

    private NavMeshAgent _agent;
    private Transform _exitPoint;
    private Animator _animator;
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
    private int _budget;

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
        _animator = GetComponent<Animator>();
    }

    public void Initialize(WorkStationRegistry registry, Transform exitPoint, int startingBudget)
    {
        _registry = registry;
        _exitPoint = exitPoint;
        _budget = startingBudget; // Salvăm bugetul primit
        ServiceLocator.Instance.TryGet(out _eventBus);

        GenerateShoppingList();
        _currentIndex = 0;

        Debug.Log($"[CustomerAI] {name} - Spawnat cu buget: {_budget} RON. Listă: {_list.Count} iteme.");

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
        UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        if (_animator == null) return;

        // 1. ANIMAȚIA DE MERS (Se bazează automat pe viteza agentului)
        bool isMoving = _agent.velocity.magnitude > 0.1f;
        _animator.SetBool("isWalking", isMoving);

        // 2. ANIMAȚII DE STARE
        // REPARAȚIA: Folosim "State.NumeStare", nu variabila!
        switch (currentState)
        {
            case State.TakingProduct:
                // Când este la raft și ia produsul
                _animator.SetBool("isGrabbing", true);
                _animator.SetBool("isRethinking", false);
                break;

            case State.Idle:
                // Când stă degeaba sau se gândește
                _animator.SetBool("isGrabbing", false);
                _animator.SetBool("isRethinking", true);
                break;

            case State.GoingToShelf:
            case State.GoingToRegister:
            case State.InQueue:
            case State.Leaving:
                // Pentru toate celelalte stări (deplasare sau așteptare), oprim acțiunile mâinilor
                _animator.SetBool("isGrabbing", false);
                _animator.SetBool("isRethinking", false);
                break;
        }
    }

    private void ShowAngryEmote()
    {
        if (emoteRenderer == null)
        {
            Debug.LogWarning($"[CustomerAI] {name} nu are emoteRenderer asignat!");
            return;
        }

        // Opțional: setăm sprite-ul specific (în caz că pe viitor vei avea și emote-uri de fericire)
        if (angryPriceSprite != null)
        {
            emoteRenderer.sprite = angryPriceSprite;
        }

        // Oprim orice altă animație veche și o pornim pe cea nouă
        StopAllCoroutines();
        StartCoroutine(AnimateEmoteRoutine());
    }

    private IEnumerator AnimateEmoteRoutine()
    {
        // 1. Aprindem iconița
        emoteRenderer.enabled = true;
        Transform emoteTransform = emoteRenderer.transform;

        // 2. Animație de "Pop-up" (Mărire de la 0 la 1 pentru un efect de "Juice")
        float popDuration = 0.2f;
        float elapsed = 0f;
        Vector3 finalScale = new Vector3(1f, 1f, 1f); // Ajustează dacă iconița ta trebuie să fie mai mică (ex: 0.5f)

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            // Lerp face tranziția fluidă între mărimea 0 și mărimea normală
            emoteTransform.localScale = Vector3.Lerp(Vector3.zero, finalScale, elapsed / popDuration);
            yield return null;
        }
        emoteTransform.localScale = finalScale; // Ne asigurăm că ajunge exact la mărimea finală

        // 3. Lăsăm iconița pe ecran 2.5 secunde ca jucătorul să o observe
        yield return new WaitForSeconds(2.5f);

        // 4. Stingem iconița (Clientul și-a vărsat nervii și pleacă)
        emoteRenderer.enabled = false;
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
    private int GetProductUnitPrice(ProductType productType)
    {
        bool hasEconomy = ServiceLocator.Instance.TryGet(out IEconomyService economyService);
        bool hasInflation = ServiceLocator.Instance.TryGet(out InflationManager inflationManager);
        float currentInflation = hasInflation ? inflationManager.CurrentInflation : 1.0f;

        if (hasEconomy && economyService.TryGetProductData(productType, out var data))
        {
            return Mathf.RoundToInt(data.sellingPrice * currentInflation);
        }
        else if (ProductDataBase.Instance != null && ProductDataBase.Instance.TryGetSellPrice(productType, out float basePrice))
        {
            return Mathf.RoundToInt(basePrice * currentInflation);
        }
        return 10; // Super-fallback
    }


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

    private float GetFairPrice(ProductType productType)
    {
        bool hasInflation = ServiceLocator.Instance.TryGet(out InflationManager inflationManager);
        float currentInflation = hasInflation ? inflationManager.CurrentInflation : 1.0f;

        if (ProductDataBase.Instance != null && ProductDataBase.Instance.TryGetSellPrice(productType, out float basePrice))
        {
            return basePrice * currentInflation;
        }
        return 10f; // Fallback
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

        if (_targetShelf.slot1Product != item.product || _targetShelf.slot1Stock <= 0)
        {
            Debug.Log($"[CustomerAI] {name} - Raftul e gol. Sar peste {item.product}.");
            NotifyProductNotFound(item.product);
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        int unitPrice = GetProductUnitPrice(item.product);

        float fairPrice = GetFairPrice(item.product);
        float markupRatio = (float)unitPrice / fairPrice;

        float buyChance = 1.0f;

        // Dacă jucătorul a scumpit produsul cu peste 20% (markup > 1.2f)
        if (markupRatio > 1.0f)
        {
            // Formula: Pentru fiecare 1% adăugat la preț, scade 1% din șansă.
            // Ex: markup 1.5 (150% din preț) -> buyChance devine 1.0 - 0.5 = 0.5 (50%)
            buyChance = 1.0f - (markupRatio - 1.0f);

            // Limităm șansa conform cerinței tale: Minim 5% (0.05f), Maxim 100% (1.0f)
            buyChance = Mathf.Clamp(buyChance, 0.05f, 1.0f);
        }

        if (UnityEngine.Random.value > buyChance)
        {
            Debug.Log($"[CustomerAI] {name} refuză {item.product} pentru că e prea scump! (Șansa calculată de a cumpăra era: {buyChance * 100:F0}%)");

            // Trimitem semnalul către inventar pentru alertă
            if (_eventBus != null) _eventBus.Publish(new ProductPricedTooHighEvent(item.product));

            // Arătăm iconița supărată
            ShowAngryEmote();

            _currentIndex++;
            GoNextItemOrCheckout();
            return; // Pleacă direct
        }

        // --- 2. VERIFICAREA DE BUGET (Sistemul tău existent) ---
        int affordableAmount = Mathf.FloorToInt((float)_budget / unitPrice);
        int desiredAmount = Mathf.Min(item.amount, affordableAmount);

        if (desiredAmount <= 0)
        {
            // Dacă a ajuns aici, prețul era ok (sau l-a tolerat), dar pur și simplu nu are bani.
            Debug.Log($"[CustomerAI] {name} - Nu am bani de {item.product}! Costă {unitPrice} dar mai am doar {_budget}. Sar peste.");
            _currentIndex++;
            GoNextItemOrCheckout();
            return;
        }

        // --- 3. CUMPĂRAREA EFECTIVĂ ---
        int taken = _targetShelf.TakeProduct(desiredAmount);

        if (taken > 0)
        {
            int cost = taken * unitPrice;
            _budget -= cost;

            if (_basket.ContainsKey(item.product))
                _basket[item.product] += taken;
            else
                _basket[item.product] = taken;

            item.amount -= taken;
            _list[_currentIndex] = item;

            Debug.Log($"[CustomerAI] {name} - Am luat {taken}x {item.product} (Cost: {cost} RON). Buget rămas: {_budget} RON");
        }

        if (_budget <= 0)
        {
            Debug.Log($"[CustomerAI] {name} - Am rămas falit! Abandonez restul listei și merg direct la casă.");
            _currentIndex = _list.Count;
        }
        else
        {
            _currentIndex++;
        }

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
        SetDestination(_targetRegister.GetNextQueuePosition());
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
        int totalBill = CalculateTotalPriceRON();
        Debug.Log($"[CustomerAI] {name} - Plătit {totalBill} RON.");

        // NOU: Înregistrăm vânzarea în managerul de finanțe
        if (FinanceManager.Instance != null)
        {
            FinanceManager.Instance.RegisterTransaction(TransactionCategory.Venituri_Vanzari, totalBill);
        }

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
            // Folosește aceeași funcție pe care a folosit-o să verifice la raft
            int pricePerUnit = GetProductUnitPrice(kv.Key);
            total += pricePerUnit * kv.Value;
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