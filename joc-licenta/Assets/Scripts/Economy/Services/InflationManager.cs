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

    [Tooltip("Șoc pozitiv (creștere) minim/maxim - procent, ex: 0.10 = 10%")]
    [SerializeField] private Vector2 positiveShockRange = new Vector2(0.10f, 0.20f);

    [Tooltip("Șoc negativ (scădere) minim/maxim - procent absolut, ex: 0.05 = 5%")]
    [SerializeField] private Vector2 negativeShockRange = new Vector2(0.05f, 0.15f);

    [Tooltip("Probabilitatea ca un eveniment să fie pozitiv (restul negativ)")]
    [Range(0f, 1f)]
    [SerializeField] private float positiveEventProbability = 0.6f;

    [Header("Price Output")]
    [Tooltip("Numărul de zecimale pentru prețuri (0 = preț întreg, 2 = bani)")]
    [SerializeField] private int priceDecimals = 0;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = false;

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    // Optional: event pe care îl pot asculta UI / Shop ca să se actualizeze automat.
    public event Action<float> OnInflationChanged;

    public float CurrentInflation => currentInflation;

    private void Awake()
    {

        // Singleton sigur
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceLocator.Instance.Register(this);

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        ClampInflation();
    }

    /// <summary>
    /// Rulează o "zi" de economie. Apeleaz-o o dată pe zi din GameTime/DayNight manager.
    /// </summary>
    public void SimulateDay()
    {
        float previous = currentInflation;

        // 1) Drift (tendință medie)
        float change = dailyDrift;

        // 2) Volatilitate random simetrică
        change += UnityEngine.Random.Range(-dailyVolatility, dailyVolatility);

        // 3) Mean reversion spre țintă
        change += (targetInflation - currentInflation) * meanReversionStrength;

        // 4) Aplică schimbarea (multiplicativ, mai realist decât aditiv în multe cazuri)
        // change este procent (ex 0.01 = +1%), deci multiplicăm cu (1+change)
        currentInflation *= (1f + change);

        // 5) Eveniment rar (șoc)
        if (UnityEngine.Random.value < eventChancePerDay)
        {
            ApplyRandomShock();
        }

        ClampInflation();

        if (!Mathf.Approximately(previous, currentInflation))
        {
            OnInflationChanged?.Invoke(currentInflation);

            if (enableLogs)
                Debug.Log($"[Inflation] {previous:F3} -> {currentInflation:F3} (target {targetInflation:F2})");
        }
    }

    private void ApplyRandomShock()
    {
        bool positive = UnityEngine.Random.value < positiveEventProbability;

        if (positive)
        {
            float pct = UnityEngine.Random.Range(positiveShockRange.x, positiveShockRange.y);
            currentInflation *= (1f + pct);

            if (enableLogs)
                Debug.Log($"[Inflation Event] ȘOC POZITIV: +{pct * 100f:F1}%");
        }
        else
        {
            float pct = UnityEngine.Random.Range(negativeShockRange.x, negativeShockRange.y);
            currentInflation *= (1f - pct);

            if (enableLogs)
                Debug.Log($"[Inflation Event] ȘOC NEGATIV: -{pct * 100f:F1}%");
        }
    }

    private void ClampInflation()
    {
        currentInflation = Mathf.Clamp(currentInflation, minInflation, maxInflation);
    }

    /// <summary>
    /// Preț curent pentru un preț de bază, cu inflația aplicată.
    /// </summary>
    public float GetPrice(float basePrice)
    {
        float raw = basePrice * currentInflation;
        return Round(raw, priceDecimals);
    }

    /// <summary>
    /// Dacă vrei să vinzi mai ieftin decât cumperi (ex: 60% din prețul curent).
    /// </summary>
    public float GetSellPrice(float basePrice, float sellMultiplier = 0.6f)
    {
        float raw = basePrice * currentInflation * sellMultiplier;
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
        // Auto-fix pentru valori invalide
        if (minInflation > maxInflation)
            (minInflation, maxInflation) = (maxInflation, minInflation);

        dailyVolatility = Mathf.Max(0f, dailyVolatility);
        meanReversionStrength = Mathf.Clamp(meanReversionStrength, 0f, 0.2f);
        eventChancePerDay = Mathf.Clamp01(eventChancePerDay);
        positiveEventProbability = Mathf.Clamp01(positiveEventProbability);

        // Range-uri ordonate
        if (positiveShockRange.x > positiveShockRange.y)
            (positiveShockRange.x, positiveShockRange.y) = (positiveShockRange.y, positiveShockRange.x);

        if (negativeShockRange.x > negativeShockRange.y)
            (negativeShockRange.x, negativeShockRange.y) = (negativeShockRange.y, negativeShockRange.x);

        ClampInflation();
    }
#endif
}
