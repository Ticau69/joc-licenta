using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// State machine pentru angajatul Curățător.
/// Patrulează magazinul și se duce la gunoaie când le găsește.
/// </summary>
public class JanitorStateMachine
{
    private enum State
    {
        Patrolling,
        MovingToDirt,
        Cleaning
    }

    private State _state = State.Patrolling;
    private DirtObject _targetDirt = null;
    private float _wanderTimer = 0f;
    private float _checkTimer = 0f;

    private readonly Employee _owner;
    private readonly Animator _animator;
    private readonly GameObject _broomVisual;
    private readonly int _isSweepingHash = Animator.StringToHash("isSweeping");

    private const float WANDER_INTERVAL = 4f;
    private const float WANDER_RADIUS = 8f;
    private const float DIRT_CHECK_INTERVAL = 0.5f;
    private const float CLEAN_RADIUS = 0.8f;

    private Vector3 _lastDirtPosition;

    // Constructorul primește acum și elementele vizuale
    public JanitorStateMachine(Employee owner, Animator animator, GameObject broomVisual)
    {
        _owner = owner;
        _animator = animator;
        _broomVisual = broomVisual;

        // Ne asigurăm că mătura e ascunsă la inițializare
        SetSweepingVisuals(false);
    }

    public void Update(NavMeshAgent agent)
    {
        _checkTimer += Time.deltaTime;

        switch (_state)
        {
            case State.Patrolling:
                HandlePatrolling(agent);
                break;
            case State.MovingToDirt:
                HandleMovingToDirt(agent);
                break;
            case State.Cleaning:
                HandleCleaning(agent);
                break;
        }
    }

    private void HandlePatrolling(NavMeshAgent agent)
    {
        if (_checkTimer >= DIRT_CHECK_INTERVAL)
        {
            _checkTimer = 0f;

            if (CleanlinessManager.Instance != null)
            {
                DirtObject closest = CleanlinessManager.Instance.GetClosestDirt(agent.transform.position);

                if (closest != null)
                {
                    _targetDirt = closest;
                    _state = State.MovingToDirt;
                    agent.SetDestination(_targetDirt.transform.position);
                    return;
                }
            }
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            _wanderTimer += Time.deltaTime;
            if (_wanderTimer >= WANDER_INTERVAL)
            {
                Vector3 newPos = Employee.RandomNavSphere(agent.transform.position, WANDER_RADIUS, -1);
                agent.SetDestination(newPos);
                _wanderTimer = 0f;
            }
        }
    }

    private void HandleMovingToDirt(NavMeshAgent agent)
    {
        if (_targetDirt == null)
        {
            _state = State.Patrolling;
            return;
        }

        Vector3 dirtPos = _targetDirt.transform.position;
        if (Vector3.SqrMagnitude(dirtPos - _lastDirtPosition) > 0.01f)
        {
            agent.SetDestination(dirtPos);
            _lastDirtPosition = dirtPos;
        }

        float distToDirt = Vector3.Distance(agent.transform.position, dirtPos);

        if (distToDirt <= CLEAN_RADIUS)
        {
            agent.ResetPath();
            _targetDirt.StartCleaning();
            _state = State.Cleaning;

            // --- NOU: Pornim animația și scoatem mătura ---
            SetSweepingVisuals(true);
        }
    }

    private void HandleCleaning(NavMeshAgent agent)
    {
        if (_targetDirt == null)
        {
            _owner.AddXP(5);
            _targetDirt = null;
            _state = State.Patrolling;

            // --- NOU: Oprim animația și ascundem mătura ---
            SetSweepingVisuals(false);
            return;
        }

        agent.ResetPath();
    }

    public void Reset()
    {
        if (_targetDirt != null)
            _targetDirt.CancelCleaning();

        _targetDirt = null;
        _state = State.Patrolling;
        _wanderTimer = 0f;
        _checkTimer = 0f;

        // Siguranță: ascundem mătura dacă își termină tura în timp ce dădea cu ea
        SetSweepingVisuals(false);
    }

    // Metodă helper pentru a păstra codul curat
    private void SetSweepingVisuals(bool isSweeping)
    {
        if (_broomVisual != null)
        {
            _broomVisual.SetActive(isSweeping);
        }

        if (_animator != null)
        {
            _animator.SetBool(_isSweepingHash, isSweeping);
        }
    }
}