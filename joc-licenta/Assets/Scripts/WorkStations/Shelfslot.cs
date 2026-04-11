using UnityEngine;
using System;

/// <summary>
/// Reprezintă un slot individual pe un raft, cu punct de afișare vizuală a produsului.
/// Adaugă-l ca List&lt;ShelfSlot&gt; în WorkStation și configurează-l din Inspector.
/// </summary>
[Serializable]
public class ShelfSlot
{
    [Header("Display")]
    [Tooltip("Transform-ul unde se va instanția / poziționa vizualul produsului în scenă.")]
    public Transform displayPoint;

    [Header("Produs")]
    public ProductType productType = ProductType.None;
    public ProductType pendingProduct = ProductType.None;

    [Header("Stoc")]
    public int currentStock = 0;
    public int maxStock = 20;

    // ── Runtime ───────────────────────────────────────────────────────────────
    [NonSerialized] public GameObject spawnedVisual; // prefab-ul instanțiat la displayPoint

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Slotul are produs setat și nu e plin; nu așteaptă schimbare de tip.</summary>
    public bool NeedsRestocking =>
        productType != ProductType.None &&
        currentStock < maxStock &&
        (pendingProduct == ProductType.None || pendingProduct == productType);

    /// <summary>Slotul trebuie golit înainte de a schimba tipul de produs.</summary>
    public bool NeedsClearing =>
        pendingProduct != ProductType.None &&
        pendingProduct != productType &&
        currentStock > 0;

    public bool HasProducts => currentStock > 0;
    public bool IsEmpty => currentStock == 0;
    public bool IsConfigured => productType != ProductType.None;

    /// <summary>Procentaj de umplere [0..1].</summary>
    public float FillRatio => maxStock > 0 ? (float)currentStock / maxStock : 0f;

    // ── Core logic ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ia o cantitate din slot și gestionează schimbarea automată de tip.
    /// Returnează cantitatea efectiv luată.
    /// </summary>
    public int Take(int requested)
    {
        if (requested <= 0 || currentStock <= 0) return 0;

        int taken = Mathf.Min(requested, currentStock);
        currentStock -= taken;

        // Comutare automată de tip când slotul e golit
        if (currentStock == 0 &&
            pendingProduct != ProductType.None &&
            pendingProduct != productType)
        {
            productType = pendingProduct;
            pendingProduct = ProductType.None;
        }

        return taken;
    }

    /// <summary>Adaugă cantitate, clamped la maxStock.</summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;
        currentStock = Mathf.Min(currentStock + amount, maxStock);
    }

    /// <summary>Umple complet slotul.</summary>
    public void FillToMax() => currentStock = maxStock;
}