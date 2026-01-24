using UnityEngine;

/// <summary>
/// Configurație centralizată pentru joc - ZERO hardcoded values
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Configuration")]
public class GameConfigSO : ScriptableObject
{
    [Header("Starting Values")]
    [Tooltip("Banii cu care începe jucătorul")]
    public int startingMoney = 5000;

    [Header("Shop Settings")]
    [Tooltip("Cantitatea default pentru comenzi de supply")]
    public int defaultSupplyQuantity = 50;

    [Tooltip("Costul temporar pentru supply (până implementăm prețuri reale)")]
    public int temporarySupplyCost = 100;

    [Header("Inventory Settings")]
    [Tooltip("Cât de des se actualizează UI-ul inventarului (secunde)")]
    public float inventoryUpdateInterval = 0.5f;

    [Tooltip("Pragul pentru stoc critic")]
    public int criticalStockThreshold = 0;

    [Tooltip("Pragul pentru stoc scăzut")]
    public int lowStockThreshold = 20;

    [Header("Pricing Settings")]
    [Tooltip("Multiplicatorul maxim pentru pricing slider")]
    public float maxPriceMultiplier = 5.0f;

    [Tooltip("Multiplicatorul minim pentru pricing slider")]
    public float minPriceMultiplier = 1.0f;

    [Header("UI Settings")]
    [Tooltip("Culoare pentru stoc critic")]
    public Color criticalStockColor = new Color(1f, 0.3f, 0.3f);

    [Tooltip("Culoare pentru stoc scăzut")]
    public Color lowStockColor = Color.yellow;

    [Tooltip("Culoare pentru stoc bun")]
    public Color goodStockColor = Color.green;

    [Header("Performance Settings")]
    [Tooltip("Numărul maxim de UI elements în pool")]
    public int uiElementPoolSize = 50;

    [Tooltip("Cache duration pentru queries (secunde)")]
    public float cacheDuration = 1.0f;

    [Header("Debug Settings")]
    [Tooltip("Activează logging detaliat")]
    public bool verboseLogging = false;

    [Tooltip("Afișează warnings pentru performanță")]
    public bool showPerformanceWarnings = true;

    // Validation
    private void OnValidate()
    {
        startingMoney = Mathf.Max(0, startingMoney);
        defaultSupplyQuantity = Mathf.Max(1, defaultSupplyQuantity);
        inventoryUpdateInterval = Mathf.Max(0.1f, inventoryUpdateInterval);
        criticalStockThreshold = Mathf.Max(0, criticalStockThreshold);
        lowStockThreshold = Mathf.Max(criticalStockThreshold + 1, lowStockThreshold);
        maxPriceMultiplier = Mathf.Max(1.0f, maxPriceMultiplier);
        minPriceMultiplier = Mathf.Clamp(minPriceMultiplier, 0.1f, maxPriceMultiplier);
        uiElementPoolSize = Mathf.Max(10, uiElementPoolSize);
        cacheDuration = Mathf.Max(0.1f, cacheDuration);
    }

    public (Color color, string status) GetStockStatus(int amount)
    {
        if (amount <= criticalStockThreshold)
            return (criticalStockColor, "CRITIC");
        if (amount < lowStockThreshold)
            return (lowStockColor, "SCĂZUT");
        return (goodStockColor, "BUN");
    }
}