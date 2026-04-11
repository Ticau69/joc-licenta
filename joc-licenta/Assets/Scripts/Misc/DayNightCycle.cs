using UnityEngine;

/// <summary>
/// Day/Night cycle bazat pe TimeManager.CurrentTimeOfDay (0..1).
/// Atașează pe un GameObject gol din scenă.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Sun")]
    [Tooltip("Directional Light-ul principal (Soarele).")]
    public Light sun;

    [Tooltip("Curba intensității soarelui de-a lungul zilei (X = timp 0..1, Y = intensitate).")]
    public AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Intensitate maximă a soarelui la amiază.")]
    public float maxSunIntensity = 1.2f;

    [Header("Sun Rotation")]
    [Tooltip("Unghiul de start al soarelui la ora 00:00 (sub orizont).")]
    public float sunriseAngle = -90f;
    [Tooltip("Unghiul de final la ora 24:00 (rotație completă).")]
    public float sunsetAngle = 270f;

    [Header("Ambient Light")]
    [Tooltip("Gradient de culoare ambient de-a lungul zilei.")]
    public Gradient ambientColorGradient;

    [Tooltip("Intensitate ambient noapte → zi.")]
    [Range(0f, 1f)] public float nightAmbientIntensity = 0.05f;
    [Range(0f, 1f)] public float dayAmbientIntensity = 1f;

    [Header("Sky")]
    [Tooltip("Material de Skybox (opțional — se modifică Exposure).")]
    public Material skyboxMaterial;
    public float nightSkyExposure = 0f;
    public float daySkyExposure = 1.3f;

    [Header("Moon (opțional)")]
    public Light moon;
    public float maxMoonIntensity = 0.15f;

    // ── Private ───────────────────────────────────────────────────────────────

    private TimeManager _timeManager;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        _timeManager = TimeManager.Instance;

        if (_timeManager == null)
            Debug.LogError("[DayNightCycle] TimeManager.Instance nu a fost găsit!");

        if (ambientColorGradient == null || ambientColorGradient.colorKeys.Length == 0)
            SetDefaultAmbientGradient();
    }

    void Update()
    {
        if (_timeManager == null) return;

        // Folosim unscaledDeltaTime indirect — citim direct CurrentTimeOfDay
        // care e calculat de TimeManager cu Time.deltaTime scalat.
        // Dacă jocul e pe pauză, CurrentTimeOfDay nu se schimbă → ciclul stă pe loc.
        float t = _timeManager.CurrentTimeOfDay; // 0..1

        UpdateSun(t);
        UpdateAmbient(t);
        UpdateSkybox(t);
        UpdateMoon(t);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void UpdateSun(float t)
    {
        if (sun == null) return;

        // Rotim soarele de la sunriseAngle la sunsetAngle
        float angle = Mathf.Lerp(sunriseAngle, sunsetAngle, t);
        sun.transform.rotation = Quaternion.Euler(angle, -30f, 0f);

        // Intensitate din curbă — 0 noaptea, max la amiază
        float curveValue = sunIntensityCurve.Evaluate(t);
        sun.intensity = curveValue * maxSunIntensity;

        // Dezactivăm soarele complet noaptea pentru performanță
        sun.enabled = sun.intensity > 0.01f;
    }

    private void UpdateAmbient(float t)
    {
        // Culoare ambient din gradient
        RenderSettings.ambientLight = ambientColorGradient.Evaluate(t);

        // Intensitate ambient
        RenderSettings.ambientIntensity = Mathf.Lerp(
            nightAmbientIntensity,
            dayAmbientIntensity,
            sunIntensityCurve.Evaluate(t)
        );
    }

    private void UpdateSkybox(float t)
    {
        if (skyboxMaterial == null) return;

        float exposure = Mathf.Lerp(nightSkyExposure, daySkyExposure,
                                    sunIntensityCurve.Evaluate(t));
        skyboxMaterial.SetFloat("_Exposure", exposure);
    }

    private void UpdateMoon(float t)
    {
        if (moon == null) return;

        // Luna e opusă soarelui
        float moonAngle = Mathf.Lerp(sunriseAngle, sunsetAngle, t) + 180f;
        moon.transform.rotation = Quaternion.Euler(moonAngle, -30f, 0f);

        // Luna apare când soarele e sub orizont (t < 0.25 sau t > 0.75)
        float sunCurve = sunIntensityCurve.Evaluate(t);
        moon.intensity = (1f - sunCurve) * maxMoonIntensity;
        moon.enabled = moon.intensity > 0.01f;
    }

    /// <summary>
    /// Gradient implicit dacă nu e setat în Inspector:
    /// noapte → albăstrui → portocaliu răsărit → alb zi → portocaliu apus → albăstrui → noapte
    /// </summary>
    private void SetDefaultAmbientGradient()
    {
        ambientColorGradient = new Gradient();

        GradientColorKey[] colors = new GradientColorKey[]
        {
            new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 0.00f), // miezul noptii
            new GradientColorKey(new Color(0.10f, 0.10f, 0.25f), 0.18f), // inainte de rasarit
            new GradientColorKey(new Color(0.90f, 0.50f, 0.20f), 0.25f), // rasarit
            new GradientColorKey(new Color(0.95f, 0.92f, 0.85f), 0.40f), // dimineata
            new GradientColorKey(new Color(1.00f, 0.98f, 0.95f), 0.50f), // amiaza
            new GradientColorKey(new Color(0.95f, 0.92f, 0.85f), 0.60f), // dupa-amiaza
            new GradientColorKey(new Color(0.90f, 0.45f, 0.15f), 0.75f), // apus
            new GradientColorKey(new Color(0.10f, 0.10f, 0.25f), 0.82f), // dupa apus
            new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 1.00f), // miezul noptii
        };

        GradientAlphaKey[] alphas = new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        };

        ambientColorGradient.SetKeys(colors, alphas);
    }

#if UNITY_EDITOR
    // Preview în Editor fără să rulezi jocul
    void OnValidate()
    {
        if (!Application.isPlaying && sun != null && _timeManager == null)
        {
            float previewT = 0.5f; // amiaza
            float angle = Mathf.Lerp(sunriseAngle, sunsetAngle, previewT);
            sun.transform.rotation = Quaternion.Euler(angle, -30f, 0f);
        }
    }
#endif
}