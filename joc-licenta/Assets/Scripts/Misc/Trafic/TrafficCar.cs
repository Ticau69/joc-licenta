using UnityEngine;

/// <summary>
/// Comportamentul unei mașini individuale pe stradă.
/// Detectează mașinile din față prin Raycast și adaptează viteza.
/// </summary>
public class TrafficCar : MonoBehaviour
{
    [Header("Lights - Faruri (fata)")]
    public Renderer[] headlightRenderers;
    public Light[] headlightLights;

    [Header("Lights - Stopuri (spate)")]
    public Renderer[] taillightRenderers;
    public Light[] taillightLights;

    [Header("Emission Colors")]
    public Color headlightEmissionDay = Color.black;
    public Color headlightEmissionNight = new Color(4f, 4f, 3f);
    public Color taillightEmissionDay = Color.black;
    public Color taillightEmissionNight = new Color(4f, 0f, 0f);
    public Color brakingEmission = new Color(6f, 0f, 0f);

    [Header("Car Following")]
    [Tooltip("Distanța de detectare a mașinii din față.")]
    public float detectionRange = 8f;
    [Tooltip("Distanța minimă față de mașina din față.")]
    public float minFollowDistance = 3f;
    [Tooltip("Cât de repede accelerează/decelerează (unități/s²).")]
    public float acceleration = 4f;
    [Tooltip("Layer-ul mașinilor pentru Raycast.")]
    public LayerMask carLayerMask = -1; // default: toate layer-ele

    // ── Runtime ───────────────────────────────────────────────────────────────

    private Vector3 _destination;
    private float _targetSpeed;       // viteza dorită (setată la init)
    private float _currentSpeed;      // viteza curentă (adaptată la trafic)
    private bool _isInitialized = false;
    private bool _lightsOn = false;
    private bool _isBraking = false;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // ── Public API ────────────────────────────────────────────────────────────

    public void Initialize(Vector3 start, Vector3 destination, float speed)
    {
        transform.position = start;
        _destination = destination;
        _targetSpeed = speed;
        _currentSpeed = speed;
        _isInitialized = true;

        Vector3 dir = (destination - start).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        SyncLightsWithTime();
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourChanged += SyncLightsWithTime;

        if (!_isInitialized)
            SyncLightsWithTime();
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourChanged -= SyncLightsWithTime;
    }

    void Update()
    {
        if (!_isInitialized) return;
        if (Time.timeScale == 0f) return;

        float distanceToEnd = Vector3.Distance(transform.position, _destination);

        // ── 1. Detectare mașină din față ──────────────────────────────────────
        float speedLimit = GetSpeedLimitFromCarAhead();

        // ── 2. Frânare la destinație ──────────────────────────────────────────
        if (distanceToEnd < 5f)
            speedLimit = Mathf.Min(speedLimit, Mathf.Lerp(0f, _targetSpeed, distanceToEnd / 5f));

        // ── 3. Adaptare viteză curentă (accelerare/frânare lină) ──────────────
        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            speedLimit,
            acceleration * Time.deltaTime
        );

        // ── 4. Actualizare stop lights ────────────────────────────────────────
        bool shouldBrake = _currentSpeed < _targetSpeed * 0.5f;
        if (shouldBrake != _isBraking)
        {
            _isBraking = shouldBrake;
            UpdateBrakeLight();
        }

        // ── 5. Mișcare ────────────────────────────────────────────────────────
        transform.position = Vector3.MoveTowards(
            transform.position,
            _destination,
            _currentSpeed * Time.deltaTime
        );

        if (distanceToEnd < 0.2f)
            Destroy(gameObject);
    }

    // ── Car Following (Raycast) ───────────────────────────────────────────────

    /// <summary>
    /// Aruncă un Ray în față. Dacă găsește o mașină, returnează viteza ei
    /// proporțional cu distanța — mai aproape = mai lent.
    /// </summary>
    private float GetSpeedLimitFromCarAhead()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, detectionRange, carLayerMask))
        {
            // Ignorăm dacă am lovit propriul collider
            if (hit.collider.gameObject == gameObject) return _targetSpeed;

            TrafficCar carAhead = hit.collider.GetComponentInParent<TrafficCar>();
            if (carAhead == null) return _targetSpeed;

            float distance = hit.distance;

            // Prea aproape — oprire completă
            if (distance <= minFollowDistance)
                return 0f;

            // Urmărim mașina din față la viteza ei, scalat cu distanța
            float followRatio = (distance - minFollowDistance) /
                                (detectionRange - minFollowDistance);

            return Mathf.Lerp(carAhead._currentSpeed, _targetSpeed, followRatio);
        }

        return _targetSpeed;
    }

    // ── Lights ────────────────────────────────────────────────────────────────

    private void SyncLightsWithTime()
    {
        if (TimeManager.Instance == null) return;

        int hour = TimeManager.Instance.CurrentHour;
        bool nightTime = hour >= 21 || hour < 8;

        if (nightTime == _lightsOn) return;
        _lightsOn = nightTime;

        SetHeadlights(nightTime);
        SetTaillights(nightTime, _isBraking);
    }

    private void SetHeadlights(bool on)
    {
        Color emission = on ? headlightEmissionNight : headlightEmissionDay;

        foreach (Renderer r in headlightRenderers)
        {
            if (r == null) continue;
            r.material.SetColor(EmissionColorID, emission);
            if (on) r.material.EnableKeyword("_EMISSION");
            else r.material.DisableKeyword("_EMISSION");
        }

        foreach (Light l in headlightLights)
        {
            if (l == null) continue;
            l.enabled = on;
        }
    }

    private void SetTaillights(bool on, bool braking)
    {
        Color emission = braking ? brakingEmission :
                         on ? taillightEmissionNight :
                                   taillightEmissionDay;

        foreach (Renderer r in taillightRenderers)
        {
            if (r == null) continue;
            r.material.SetColor(EmissionColorID, emission);
            if (on || braking) r.material.EnableKeyword("_EMISSION");
            else r.material.DisableKeyword("_EMISSION");
        }

        foreach (Light l in taillightLights)
        {
            if (l == null) continue;
            l.enabled = on || braking;
            l.color = braking ? Color.red : new Color(1f, 0.3f, 0.3f);
            l.intensity = braking ? 1.5f : 0.8f;
        }
    }

    private void UpdateBrakeLight()
    {
        SetTaillights(_lightsOn, _isBraking);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!_isInitialized) return;

        // Ray de detectare
        Gizmos.color = _isBraking ? Color.red : Color.yellow;
        Gizmos.DrawRay(
            transform.position + Vector3.up * 0.5f,
            transform.forward * detectionRange
        );
    }
#endif
}