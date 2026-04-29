using System;
using UnityEngine;

/// <summary>
/// Reprezintă un credit activ al jucătorului.
/// Serializabil pentru save/load viitor.
/// </summary>
[Serializable]
public class BankLoan
{
    public BankSO bank;

    public float principal;          // Suma împrumutată
    public float weeklyPayment;      // Rata săptămânală (fixată la contractare)
    public float annualRateSnapshot; // Dobânda anuală la momentul contractării
    public float totalOwed;          // Total de plătit (principal + dobândă totală)
    public float totalPaid;          // Cât s-a plătit până acum

    public int termDays;             // Durata totală în zile
    public int dayTaken;             // Ziua in-game la care s-a luat creditul
    public int nextPaymentDay;       // Ziua in-game la care e următoarea rată
    public int weeksRemaining;       // Rate rămase

    public bool IsFullyPaid => totalPaid >= totalOwed - 0.01f;

    public float RemainingBalance => Mathf.Max(0f, totalOwed - totalPaid);

    /// <summary>
    /// Construiește un credit nou cu rata fixată la dobânda curentă.
    /// </summary>
    public BankLoan(BankSO bank, float principal, int termDays,
                    float currentInflationPercent, int currentDay)
    {
        this.bank = bank;
        this.principal = principal;
        this.termDays = termDays;
        this.dayTaken = currentDay;

        annualRateSnapshot = bank.GetCurrentAnnualRate(currentInflationPercent);

        int weeks = Mathf.Max(1, termDays / 7);
        weeksRemaining = weeks;

        float years = termDays / 365f;
        totalOwed = principal * (1f + annualRateSnapshot * years);
        weeklyPayment = totalOwed / weeks;

        nextPaymentDay = currentDay + 7;
    }

    public override string ToString()
    {
        return $"{bank.bankName} | {principal:F0} RON | Rată: {weeklyPayment:F2} RON/săpt | "
             + $"Dobândă: {annualRateSnapshot * 100f:F1}% | Rămase: {weeksRemaining} rate";
    }
}