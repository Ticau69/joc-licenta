using UnityEngine;

/// <summary>
/// ScriptableObject care definește personalitatea unei bănci.
/// Creează câte un asset per bancă: BancaRomana, BancaTransilvania, BancaComercial.
/// </summary>
[CreateAssetMenu(fileName = "NewBank", menuName = "Economy/Bank")]
public class BankSO : ScriptableObject
{
    [Header("Identitate")]
    public string bankName = "Banca Nouă";
    [TextArea(2, 3)]
    public string description = "O bancă de încredere.";
    public Color brandColor = Color.white;

    [Header("Dobândă")]
    [Tooltip("Dobânda anuală de bază, indiferent de inflație (ex: 0.05 = 5%/an)")]
    [Range(0.01f, 0.30f)]
    public float baseAnnualRate = 0.07f;

    [Tooltip("Cât de mult urmărește banca inflația. 0 = ignoră, 1 = urmărește 1:1")]
    [Range(0f, 1.5f)]
    public float inflationSensitivity = 0.8f;

    [Tooltip("Dobândă minimă garantată, indiferent de inflație negativă")]
    [Range(0.01f, 0.10f)]
    public float minAnnualRate = 0.03f;

    [Header("Limite Credit")]
    public float minLoanAmount = 500f;
    public float maxLoanAmount = 50000f;

    [Header("Termene disponibile (zile in-game)")]
    [Tooltip("Opțiunile de durată pe care le oferă banca — ex: 7, 14, 30 zile")]
    public int[] availableTermDays = { 7, 14, 30 };

    [Header("Penalizare întârziere")]
    [Tooltip("% aplicat la rata restantă dacă jucătorul nu plătește la timp")]
    [Range(0f, 0.20f)]
    public float latePenaltyRate = 0.05f;

    // ─── Runtime ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculează dobânda anuală curentă ținând cont de inflație.
    /// </summary>
    public float GetCurrentAnnualRate(float currentInflationPercent)
    {
        // Inflația vine ca procent (ex: 5.2%), o convertim la factor (0.052)
        float inflationFactor = currentInflationPercent / 100f;
        float adjustedRate = baseAnnualRate + inflationFactor * inflationSensitivity;
        return Mathf.Max(adjustedRate, minAnnualRate);
    }

    /// <summary>
    /// Calculează rata săptămânală pentru un credit dat.
    /// Folosim dobândă simplă: Total = Principal × (1 + rateAnuala × ani)
    /// </summary>
    public float CalculateWeeklyPayment(float principal, int termDays, float currentInflationPercent)
    {
        float annualRate = GetCurrentAnnualRate(currentInflationPercent);
        float years = termDays / 365f;
        float totalOwed = principal * (1f + annualRate * years);
        int weeks = Mathf.Max(1, termDays / 7);
        return totalOwed / weeks;
    }
}