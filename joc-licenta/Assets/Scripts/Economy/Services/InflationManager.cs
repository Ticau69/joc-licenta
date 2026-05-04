using System;
using UnityEngine;

public class InflationManager : MonoBehaviour
{
    public static InflationManager Instance { get; private set; }

    [Header("Inflation State")]
    [SerializeField] private float currentInflation = 1.0f;

    [Header("Bounds")]
    [SerializeField] private float minInflation = 0.5f;
    [SerializeField] private float maxInflation = 3.0f;

    [Header("Daily Model")]
    [Tooltip("Tendința medie zilnică (ex: 0.003 = +0.3% pe zi)")]
    [SerializeField] private float dailyDrift = 0.0025f;

    [Tooltip("Volatilitatea zilnică (ex: 0.01 = ±1% random)")]
    [SerializeField] private float dailyVolatility = 0.01f;

    [Tooltip("Ținta către care tinde inflația în timp")]
    [SerializeField] private float targetInflation = 1.2f;

    [Tooltip("Cât de repede revine spre țintă (0 = deloc, 0.02 = destul de vizibil)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float meanReversionStrength = 0.01f;

    [Header("Rare Events")]
    [Tooltip("Șansa zilnică să apară un eveniment (ex: 0.05 = 5%)")]
    [Range(0f, 1f)]
    [SerializeField] private float eventChancePerDay = 0.05f;

    [Tooltip("Șoc pozitiv (creștere) minim/maxim")]
    [SerializeField] private Vector2 positiveShockRange = new Vector2(0.10f, 0.20f);

    [Tooltip("Șoc negativ (scădere) minim/maxim")]
    [SerializeField] private Vector2 negativeShockRange = new Vector2(0.05f, 0.15f);

    [Tooltip("Probabilitatea ca un eveniment să fie pozitiv")]
    [Range(0f, 1f)]
    [SerializeField] private float positiveEventProbability = 0.6f;

    [Header("Price Output")]
    [SerializeField] private int priceDecimals = 0;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = false;

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<float> OnInflationChanged;

    /// <summary>Apelat când apare un șoc de inflație — UI-ul marchează ziua.</summary>
    public event Action OnShockApplied; // ── NOU

    public float CurrentInflation => currentInflation;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ServiceLocator.Instance.Register(this);
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        ClampInflation();
    }

    /// <summary>Rulează o "zi" de economie.</summary>
    public void SimulateDay()
    {
        float previous = currentInflation;

        float change = dailyDrift;
        change += UnityEngine.Random.Range(-dailyVolatility, dailyVolatility);
        change += (targetInflation - currentInflation) * meanReversionStrength;
        currentInflation *= (1f + change);

        if (UnityEngine.Random.value < eventChancePerDay)
            ApplyRandomShock();

        ClampInflation();

        if (!Mathf.Approximately(previous, currentInflation))
        {
            OnInflationChanged?.Invoke(currentInflation);

            if (enableLogs)
                Debug.Log($"[Inflation] {previous:F3} -> {currentInflation:F3} " +
                          $"(target {targetInflation:F2})");
        }
    }

    private void ApplyRandomShock()
    {
        bool positive = UnityEngine.Random.value < positiveEventProbability;

        if (positive)
        {
            float pct = UnityEngine.Random.Range(positiveShockRange.x, positiveShockRange.y);
            currentInflation *= (1f + pct);
            if (enableLogs) Debug.Log($"[Inflation] ȘOC POZITIV: +{pct * 100f:F1}%");
        }
        else
        {
            float pct = UnityEngine.Random.Range(negativeShockRange.x, negativeShockRange.y);
            currentInflation *= (1f - pct);
            if (enableLogs) Debug.Log($"[Inflation] ȘOC NEGATIV: -{pct * 100f:F1}%");
        }

        // ── NOU: notificăm UI-ul că a apărut un șoc ──────────────────────────
        OnShockApplied?.Invoke();
    }

    private void ClampInflation()
    {
        currentInflation = Mathf.Clamp(currentInflation, minInflation, maxInflation);
    }

    public float GetPrice(float basePrice)
    {
        float raw = basePrice * currentInflation;
        return Round(raw, priceDecimals);
    }

    private float Round(float value, int decimals)
    {
        decimals = Mathf.Clamp(decimals, 0, 6);
        float p = Mathf.Pow(10f, decimals);
        return Mathf.Round(value * p) / p;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (minInflation > maxInflation)
            (minInflation, maxInflation) = (maxInflation, minInflation);

        dailyVolatility = Mathf.Max(0f, dailyVolatility);
        meanReversionStrength = Mathf.Clamp(meanReversionStrength, 0f, 0.2f);
        eventChancePerDay = Mathf.Clamp01(eventChancePerDay);
        positiveEventProbability = Mathf.Clamp01(positiveEventProbability);

        if (positiveShockRange.x > positiveShockRange.y)
            (positiveShockRange.x, positiveShockRange.y) =
                (positiveShockRange.y, positiveShockRange.x);

        if (negativeShockRange.x > negativeShockRange.y)
            (negativeShockRange.x, negativeShockRange.y) =
                (negativeShockRange.y, negativeShockRange.x);

        ClampInflation();
    }
#endif
}