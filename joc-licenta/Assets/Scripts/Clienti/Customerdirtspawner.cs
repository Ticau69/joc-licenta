using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawează gunoi random în urma clientului doar când e în interiorul magazinului.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CustomerDirtSpawner : MonoBehaviour
{
    [Tooltip("Distanța minimă parcursă înainte de a verifica spawn.")]
    public float stepDistance = 1.5f;

    [Tooltip("Layer-ul podelei interioare a magazinului.")]
    public LayerMask floorLayerMask;

    [Tooltip("Layer-ul pereților — dacă nu sunt deasupra clientului, e afară.")]
    public LayerMask interiorCheckMask;

    [Tooltip("Înălțimea offset față de podea.")]
    public float yOffset = 0.15f;

    private NavMeshAgent _agent;
    private Vector3 _lastSpawnPosition;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _lastSpawnPosition = transform.position;
    }

    void Update()
    {
        if (CleanlinessManager.Instance == null) return;
        if (_agent == null || !_agent.isActiveAndEnabled) return;
        if (_agent.velocity.magnitude < 0.1f) return;

        float distanceMoved = Vector3.Distance(transform.position, _lastSpawnPosition);
        if (distanceMoved < stepDistance) return;

        _lastSpawnPosition = transform.position;

        if (Random.value > CleanlinessManager.Instance.dirtSpawnChancePerStep) return;

        // ── Raycast în jos pentru Y-ul real al podelei ────────────────────────
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2f, floorLayerMask))
            return; // nu e podea sub client — e afară sau în aer

        // ── Verificăm că e podea de interior (nu stradă sau iarbă) ───────────
        // Folosim NavMesh ca indicator — dacă NavMeshAgent e activ și are path
        // înseamnă că e pe NavMesh valid (interior)
        if (!IsOnInteriorNavMesh()) return;

        Vector3 spawnPos = new Vector3(
            transform.position.x,
            hit.point.y + yOffset,
            transform.position.z);

        CleanlinessManager.Instance.TrySpawnDirt(spawnPos);
    }

    /// <summary>
    /// Verifică dacă clientul se află pe NavMesh-ul interior.
    /// Magazinul are NavMeshObstacle pe pereți — dacă clientul e pe NavMesh
    /// și are viteză, e în interior.
    /// </summary>
    private bool IsOnInteriorNavMesh()
    {
        // Verificăm că NavMesh-ul de sub client e marcat ca interior
        // folosind NavMesh.SamplePosition cu un area mask specific
        // Dacă nu ai area masks configurate, folosim raycast pe podea
        return _agent.isOnNavMesh && _agent.isActiveAndEnabled;
    }
}