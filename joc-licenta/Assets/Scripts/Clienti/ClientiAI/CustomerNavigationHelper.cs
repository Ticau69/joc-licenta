using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gestionează tot ce ține de NavMesh și deplasarea clientului:
/// setarea destinațiilor, verificarea sosirii, găsirea celui mai apropiat raft
/// și callback-ul de sosire la destinație.
///
/// Este un helper pur de navigație — nu știe nimic despre logica de cumpărare
/// sau starea clientului.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CustomerNavigationHelper : MonoBehaviour
{
    // =========================================================================
    //  INSPECTOR
    // =========================================================================

    [Header("Navigație")]
    [SerializeField] private float arriveDistance = 1f;

    // =========================================================================
    //  STARE INTERNĂ
    // =========================================================================

    private NavMeshAgent _agent;

    // Guard împotriva lui IsAtDestination() care returnează true în primul frame
    // înainte ca NavMeshAgent să fi calculat path-ul.
    private bool _destinationSet = false;
    private float _destinationSetTime = 0f;
    private const float DestinationSettleDelay = 0.1f;
    private Vector3 _lastQueueTargetPos;

    // Callback apelat o singură dată când clientul ajunge la destinația curentă.
    // Setat de NavigateTo() și anulat după apelare.
    private Action _onArrivalCallback;

    // Ținta curentă de coadă — actualizată de CashRegisterQueue în fiecare frame.
    private Transform _queueTarget;

    // =========================================================================
    //  INIT
    // =========================================================================

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    // =========================================================================
    //  UPDATE – verificare sosire
    // =========================================================================

    /// <summary>
    /// Trebuie apelat din CustomerAI.Update() pentru stările care necesită
    /// verificarea sosirii (GoingToShelf, GoingToRegister).
    /// </summary>
    public void TickArrivalCheck()
    {
        if (_onArrivalCallback == null) return;
        if (!HasDestinationSettled()) return;
        if (!IsAtDestination()) return;

        // Consumăm callback-ul înainte de apelare pentru a evita reintranța
        var callback = _onArrivalCallback;
        _onArrivalCallback = null;
        callback.Invoke();
    }

    /// <summary>
    /// Trebuie apelat din CustomerAI.Update() când clientul e în coadă,
    /// pentru a urmări spot-ul alocat de CashRegisterQueue.
    /// </summary>
    public void TickQueueFollow()
    {
        if (_queueTarget != null) return;

        Vector3 targetPos = _queueTarget.position;

        if (Vector3.SqrMagnitude(targetPos - _lastQueueTargetPos) > 0.09f)
        {
            _agent.SetDestination(_queueTarget.position);
            _lastQueueTargetPos = targetPos;
        }
    }

    // =========================================================================
    //  API PUBLIC
    // =========================================================================

    /// <summary>
    /// Navighează spre raftul specificat și apelează <paramref name="onArrival"/>
    /// când clientul ajunge la stand position.
    /// </summary>
    public void NavigateToShelf(WorkStation shelf, Action onArrival)
    {
        SetDestination(shelf.GetClosestStandPosition(transform.position));
        _onArrivalCallback = onArrival;
    }

    /// <summary>
    /// Navighează spre o poziție arbitrară (ex: casa de marcat, exit)
    /// și apelează <paramref name="onArrival"/> la sosire.
    /// </summary>
    public void NavigateTo(Vector3 destination, Action onArrival = null)
    {
        SetDestination(destination);
        _onArrivalCallback = onArrival;
    }

    /// <summary>
    /// Navighează spre exit fără callback — clientul se dezactivează
    /// când ajunge (gestionat de CustomerAI.Update în starea Leaving).
    /// </summary>
    public void NavigateToExit(Vector3 exitPosition)
    {
        SetDestination(exitPosition);
        _onArrivalCallback = null;
    }

    /// <summary>
    /// Setează ținta de coadă — apelat de CashRegisterQueue.UpdateQueueDestinations().
    /// </summary>
    public void SetQueueTarget(Transform target)
    {
        _queueTarget = target;
        if (_queueTarget != null)
            _agent.SetDestination(_queueTarget.position);
    }

    /// <summary>
    /// Anulează orice callback de sosire existent (ex: ForceExit).
    /// </summary>
    public void CancelArrivalCallback()
    {
        _onArrivalCallback = null;
        _queueTarget = null;
    }

    /// <summary>
    /// Returnează cel mai apropiat WorkStation față de poziția curentă a clientului.
    /// Folosit de CustomerShoppingBehavior pentru a alege raftul optim.
    /// </summary>
    public WorkStation GetClosestShelf(List<WorkStation> shelves)
    {
        WorkStation best = null;
        float bestDist = float.MaxValue;

        foreach (var shelf in shelves)
        {
            if (shelf == null) continue;
            float d = Vector3.Distance(transform.position, shelf.GetStandPosition());
            if (d < bestDist)
            {
                bestDist = d;
                best = shelf;
            }
        }

        return best;
    }

    /// <summary>
    /// Returnează cel mai apropiat CashRegisterQueue disponibil (coadă neplină),
    /// sau cel mai puțin aglomerat dacă toate sunt pline.
    /// </summary>
    public CashRegisterQueue GetBestRegister(IReadOnlyList<CashRegisterQueue> registers)
    {
        if (registers == null || registers.Count == 0) return null;

        CashRegisterQueue best = null;
        int bestCount = int.MaxValue;
        float bestDist = float.MaxValue;

        // Pasul 1: caută case cu loc disponibil
        foreach (var r in registers)
        {
            if (r == null) continue;
            if (r.QueueCount >= r.MaxQueueSize) continue;

            int qc = r.QueueCount;
            float d = Vector3.Distance(transform.position, r.GetNextQueuePosition());

            if (qc < bestCount || (qc == bestCount && d < bestDist))
            {
                bestCount = qc;
                bestDist = d;
                best = r;
            }
        }

        if (best != null) return best;

        // Pasul 2 (fallback): toate pline → alege cea mai puțin aglomerată
        bestCount = int.MaxValue;
        bestDist = float.MaxValue;

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

        return best;
    }

    // =========================================================================
    //  CHECKS NAVIGAȚIE
    // =========================================================================

    public bool IsAtDestination()
    {
        if (_agent.pathPending) return false;
        if (_agent.remainingDistance == Mathf.Infinity) return false;
        return _agent.remainingDistance <= Mathf.Max(arriveDistance, _agent.stoppingDistance);
    }

    public bool IsMoving() => _agent.velocity.magnitude > 0.1f;

    // =========================================================================
    //  HELPERS PRIVATI
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
}