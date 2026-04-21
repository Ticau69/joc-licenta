using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// State machine pentru angajatul Curățător.
/// Patrulează magazinul și se duce la gunoaie când le găsește.
/// Înlocuiește WanderBehavior din Employee.DoJanitorWork().
/// </summary>
public class JanitorStateMachine
{
    private enum State
    {
        Patrolling,     // patrulează random
        MovingToDirt,   // se duce la un gunoi detectat
        Cleaning        // curăță gunoiul
    }

    private State _state = State.Patrolling;
    private DirtObject _targetDirt = null;
    private float _wanderTimer = 0f;
    private float _checkTimer = 0f;
    private readonly Employee _owner;

    private const float WANDER_INTERVAL = 4f;
    private const float WANDER_RADIUS = 8f;
    private const float DIRT_CHECK_INTERVAL = 0.5f; // verifică la fiecare 0.5s
    private const float CLEAN_RADIUS = 0.8f; // distanța la care începe curățarea

    public JanitorStateMachine(Employee owner)
    {
        _owner = owner;
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

    // ── States ────────────────────────────────────────────────────────────────

    private void HandlePatrolling(NavMeshAgent agent)
    {
        // Verificăm periodic dacă există gunoi
        if (_checkTimer >= DIRT_CHECK_INTERVAL)
        {
            _checkTimer = 0f;

            if (CleanlinessManager.Instance != null)
            {
                DirtObject closest = CleanlinessManager.Instance
                    .GetClosestDirt(agent.transform.position);

                if (closest != null)
                {
                    _targetDirt = closest;
                    _state = State.MovingToDirt;
                    agent.SetDestination(_targetDirt.transform.position);
                    return;
                }
            }
        }

        // Patrulare random
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            _wanderTimer += Time.deltaTime;
            if (_wanderTimer >= WANDER_INTERVAL)
            {
                Vector3 newPos = Employee.RandomNavSphere(
                    agent.transform.position, WANDER_RADIUS, -1);
                agent.SetDestination(newPos);
                _wanderTimer = 0f;
            }
        }
    }

    private void HandleMovingToDirt(NavMeshAgent agent)
    {
        // Gunoiul a fost deja curățat de altcineva
        if (_targetDirt == null)
        {
            _state = State.Patrolling;
            return;
        }

        agent.SetDestination(_targetDirt.transform.position);

        float distToDirt = Vector3.Distance(
            agent.transform.position,
            _targetDirt.transform.position);

        if (distToDirt <= CLEAN_RADIUS)
        {
            // Am ajuns — începem curățarea
            agent.ResetPath();
            _targetDirt.StartCleaning();
            _state = State.Cleaning;
        }
    }

    private void HandleCleaning(NavMeshAgent agent)
    {
        // Gunoiul s-a distrus = curățare terminată
        if (_targetDirt == null)
        {
            _owner.AddXP(5);
            _targetDirt = null;
            _state = State.Patrolling;
            return;
        }

        // Stăm pe loc cât curățăm
        agent.ResetPath();
    }

    /// <summary>Resetează starea la începerea unui nou schimb.</summary>
    public void Reset()
    {
        if (_targetDirt != null)
            _targetDirt.CancelCleaning();

        _targetDirt = null;
        _state = State.Patrolling;
        _wanderTimer = 0f;
        _checkTimer = 0f;
    }
}