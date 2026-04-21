using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Manager pentru piața concurențială.
/// Calculează prețurile concurenților și impactul lor asupra clienților.
/// </summary>
public class CompetitiveMarketManager : MonoBehaviour
{
    public static CompetitiveMarketManager Instance { get; private set; }

    [Header("Concurenți")]
    [Tooltip("Lista magazinelor concurente (ScriptableObjects).")]
    public List<CompetitorStore> competitors = new List<CompetitorStore>();

    [Header("Impact pe Clienți")]
    [Tooltip("Dacă concurentul e cu X% mai ieftin, șansa clientului de a cumpăra scade cu Y%.")]
    public float competitorPriceImpactFactor = 0.5f;

    [Tooltip("Diferența minimă de preț (%) la care concurentul afectează comportamentul clientului.")]
    [Range(0f, 0.5f)] public float priceThresholdForImpact = 0.05f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    // Prețurile curente ale concurenților: competitor → (product → price)
    private Dictionary<CompetitorStore, Dictionary<ProductType, float>> _competitorPrices
        = new Dictionary<CompetitorStore, Dictionary<ProductType, float>>();

    public event Action OnMarketUpdated;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        InitializeCompetitorPrices();

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += UpdateCompetitorPrices;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= UpdateCompetitorPrices;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    private void InitializeCompetitorPrices()
    {
        foreach (CompetitorStore competitor in competitors)
        {
            if (competitor == null) continue;
            _competitorPrices[competitor] = new Dictionary<ProductType, float>();
            RecalculatePricesForCompetitor(competitor);
        }

        Debug.Log($"[Market] {competitors.Count} concurenți inițializați.");
    }

    // ── Price Calculation ─────────────────────────────────────────────────────

    private void RecalculatePricesForCompetitor(CompetitorStore competitor)
    {
        if (!ServiceLocator.Instance.TryGet(out IEconomyService economy)) return;

        foreach (ProductType type in Enum.GetValues(typeof(ProductType)))
        {
            if (type == ProductType.None) continue;

            float playerPrice = economy.GetSellingPrice(type);
            if (playerPrice <= 0f) continue;

            // Multiplicator bazat pe specialitate
            bool isSpecialty = competitor.specialtyProducts.Contains(type);
            float multiplier = isSpecialty
                ? competitor.specialtyPriceMultiplier
                : competitor.defaultPriceMultiplier;

            // Reacție la prețul jucătorului
            float basePrice = economy.GetBaseCost(type);
            float targetPrice = basePrice * multiplier;

            // Blend între prețul target și prețul jucătorului
            float competitorPrice = Mathf.Lerp(
                targetPrice,
                playerPrice * multiplier,
                competitor.reactivityToPlayer);

            // Variație zilnică random
            float variation = 1f + UnityEngine.Random.Range(
                -competitor.dailyPriceVariation,
                 competitor.dailyPriceVariation);

            _competitorPrices[competitor][type] = competitorPrice * variation;
        }
    }

    private void UpdateCompetitorPrices()
    {
        foreach (CompetitorStore competitor in competitors)
        {
            if (competitor == null) continue;
            RecalculatePricesForCompetitor(competitor);
        }

        OnMarketUpdated?.Invoke();
        Debug.Log("[Market] Prețuri concurenți actualizate pentru ziua nouă.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returnează cel mai mic preț al concurenților pentru un produs.
    /// </summary>
    public float GetBestCompetitorPrice(ProductType type)
    {
        float bestPrice = float.MaxValue;

        foreach (var kvp in _competitorPrices)
        {
            if (kvp.Value.TryGetValue(type, out float price))
                bestPrice = Mathf.Min(bestPrice, price);
        }

        return bestPrice == float.MaxValue ? 0f : bestPrice;
    }

    /// <summary>
    /// Calculează impactul concurenților asupra șansei de cumpărare a clientului.
    /// Returnează un multiplicator [0..1] aplicat pe buyChance.
    /// </summary>
    public float GetBuyChanceModifier(ProductType type, float playerPrice)
    {
        float bestCompetitorPrice = GetBestCompetitorPrice(type);
        if (bestCompetitorPrice <= 0f) return 1f;

        float priceDiff = (playerPrice - bestCompetitorPrice) / bestCompetitorPrice;

        // Dacă jucătorul e mai ieftin sau la același preț — nu e impact negativ
        if (priceDiff <= priceThresholdForImpact) return 1f;

        // Concurentul e mai ieftin — scădem șansa de cumpărare
        float modifier = 1f - (priceDiff * competitorPriceImpactFactor);
        return Mathf.Clamp(modifier, 0.1f, 1f);
    }

    /// <summary>
    /// Returnează toate prețurile unui concurent specific pentru UI.
    /// </summary>
    public Dictionary<ProductType, float> GetCompetitorPrices(CompetitorStore competitor)
    {
        if (_competitorPrices.TryGetValue(competitor, out var prices))
            return new Dictionary<ProductType, float>(prices);
        return new Dictionary<ProductType, float>();
    }

    /// <summary>
    /// Returnează dacă jucătorul e mai ieftin decât toți concurenții la un produs.
    /// </summary>
    public bool IsPlayerCheapest(ProductType type, float playerPrice)
    {
        return playerPrice <= GetBestCompetitorPrice(type);
    }

    /// <summary>
    /// Returnează concurentul cu cel mai mic preț pentru un produs.
    /// </summary>
    public (CompetitorStore store, float price) GetCheapestCompetitor(ProductType type)
    {
        CompetitorStore bestStore = null;
        float bestPrice = float.MaxValue;

        foreach (var kvp in _competitorPrices)
        {
            if (kvp.Value.TryGetValue(type, out float price) && price < bestPrice)
            {
                bestPrice = price;
                bestStore = kvp.Key;
            }
        }

        return (bestStore, bestPrice == float.MaxValue ? 0f : bestPrice);
    }
}