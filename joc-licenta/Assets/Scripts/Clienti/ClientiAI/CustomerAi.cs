using System;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Orchestratorul principal al clientului.
/// Gestionează mașina de stări și delegă responsabilitățile către:
/// - CustomerShoppingBehavior  → logica de cumpărare
/// - CustomerNavigationHelper  → deplasare NavMesh
/// - CustomerEmoteController   → animații și emote-uri
///
/// Acest script nu conține logică de business — doar conectează componentele
/// și răspunde la tranziții de stare.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CustomerShoppingBehavior))]
[RequireComponent(typeof(CustomerNavigationHelper))]
[RequireComponent(typeof(CustomerEmoteController))]
public class CustomerAI : MonoBehaviour
{
    // =========================================================================
    //  STARE
    // =========================================================================

    public enum State
    {
        Idle,
        GoingToShelf,
        TakingProduct,
        GoingToRegister,
        InQueue,
        Leaving
    }

    [SerializeField] private State currentState;
    public State CurrentState
    {
        get => currentState;
        private set => currentState = value;
    }

    // =========================================================================
    //  INSPECTOR
    // =========================================================================

    [Header("Ieșire")]
    [SerializeField] private float arriveDistance = 1f;

    // =========================================================================
    //  REFERINȚE COMPONENTE
    // =========================================================================

    private CustomerShoppingBehavior _shopping;
    private CustomerNavigationHelper _nav;
    private CustomerEmoteController _emote;

    // =========================================================================
    //  STARE INTERNĂ
    // =========================================================================

    private Transform _exitPoint;
    private WorkStationRegistry _registry;
    private CashRegisterQueue _targetRegister;

    // =========================================================================
    //  INIT
    // =========================================================================

    private void Awake()
    {
        _shopping = GetComponent<CustomerShoppingBehavior>();
        _nav = GetComponent<CustomerNavigationHelper>();
        _emote = GetComponent<CustomerEmoteController>();
    }

    /// <summary>
    /// Apelat de CustomerSpawner după instantiere.
    /// </summary>
    public void Initialize(WorkStationRegistry registry, Transform exitPoint, int startingBudget)
    {
        _registry = registry;
        _exitPoint = exitPoint;

        // Abonăm evenimentele de shopping înainte de Initialize
        _shopping.OnReadyForCheckout += GoToRegister;
        _shopping.OnLeaveEmpty += LeaveStore;

        IEventBus eventBus = null;
        ServiceLocator.Instance.TryGet(out eventBus);

        _shopping.Initialize(registry, startingBudget, eventBus);

        Debug.Log($"[CustomerAI] {name} – Inițializat. Pornesc cumpărăturile.");

        CurrentState = State.GoingToShelf;
        _shopping.StartShopping();
    }

    // =========================================================================
    //  UPDATE – mașina de stări
    // =========================================================================

    private void Update()
    {
        switch (CurrentState)
        {
            // ── Merge spre raft ───────────────────────────────────────────────
            case State.GoingToShelf:
                _nav.TickArrivalCheck();
                break;

            // ── Ajuns la raft, ia produsul ────────────────────────────────────
            // Starea TakingProduct e setată din callback-ul NavigateToShelf;
            // revenirea la GoingToShelf / GoingToRegister e gestionată de _shopping.
            case State.TakingProduct:
                break;

            // ── Merge spre casa de marcat ─────────────────────────────────────
            case State.GoingToRegister:
                _nav.TickArrivalCheck();
                break;

            // ── Stă la coadă ──────────────────────────────────────────────────
            case State.InQueue:
                _nav.TickQueueFollow();
                break;

            // ── Pleacă din magazin ────────────────────────────────────────────
            case State.Leaving:
                if (_exitPoint != null &&
                    Vector3.Distance(transform.position, _exitPoint.position) <= arriveDistance)
                {
                    gameObject.SetActive(false);
                }
                break;
        }

        _emote.UpdateAnimations(StateToVisual(CurrentState));
    }

    // =========================================================================
    //  NAVIGARE SPRE CASĂ DE MARCAT
    // =========================================================================

    private void GoToRegister()
    {
        IReadOnlyList<CashRegisterQueue> registers;
        if (_registry != null)
            registers = _registry.GetAllCashRegisterQueues();
        else
            registers = FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None);

        _targetRegister = _nav.GetBestRegister(registers);

        if (_targetRegister == null)
        {
            Debug.LogWarning($"[CustomerAI] {name} – Nicio casă de marcat disponibilă. Plec.");
            LeaveStore();
            return;
        }

        Debug.Log($"[CustomerAI] {name} – Merg spre {_targetRegister.name}.");

        _nav.NavigateTo(
            destination: _targetRegister.GetNextQueuePosition(),
            onArrival: TryJoinQueue
        );

        CurrentState = State.GoingToRegister;
    }

    private void TryJoinQueue()
    {
        if (_targetRegister == null)
        {
            GoToRegister();
            return;
        }

        if (_targetRegister.TryEnqueue(this))
        {
            Debug.Log($"[CustomerAI] {name} – Am intrat în coada la {_targetRegister.name}.");
            CurrentState = State.InQueue;
        }
        else
        {
            Debug.Log($"[CustomerAI] {name} – Coada la {_targetRegister.name} e plină. Caut alta.");
            _targetRegister = null;
            GoToRegister();
        }
    }

    // =========================================================================
    //  CHECKOUT & PLECARE
    // =========================================================================

    /// <summary>
    /// Apelat de CashRegisterQueue când vine rândul clientului.
    /// </summary>
    public void OnCheckoutComplete()
    {
        int totalBill = _shopping.CalculateTotalPriceRON();
        Debug.Log($"[CustomerAI] {name} – Plătit {totalBill} RON.");

        if (FinanceManager.Instance != null)
            FinanceManager.Instance.RegisterTransaction(TransactionCategory.Venituri_Vanzari, totalBill);

        LeaveStore();
    }

    /// <summary>
    /// Apelat de CashRegisterQueue pentru a actualiza poziția în coadă.
    /// </summary>
    public void SetQueueTarget(Transform target) => _nav.SetQueueTarget(target);

    /// <summary>
    /// Forțează clientul să iasă imediat (ex: magazin închis).
    /// </summary>
    public void ForceExit()
    {
        _nav.CancelArrivalCallback();
        _emote.HideEmote();
        LeaveStore();
    }

    private void LeaveStore()
    {
        if (_exitPoint != null)
        {
            _nav.NavigateToExit(_exitPoint.position);
            CurrentState = State.Leaving;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // =========================================================================
    //  CLEANUP
    // =========================================================================

    private void OnDestroy()
    {
        if (_shopping != null)
        {
            _shopping.OnReadyForCheckout -= GoToRegister;
            _shopping.OnLeaveEmpty -= LeaveStore;
        }
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    /// <summary>
    /// Convertește starea AI în starea vizuală pentru EmoteController.
    /// Menține decuplarea între logica de business și animații.
    /// </summary>
    private static CustomerEmoteController.VisualState StateToVisual(State state) =>
        state switch
        {
            State.GoingToShelf => CustomerEmoteController.VisualState.GoingToShelf,
            State.TakingProduct => CustomerEmoteController.VisualState.TakingProduct,
            State.GoingToRegister => CustomerEmoteController.VisualState.GoingToRegister,
            State.InQueue => CustomerEmoteController.VisualState.InQueue,
            State.Leaving => CustomerEmoteController.VisualState.Leaving,
            _ => CustomerEmoteController.VisualState.Idle
        };
}