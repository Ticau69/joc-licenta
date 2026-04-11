using UnityEngine;

public class LuminiStalp : MonoBehaviour
{
    [Header("Light")]
    [Tooltip("Light-ul punctiform al becului.")]
    public Light lampLight;
 
    [Tooltip("Intensitate maximă când e aprins.")]
    public float maxIntensity = 2f;
 
    [Tooltip("Renderer-ul becului (pentru emisive material).")]
    public Renderer bulbRenderer;
 
    [Tooltip("Index material emissive pe bulbRenderer.")]
    public int bulbMaterialIndex = 0;
 
    [Header("Schedule")]
    [Tooltip("Ora la care se aprinde (ex: 21 = 21:00).")]
    public int turnOnHour  = 21;
    [Tooltip("Ora la care se stinge (ex: 8 = 08:00).")]
    public int turnOffHour = 8;
 
    [Header("Transition")]
    [Tooltip("Cât durează tranziția aprindere/stingere în secunde reale.")]
    public float transitionDuration = 1.5f;
 
    // ── Private ───────────────────────────────────────────────────────────────
 
    private bool   _isOn            = false;
    private float  _targetIntensity = 0f;
    private float  _currentIntensity = 0f;
 
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
 
    // ── Unity lifecycle ───────────────────────────────────────────────────────
 
    void Start()
    {
        if (lampLight == null)
            lampLight = GetComponentInChildren<Light>();
 
        // Stare inițială bazată pe ora curentă
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourChanged += OnHourChanged;
            ApplyStateImmediate(ShouldBeOn(TimeManager.Instance.CurrentHour));
        }
        else
        {
            Debug.LogWarning($"[StreetLamp] {name} — TimeManager nu e disponibil!");
        }
    }
 
    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourChanged -= OnHourChanged;
    }
 
    // Tranziție fluidă — singura logică din Update
    void Update()
    {
        if (Mathf.Approximately(_currentIntensity, _targetIntensity)) return;
 
        _currentIntensity = Mathf.MoveTowards(
            _currentIntensity,
            _targetIntensity,
            (maxIntensity / transitionDuration) * Time.unscaledDeltaTime
        );
 
        ApplyIntensity(_currentIntensity);
    }
 
    // ── Private ───────────────────────────────────────────────────────────────
 
    private void OnHourChanged()
    {
        if (TimeManager.Instance == null) return;
 
        bool shouldBeOn = ShouldBeOn(TimeManager.Instance.CurrentHour);
 
        if (shouldBeOn == _isOn) return; // Nu s-a schimbat nimic
 
        _isOn            = shouldBeOn;
        _targetIntensity = shouldBeOn ? maxIntensity : 0f;
    }
 
    /// <summary>
    /// Verifică dacă stâlpul trebuie să fie aprins la ora dată.
    /// Suportă și intervalul peste miezul nopții (ex: 21→8).
    /// </summary>
    private bool ShouldBeOn(int hour)
    {
        if (turnOnHour > turnOffHour)
            // Peste miezul nopții: aprins de la 21 până la 8
            return hour >= turnOnHour || hour < turnOffHour;
        else
            // Normal: aprins de la 8 până la 21
            return hour >= turnOnHour && hour < turnOffHour;
    }
 
    /// <summary>Setează starea fără tranziție — folosit la inițializare.</summary>
    private void ApplyStateImmediate(bool on)
    {
        _isOn             = on;
        _targetIntensity  = on ? maxIntensity : 0f;
        _currentIntensity = _targetIntensity;
        ApplyIntensity(_currentIntensity);
    }
 
    private void ApplyIntensity(float intensity)
    {
        if (lampLight != null)
        {
            lampLight.intensity = intensity;
            lampLight.enabled   = intensity > 0.01f;
        }
 
        // Emissive pe material (becul se aprinde vizual)
        if (bulbRenderer != null)
        {
            Material mat = bulbRenderer.materials[bulbMaterialIndex];
            Color emissive = Color.yellow * (intensity / maxIntensity);
            mat.SetColor(EmissionColor, emissive);
 
            // Forțează GPU să știe că emissive s-a schimbat
            DynamicGI.SetEmissive(bulbRenderer, emissive);
        }
    }
 
#if UNITY_EDITOR
    void OnValidate()
    {
        // Preview în Editor
        if (lampLight != null && !Application.isPlaying)
        {
            lampLight.intensity = maxIntensity;
        }
    }
#endif
}
