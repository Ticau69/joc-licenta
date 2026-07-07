using UnityEngine;
using System;

/// <summary>
/// Gestionează TVA-ul aplicat la cumpărare și vânzare.
/// TVA la cumpărare = crește costul achiziției din depozit.
/// TVA la vânzare = adaugă TVA la prețul plătit de client, dar statul ia partea lui.
/// </summary>
public class TaxManager : MonoBehaviour
{
    public static TaxManager Instance { get; private set; }

    [Header("TVA Settings")]
    [Tooltip("Procentaj TVA aplicat la achizițiile din depozit (ex: 0.19 = 19%).")]
    [Range(0f, 0.5f)] public float purchaseTaxRate = 0.19f;

    [Tooltip("Procentaj TVA aplicat la vânzări către clienți.")]
    [Range(0f, 0.5f)] public float salesTaxRate = 0.19f;

    [Tooltip("Dacă e true, TVA-ul de vânzare e inclus în preț (client nu vede diferența).\n" +
             "Dacă e false, TVA-ul se adaugă peste preț (clientul plătește mai mult).")]
    public bool salesTaxIncludedInPrice = true;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<int> OnTaxCollected; // TVA colectat de stat

    // ── Runtime ───────────────────────────────────────────────────────────────
    private int _totalTaxCollectedToday = 0;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += ResetDailyTax;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= ResetDailyTax;
    }

    // ── Purchase Tax (TVA la cumpărare) ───────────────────────────────────────

    /// <summary>
    /// Calculează costul final al unei achiziții din depozit cu TVA inclus.
    /// </summary>
    public int ApplyPurchaseTax(int basePrice)
    {
        return Mathf.RoundToInt(basePrice * (1f + purchaseTaxRate));
    }

    /// <summary>
    /// Returnează doar suma TVA pentru o achiziție.
    /// </summary>
    public int GetPurchaseTaxAmount(int basePrice)
    {
        return Mathf.RoundToInt(basePrice * purchaseTaxRate);
    }

    // ── Sales Tax (TVA la vânzare) ────────────────────────────────────────────

    /// <summary>
    /// Procesează o vânzare — separă TVA-ul de profitul jucătorului.
    /// Returnează suma netă care rămâne jucătorului după ce statul ia TVA-ul.
    /// </summary>
    public int ProcessSale(int totalPaidByCustomer)
    {
        if (totalPaidByCustomer <= 0) return 0;

        int taxAmount;
        int playerRevenue;

        if (salesTaxIncludedInPrice)
        {
            // TVA e inclus în preț — scoatem TVA-ul din total
            // Formula: tax = total * rate / (1 + rate)
            taxAmount = Mathf.RoundToInt(totalPaidByCustomer * salesTaxRate / (1f + salesTaxRate));
            playerRevenue = totalPaidByCustomer - taxAmount;
        }
        else
        {
            // TVA se adaugă peste preț
            taxAmount = Mathf.RoundToInt(totalPaidByCustomer * salesTaxRate);
            playerRevenue = totalPaidByCustomer;
        }

        // Înregistrăm TVA-ul colectat
        _totalTaxCollectedToday += taxAmount;
        OnTaxCollected?.Invoke(taxAmount);

        if (FinanceManager.Instance != null)
            FinanceManager.Instance.RegisterTransaction(
                TransactionCategory.Amenzi, taxAmount);

        Debug.Log($"[TVA] Vânzare: {totalPaidByCustomer} RON | TVA: {taxAmount} RON | Net: {playerRevenue} RON");

        return playerRevenue;
    }
    /// <summary>
    /// Returnează doar suma TVA extrasă dintr-un preț de vânzare (pentru afișare în UI).
    /// </summary>
    public int GetSalesTaxAmount(int salePrice)
    {
        if (salesTaxIncludedInPrice)
            return Mathf.RoundToInt(salePrice * salesTaxRate / (1f + salesTaxRate));
        else
            return Mathf.RoundToInt(salePrice * salesTaxRate);
    }
    /// <summary>Procentaj TVA formatat pentru UI.</summary>
    public string GetSalesTaxDisplay() => $"{salesTaxRate * 100f:F0}%";
    public string GetPurchaseTaxDisplay() => $"{purchaseTaxRate * 100f:F0}%";
    public int TotalTaxToday => _totalTaxCollectedToday;

    private void ResetDailyTax() => _totalTaxCollectedToday = 0;
}