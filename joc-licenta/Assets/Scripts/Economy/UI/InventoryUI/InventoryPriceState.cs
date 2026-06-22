using System.Collections.Generic;

/// <summary>
/// Shared mutable state for price warnings.
/// Tracks which products are overpriced (reported by customers)
/// and which have already triggered a "below cost" mentor notification.
/// Plain C# class — no Unity dependencies.
/// </summary>
public class InventoryPriceState
{
    private readonly HashSet<ProductType> _overpricedProducts = new();
    private readonly HashSet<ProductType> _belowCostNotified = new();

    // ── Overpriced (too expensive for customers) ─────────────────────────────

    /// <summary>
    /// Marks a product as overpriced.
    /// Returns <c>true</c> only the first time (i.e. the set changed),
    /// so callers can decide whether a UI refresh is needed.
    /// </summary>
    public bool MarkOverpriced(ProductType product) => _overpricedProducts.Add(product);

    public bool IsOverpriced(ProductType product) => _overpricedProducts.Contains(product);

    // ── Below-cost debounce (mentor "Fane" notification) ────────────────────

    /// <summary>
    /// Call when the player's selling price drops below cost.
    /// Returns <c>true</c> the first time for a given product,
    /// so the caller knows whether to fire the mentor notification.
    /// </summary>
    public bool TryNotifyBelowCost(ProductType product) => _belowCostNotified.Add(product);

    /// <summary>
    /// Call when the player corrects the price above cost,
    /// so the notification can fire again if they drop below again later.
    /// </summary>
    public void ClearBelowCost(ProductType product) => _belowCostNotified.Remove(product);
}