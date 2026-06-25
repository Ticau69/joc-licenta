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

    [Header("Sun Visuals (Nou)")]
    [Tooltip("Culoarea luminii directe a soarelui (roșiatic la răsărit, alb la amiază).")]
    public Gradient sunColorGradient;

    [Tooltip("Cât de puternice sunt umbrele (slabe la orizont, puternice la amiază).")]
    public AnimationCurve shadowStrengthCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);

    [Header("Sun Rotation")]
    [Tooltip("Unghiul de start al soarelui la ora 00:00 (sub orizont).")]
    public float sunriseAngle = -90f;
    [Tooltip("Unghiul de final la ora 24:00 (rotație completă).")]
    public float sunsetAngle = 270f;

    [Header("Ambient & Fog Light")]
    [Tooltip("Gradient de culoare ambient de-a lungul zilei.")]
    public Gradient ambientColorGradient;

    [Tooltip("Intensitate ambient noapte → zi.")]
    [Range(0f, 1f)] public float nightAmbientIntensity = 0.05f;
    [Range(0f, 1f)] public float dayAmbientIntensity = 1f;

    [Tooltip("Sincronizează culoarea ceții (Fog) cu culoarea ambientală.")]
    public bool enableDynamicFog = true;

    [Header("Sky")]
    [Tooltip("Material de Skybox (opțional — se modifică Exposure).")]
    public Material skyboxMaterial;
    public float nightSkyExposure = 0f;
    public float daySkyExposure = 1.3f;

    [Header("Moon (opțional)")]
    public Light moon;
    public float maxMoonIntensity = 0.15f;
    [Tooltip("Culoarea luminii lunii.")]
    public Color moonColor = new Color(0.6f, 0.7f, 1f);

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

        if (sunColorGradient == null || sunColorGradient.colorKeys.Length == 0)
            SetDefaultSunGradient();

        if (enableDynamicFog)
            RenderSettings.fog = true;
    }

    void Update()
    {
        if (_timeManager == null) return;

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

        float angle = Mathf.Lerp(sunriseAngle, sunsetAngle, t);
        sun.transform.rotation = Quaternion.Euler(angle, -30f, 0f);

        float curveValue = sunIntensityCurve.Evaluate(t);
        sun.intensity = curveValue * maxSunIntensity;

        // Nou: Culoare și umbre dinamice
        sun.color = sunColorGradient.Evaluate(t);
        sun.shadowStrength = shadowStrengthCurve.Evaluate(t);

        sun.enabled = sun.intensity > 0.01f;
    }

    private void UpdateAmbient(float t)
    {
        Color currentAmbient = ambientColorGradient.Evaluate(t);

        RenderSettings.ambientLight = currentAmbient;
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, sunIntensityCurve.Evaluate(t));

        // Nou: Ceață dinamică pentru a ascunde pop-in-ul și a oferi atmosferă
        if (enableDynamicFog)
        {
            RenderSettings.fogColor = currentAmbient;
        }
    }

    private void UpdateSkybox(float t)
    {
        if (skyboxMaterial == null) return;

        float exposure = Mathf.Lerp(nightSkyExposure, daySkyExposure, sunIntensityCurve.Evaluate(t));
        skyboxMaterial.SetFloat("_Exposure", exposure);

        // Dacă folosești un Skybox procedural nativ Unity, îi poți schimba nuanța:
        if (skyboxMaterial.HasProperty("_SkyTint"))
        {
            skyboxMaterial.SetColor("_SkyTint", ambientColorGradient.Evaluate(t));
        }
    }

    private void UpdateMoon(float t)
    {
        if (moon == null) return;

        float moonAngle = Mathf.Lerp(sunriseAngle, sunsetAngle, t) + 180f;
        moon.transform.rotation = Quaternion.Euler(moonAngle, -30f, 0f);

        float sunCurve = sunIntensityCurve.Evaluate(t);
        moon.intensity = (1f - sunCurve) * maxMoonIntensity;
        moon.color = moonColor;

        // Păstrăm umbre fine și pentru lună
        moon.shadowStrength = 0.5f;
        moon.enabled = moon.intensity > 0.01f;
    }

    private void SetDefaultAmbientGradient()
    {
        ambientColorGradient = new Gradient();
        GradientColorKey[] colors = new GradientColorKey[]
        {
            new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 0.00f),
            new GradientColorKey(new Color(0.10f, 0.10f, 0.25f), 0.18f),
            new GradientColorKey(new Color(0.90f, 0.50f, 0.20f), 0.25f),
            new GradientColorKey(new Color(0.95f, 0.92f, 0.85f), 0.40f),
            new GradientColorKey(new Color(1.00f, 0.98f, 0.95f), 0.50f),
            new GradientColorKey(new Color(0.95f, 0.92f, 0.85f), 0.60f),
            new GradientColorKey(new Color(0.90f, 0.45f, 0.15f), 0.75f),
            new GradientColorKey(new Color(0.10f, 0.10f, 0.25f), 0.82f),
            new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 1.00f),
        };
        GradientAlphaKey[] alphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        ambientColorGradient.SetKeys(colors, alphas);
    }

    private void SetDefaultSunGradient()
    {
        sunColorGradient = new Gradient();
        GradientColorKey[] colors = new GradientColorKey[]
        {
            new GradientColorKey(new Color(0.1f, 0.1f, 0.3f), 0.00f), // Noapte
            new GradientColorKey(new Color(1.0f, 0.4f, 0.1f), 0.23f), // Răsărit intens portocaliu
            new GradientColorKey(new Color(1.0f, 0.9f, 0.8f), 0.30f), // Dimineață caldă
            new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.50f), // Amiază alb curat
            new GradientColorKey(new Color(1.0f, 0.9f, 0.8f), 0.70f), // După-amiază
            new GradientColorKey(new Color(1.0f, 0.3f, 0.1f), 0.77f), // Apus intens
            new GradientColorKey(new Color(0.1f, 0.1f, 0.3f), 1.00f)  // Noapte
        };
        GradientAlphaKey[] alphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        sunColorGradient.SetKeys(colors, alphas);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && sun != null && _timeManager == null)
        {
            float previewT = 0.5f;
            float angle = Mathf.Lerp(sunriseAngle, sunsetAngle, previewT);
            sun.transform.rotation = Quaternion.Euler(angle, -30f, 0f);
        }
    }
#endif
}