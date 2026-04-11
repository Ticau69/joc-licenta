using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Componentă pe NPC (CustomerAI, Employee) care detectează uși
/// în calea NavMeshAgent-ului și le deschide/închide automat.
///
/// Adaugă pe același GameObject cu NavMeshAgent.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DoorInteractor : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Distanța la care NPC-ul detectează o ușă și o deschide.")]
    public float detectionRange = 1.5f;

    [Tooltip("Layer-ul ușilor pentru Raycast.")]
    public LayerMask doorLayerMask = -1;

    [Tooltip("Cât de des verifică (secunde). 0 = fiecare frame.")]
    public float checkInterval = 0.1f;

    // ── Private ───────────────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private IDoor _currentDoor;
    private float _checkTimer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        _checkTimer += Time.deltaTime;

        if (_checkTimer < checkInterval) return;
        _checkTimer = 0f;

        CheckForDoor();
    }

    void OnDestroy()
    {
        // Eliberăm ușa curentă când NPC-ul e distrus
        ReleaseDoor();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void CheckForDoor()
    {
        if (_agent == null || !_agent.isActiveAndEnabled) return;

        // Direcția de mers
        Vector3 moveDir = _agent.velocity.magnitude > 0.1f
            ? _agent.velocity.normalized
            : (_agent.hasPath ? (_agent.steeringTarget - transform.position).normalized : transform.forward);

        Ray ray = new Ray(transform.position + Vector3.up * 0.8f, moveDir);

        if (Physics.Raycast(ray, out RaycastHit hit, detectionRange, doorLayerMask))
        {
            IDoor door = hit.collider.GetComponent<IDoor>()
                      ?? hit.collider.GetComponentInParent<IDoor>();

            if (door != null && !door.IsLocked)
            {
                // Nouă ușă detectată
                if (door != _currentDoor)
                {
                    ReleaseDoor();          // eliberăm ușa anterioară
                    _currentDoor = door;
                    _currentDoor.RequestOpen(this);
                }
                return; // ușa e deschisă — continuăm
            }
        }

        // Nu mai e nicio ușă în față — eliberăm
        if (_currentDoor != null)
            ReleaseDoor();
    }

    private void ReleaseDoor()
    {
        if (_currentDoor == null) return;
        _currentDoor.RequestClose(this);
        _currentDoor = null;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        NavMeshAgent ag = GetComponent<NavMeshAgent>();
        if (ag == null) return;

        Vector3 dir = ag.velocity.magnitude > 0.1f
            ? ag.velocity.normalized
            : transform.forward;

        Gizmos.color = _currentDoor != null ? Color.green : Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.8f, dir * detectionRange);
    }
#endif
}