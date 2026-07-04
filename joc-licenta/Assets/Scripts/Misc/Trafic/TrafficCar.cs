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

    // OPTIMIZARE (pooling): referințe către bandă/spawner, ca la finalul
    // traseului mașina să se întoarcă în pool în loc să se distrugă.
    private TrafficLane _lane;
    private System.Collections.Generic.List<TrafficCar> _laneList;

    // OPTIMIZARE: ne abonăm o singură dată la evenimentul orei, indiferent
    // de câte ori e reciclat obiectul (Start rulează o singură dată per
    // instanță, chiar dacă e dezactivat/reactivat de multe ori de pool).
    private bool _subscribedToClock = false;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // OPTIMIZARE: MaterialPropertyBlock în loc de accesarea r.material,
    // care ar instanția un material unic per renderer (leak de memorie și
    // break de batching). Folosim un singur block reutilizabil per array.
    private MaterialPropertyBlock _propBlock;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Initialize(Vector3 start, Vector3 destination, float speed,
                            TrafficLane lane, System.Collections.Generic.List<TrafficCar> laneList)
    {
        transform.position = start;
        _destination = destination;
        _targetSpeed = speed;
        _currentSpeed = speed;
        _isInitialized = true;

        _lane = lane;
        _laneList = laneList;

        // Resetăm starea reziduală rămasă de la o eventuală utilizare
        // anterioară a acestui obiect din pool.
        _isBraking = false;

        Vector3 dir = (destination - start).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        SyncLightsWithTime(forceApply: true);
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        _propBlock ??= new MaterialPropertyBlock();

        if (!_subscribedToClock && TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourChanged += OnHourChangedHandler;
            _subscribedToClock = true;
        }

        if (!_isInitialized)
            SyncLightsWithTime(forceApply: true);
    }

    void OnDestroy()
    {
        // Rămâne ca plasă de siguranță: dacă vreodată obiectul chiar e
        // distrus (nu doar dezactivat de pool), ne dezabonăm corect.
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourChanged -= OnHourChangedHandler;
    }

    /// <summary>
    /// Wrapper fără parametri, compatibil cu semnătura Action a lui
    /// TimeManager.OnHourChanged. La schimbarea orei nu forțăm reaplicarea
    /// (forceApply: false) — early-return-ul normal din SyncLightsWithTime
    /// e corect aici, doar la Initialize (reciclare din pool) avem nevoie
    /// de forceApply: true.
    /// </summary>
    private void OnHourChangedHandler() => SyncLightsWithTime(forceApply: false);

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
        {
            _isInitialized = false;

            if (_lane != null && _laneList != null)
                _lane.ReturnToPool(this, _laneList);
            else
                Destroy(gameObject); // fallback dacă nu a fost spawnat prin pool
        }
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

    /// <param name="forceApply">
    /// La reciclarea din pool, ora poate fi aceeași ca înainte de a fi
    /// dezactivată mașina, deci early-return-ul normal (nightTime == _lightsOn)
    /// ar păstra greșit stopurile de frânare aprinse. forceApply=true sare
    /// peste acel early-return și reaplică luminile corect la fiecare Initialize.
    /// </param>
    private void SyncLightsWithTime(bool forceApply = false)
    {
        if (TimeManager.Instance == null) return;

        int hour = TimeManager.Instance.CurrentHour;
        bool nightTime = hour >= 21 || hour < 8;

        if (!forceApply && nightTime == _lightsOn) return;
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
            ApplyEmission(r, emission, on);
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
            ApplyEmission(r, emission, on || braking);
        }

        foreach (Light l in taillightLights)
        {
            if (l == null) continue;
            l.enabled = on || braking;
            l.color = braking ? Color.red : new Color(1f, 0.3f, 0.3f);
            l.intensity = braking ? 1.5f : 0.8f;
        }
    }

    /// <summary>
    /// Setează culoarea de emisie printr-un MaterialPropertyBlock, în loc de
    /// r.material (care clonează materialul într-o instanță unică per renderer,
    /// blocând SRP batching / static batching și crescând memoria pentru
    /// fiecare mașină din pool). Keyword-ul _EMISSION rămâne pe shared material
    /// și trebuie activat o singură dată acolo (vezi notă mai jos).
    /// </summary>
    private void ApplyEmission(Renderer r, Color emission, bool enabled)
    {
        _propBlock ??= new MaterialPropertyBlock();

        r.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(EmissionColorID, emission);
        r.SetPropertyBlock(_propBlock);

        // NOTĂ: EnableKeyword/DisableKeyword tot lucrează pe shared material
        // (nu instanțiază), deci rămâne sigur să-l păstrăm aici — dar dacă
        // shader-ul citește emisia mereu (majoritatea URP/HDRP Lit-urilor o fac
        // dacă _EMISSION e activ), poți lăsa keyword-ul mereu pornit din editor
        // și controla totul doar prin PropertyBlock, fără branch pe 'enabled'.
        if (enabled) r.sharedMaterial.EnableKeyword("_EMISSION");
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