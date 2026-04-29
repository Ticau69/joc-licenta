using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Gestionează relația jucătorului cu fiecare furnizor.
/// Relația afectează prețurile și disponibilitatea comenzilor.
/// </summary>
public class SupplierRelationshipManager : MonoBehaviour
{
    public static SupplierRelationshipManager Instance { get; private set; }

    [Header("Furnizori")]
    public List<FurnizoriSO> allSuppliers = new List<FurnizoriSO>();

    [Header("Praguri relație")]
    [Tooltip("Sub această valoare furnizorul e Supărat.")]
    public int angryThreshold = 30;
    [Tooltip("Peste această valoare furnizorul e Prietenos.")]
    public int friendlyThreshold = 70;

    [Header("Discount / Penalizare")]
    [Tooltip("Discount la relație Prietenos (ex: 0.10 = 10%).")]
    public float friendlyDiscount = 0.10f;
    [Tooltip("Penalizare la relație Supărat (ex: 0.15 = +15% preț).")]
    public float angryPenalty = 0.15f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    // supplierName → relationship score 0-100
    private Dictionary<string, int> _relationships = new Dictionary<string, int>();
    // supplierName → datorie neachitată
    private Dictionary<string, int> _pendingDebts = new Dictionary<string, int>();

    public event Action OnRelationshipsChanged;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitializeRelationships();
    }

    private void InitializeRelationships()
    {
        foreach (var supplier in allSuppliers)
        {
            if (supplier == null) continue;
            if (!_relationships.ContainsKey(supplier.supplierName))
                _relationships[supplier.supplierName] = supplier.startingRelationship;
            if (!_pendingDebts.ContainsKey(supplier.supplierName))
                _pendingDebts[supplier.supplierName] = 0;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int GetRelationship(FurnizoriSO supplier)
    {
        if (supplier == null) return 50;
        return _relationships.TryGetValue(supplier.supplierName, out int val) ? val : 50;
    }

    public RelationshipStatus GetStatus(FurnizoriSO supplier)
    {
        int rel = GetRelationship(supplier);
        if (rel < angryThreshold) return RelationshipStatus.Angry;
        if (rel >= friendlyThreshold) return RelationshipStatus.Friendly;
        return RelationshipStatus.Neutral;
    }

    public string GetStatusText(FurnizoriSO supplier)
    {
        return GetStatus(supplier) switch
        {
            RelationshipStatus.Angry => "🔴 Supărat",
            RelationshipStatus.Neutral => "🟡 Neutru",
            RelationshipStatus.Friendly => "🟢 Prietenos",
            _ => "🟡 Neutru"
        };
    }

    public Color GetStatusColor(FurnizoriSO supplier)
    {
        return GetStatus(supplier) switch
        {
            RelationshipStatus.Angry => new Color(0.9f, 0.3f, 0.3f),
            RelationshipStatus.Neutral => new Color(1f, 0.85f, 0.2f),
            RelationshipStatus.Friendly => new Color(0.3f, 0.85f, 0.3f),
            _ => Color.white
        };
    }

    /// <summary>Returnează prețul final per unitate cu modificatorii de relație.</summary>
    public float GetFinalPrice(FurnizoriSO supplier, ProductType type)
    {
        SupplierProduct product = supplier.GetProduct(type);
        if (product == null) return 0f;

        float price = product.basePricePerUnit;
        switch (GetStatus(supplier))
        {
            case RelationshipStatus.Friendly:
                price *= (1f - friendlyDiscount);
                break;
            case RelationshipStatus.Angry:
                price *= (1f + angryPenalty);
                break;
        }
        return price;
    }

    /// <summary>Furnizorul refuză comenzile dacă e Supărat și are datorie neachitată.</summary>
    public bool CanOrder(FurnizoriSO supplier)
    {
        if (supplier == null) return false;
        if (GetStatus(supplier) == RelationshipStatus.Angry &&
            GetPendingDebt(supplier) > 0)
            return false;
        return true;
    }

    public int GetPendingDebt(FurnizoriSO supplier)
    {
        if (supplier == null) return 0;
        return _pendingDebts.TryGetValue(supplier.supplierName, out int d) ? d : 0;
    }

    // ── Modificare relație ────────────────────────────────────────────────────

    /// <summary>Plată la timp → relație crește.</summary>
    public void OnPaymentMade(FurnizoriSO supplier, bool onTime)
    {
        ModifyRelationship(supplier, onTime ? +8 : -12);

        if (_pendingDebts.ContainsKey(supplier.supplierName))
            _pendingDebts[supplier.supplierName] = 0;

        Debug.Log($"[Supplier] {supplier.supplierName} — plată {(onTime ? "la timp" : "întârziată")}. " +
                  $"Relație: {GetRelationship(supplier)}");
    }

    /// <summary>Comandă nouă plasată → relație crește ușor.</summary>
    public void OnOrderPlaced(FurnizoriSO supplier)
    {
        ModifyRelationship(supplier, +3);
    }

    /// <summary>Adaugă datorie neachitată pentru plata la livrare/credit.</summary>
    public void AddPendingDebt(FurnizoriSO supplier, int amount)
    {
        if (!_pendingDebts.ContainsKey(supplier.supplierName))
            _pendingDebts[supplier.supplierName] = 0;
        _pendingDebts[supplier.supplierName] += amount;
    }

    private void ModifyRelationship(FurnizoriSO supplier, int delta)
    {
        if (supplier == null) return;
        string key = supplier.supplierName;
        if (!_relationships.ContainsKey(key)) _relationships[key] = 50;
        _relationships[key] = Mathf.Clamp(_relationships[key] + delta, 0, 100);
        OnRelationshipsChanged?.Invoke();
    }
}

public enum RelationshipStatus { Angry, Neutral, Friendly }