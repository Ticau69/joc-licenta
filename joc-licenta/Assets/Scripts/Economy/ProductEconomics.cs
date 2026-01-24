using System;

/// <summary>
/// Model de date pentru economia unui produs
/// Combinează datele statice (din ScriptableObject) cu prețul dinamic setat de jucător
/// </summary>
[Serializable]
public class ProductEconomics
{
    /// <summary>
    /// Datele statice ale produsului (din ProductDataSO)
    /// </summary>
    public ProductData data;

    /// <summary>
    /// Prețul de vânzare setat de jucător (dinamic)
    /// </summary>
    public float sellingPrice;

    /// <summary>
    /// Costul de bază al produsului (citit din data statică)
    /// </summary>
    public float CurrentBaseCost => data.baseCost;

    /// <summary>
    /// Profitul per unitate (selling price - cost)
    /// </summary>
    public float Profit => sellingPrice - CurrentBaseCost;

    /// <summary>
    /// Procentul de markup/profit
    /// </summary>
    public float ProfitMargin => CurrentBaseCost > 0
        ? ((sellingPrice - CurrentBaseCost) / CurrentBaseCost) * 100f
        : 0f;

    /// <summary>
    /// Verifică dacă prețul este profitabil
    /// </summary>
    public bool IsProfitable => Profit > 0;

    /// <summary>
    /// Constructor - inițializează cu datele din ScriptableObject
    /// </summary>
    public ProductEconomics(ProductData sourceData)
    {
        if (sourceData == null)
        {
            throw new ArgumentNullException(nameof(sourceData), "ProductData cannot be null");
        }

        data = sourceData;
        sellingPrice = sourceData.defaultSellingPrice;
    }

    /// <summary>
    /// Calculează profitul total pentru o cantitate dată
    /// </summary>
    public float CalculateTotalProfit(int quantity)
    {
        return Profit * quantity;
    }

    /// <summary>
    /// Calculează revenue-ul total pentru o cantitate dată
    /// </summary>
    public float CalculateTotalRevenue(int quantity)
    {
        return sellingPrice * quantity;
    }

    /// <summary>
    /// Calculează costul total pentru o cantitate dată
    /// </summary>
    public float CalculateTotalCost(int quantity)
    {
        return CurrentBaseCost * quantity;
    }

    public override string ToString()
    {
        return $"{data.productName}: Cost={CurrentBaseCost:F2}, Price={sellingPrice:F2}, Profit={Profit:F2} ({ProfitMargin:F1}%)";
    }
}