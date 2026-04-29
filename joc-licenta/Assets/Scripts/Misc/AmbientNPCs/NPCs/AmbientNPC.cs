using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// NPC ambient care patrulează între waypoints pe NavMesh exterior.
/// Animator: bool "isWalking" — Idle ↔ walk.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class AmbientNPC : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("Waypoints")]
    [Tooltip("Lista de puncte pe care le va parcurge NPC-ul în ordine.")]
    [SerializeField] private GameObject[] waypoints;

    [Tooltip("Ping-pong = dus-întors | Loop = buclă în cerc")]
    [SerializeField] private PatrolMode patrolMode = PatrolMode.PingPong;

    [Header("Comportament")]
    [Tooltip("Timp de așteptare (secunde) la fiecare waypoint.")]
    [SerializeField] private float idleTimeAtWaypoint = 1.5f;

    [Tooltip("Variație random la idle time ca să pară mai natural (±).")]
    [SerializeField] private float idleTimeVariance = 0.5f;

    [Tooltip("Distanță la care considerăm că am ajuns la waypoint.")]
    [SerializeField] private float arrivalThreshold = 0.4f;

    [Header("Viteză")]
    [SerializeField] private float walkSpeed = 1.4f;

    // =========================================================================
    // PRIVATE
    // =========================================================================

    private NavMeshAgent _agent;
    private Animator _animator;

    private int _currentIndex = 0;
    private int _direction = 1;   // +1 sau -1 pentru PingPong
    private bool _isWaiting = false;

    private static readonly int AnimIsWalking = Animator.StringToHash("isWalking");

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

        _agent.speed = walkSpeed;
        _agent.stoppingDistance = arrivalThreshold;
    }

    void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[AmbientNPC] {name} nu are waypoints asignate!", this);
            enabled = false;
            return;
        }

        // Pornim de la cel mai apropiat waypoint (nu neapărat index 0)
        _currentIndex = GetClosestWaypointIndex();
        GoToCurrentWaypoint();
    }

    void Update()
    {
        if (_isWaiting || waypoints.Length == 0) return;

        // Am ajuns?
        if (!_agent.pathPending && _agent.remainingDistance <= arrivalThreshold)
        {
            StartCoroutine(WaitThenAdvance());
        }
    }

    // =========================================================================
    // NAVIGARE
    // =========================================================================

    private void GoToCurrentWaypoint()
    {
        if (waypoints[_currentIndex] == null) { AdvanceIndex(); return; }

        _agent.SetDestination(waypoints[_currentIndex].transform.position);
        SetWalking(true);
    }

    private IEnumerator WaitThenAdvance()
    {
        _isWaiting = true;
        SetWalking(false);

        float wait = idleTimeAtWaypoint + Random.Range(-idleTimeVariance, idleTimeVariance);
        yield return new WaitForSeconds(Mathf.Max(0.1f, wait));

        AdvanceIndex();
        GoToCurrentWaypoint();
        _isWaiting = false;
    }

    private void AdvanceIndex()
    {
        if (patrolMode == PatrolMode.Loop)
        {
            _currentIndex = (_currentIndex + 1) % waypoints.Length;
        }
        else // PingPong
        {
            _currentIndex += _direction;

            if (_currentIndex >= waypoints.Length)
            {
                _direction = -1;
                _currentIndex = waypoints.Length - 2;
            }
            else if (_currentIndex < 0)
            {
                _direction = 1;
                _currentIndex = 1;
            }
        }
    }

    // =========================================================================
    // ANIMATOR
    // =========================================================================

    private void SetWalking(bool walking)
    {
        _animator.SetBool(AnimIsWalking, walking);
    }

    // =========================================================================
    // UTILITAR
    // =========================================================================

    private int GetClosestWaypointIndex()
    {
        int best = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            float d = Vector3.Distance(transform.position, waypoints[i].transform.position);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    // =========================================================================
    // GIZMOS — vizualizare traseu în Editor
    // =========================================================================

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;
            Gizmos.DrawLine(waypoints[i].transform.position, waypoints[i + 1].transform.position);
            Gizmos.DrawSphere(waypoints[i].transform.position, 0.15f);
        }
        Gizmos.DrawSphere(waypoints[waypoints.Length - 1].transform.position, 0.15f);

        // Linie de la NPC la waypoint curent (play mode)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, waypoints[_currentIndex].transform.position);
        }
    }
#endif

    // =========================================================================
    // ENUM
    // =========================================================================

    public enum PatrolMode { Loop, PingPong }
}