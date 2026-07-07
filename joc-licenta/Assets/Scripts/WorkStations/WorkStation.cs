using UnityEngine;
using System.Collections.Generic;

public enum StationType { CashRegister, Storage, Shelf }

public enum ProductType
{
    None,
    // --- Produse Raft Normal ---
    Paine,
    Chipsuri,
    Biscuiti,
    Conserve,
    // --- Produse Frigider ---
    Cola,
    DozaDeCola,
    Apa,
    SucNatural,
    Iaurt,
    // --- Produse Congelator ---
    Inghetata,
    PizzaCongelata,
    PuiCongelat,
    // --- Produse Raft Fructe/Legume ---
    Banane,
    Ananas,
}

public enum ShelfType
{
    StandardShelf,  // Raft Lemn/Metal
    Fridge,         // Frigider
    Freezer,         // Congelator
    CosFructe,
    RaftLegume,
}

/// <summary>
/// WorkStation — un singur tip de produs per raft.
/// Storage-ul delegă complet către StorageRacks.
/// </summary>
public class WorkStation : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Station Configuration")]
    public StationType stationType;

    [Header("Navigation")]
    public Transform interactionPoint;
    public Transform interactionPointSecondary;

    [Header("Shelf Configuration")]
    public ShelfType shelfVariant;
    public ProductDataSO productDatabase;

    [Header("Shelf Slot")]
    public ProductType slotProduct = ProductType.None;
    public ProductType pendingProduct = ProductType.None;
    public int slotStock = 0;
    public int maxStock = 20;


    [Header("Visuals")]
    public SimpleDoorController doorController;

    [Header("Storage Racks")]
    [Tooltip("Rafturile fizice din depozit — active doar dacă stationType == Storage.")]
    public List<StorageRacks> storageRacks = new List<StorageRacks>();

    // ── Runtime (nu se serializează) ──────────────────────────────────────────


    private IEventBus _eventBus;
    private ShelfDisplayController _displayController;
    private bool _hasEventBus;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _hasEventBus = ServiceLocator.Instance.TryGet(out _eventBus);

        if (stationType == StationType.Storage && storageRacks.Count == 0)
            storageRacks.AddRange(GetComponentsInChildren<StorageRacks>());

        _displayController = GetComponent<ShelfDisplayController>();
    }

    void Start()
    {
        if (!_hasEventBus)
            _hasEventBus = ServiceLocator.Instance.TryGet(out _eventBus);

        ValidateShelf();
        _displayController?.RefreshDisplay();
    }

    // ── SHELF OPERATIONS ──────────────────────────────────────────────────────

    /// <summary>
    /// Adaugă produse în slot. Folosit de Restocker.
    /// </summary>
    public void AddProduct(int amount)
    {
        if (slotProduct == ProductType.None)
        {
            Debug.LogWarning($"[{name}] Niciun produs configurat în slot!");
            return;
        }
        if (amount <= 0) return;

        int oldStock = slotStock;
        slotStock = Mathf.Min(slotStock + amount, maxStock);

        PublishStockChanged(slotProduct, oldStock, slotStock);
        _displayController?.RefreshDisplay();
    }

    /// <summary>
    /// Ia produse din slot. Folosit de Client.
    /// Returnează cantitatea efectiv luată.
    /// </summary>
    public int TakeProduct(int requestedAmount)
    {
        if (slotStock <= 0 || requestedAmount <= 0) return 0;

        int oldStock = slotStock;
        int taken = Mathf.Min(requestedAmount, slotStock);
        slotStock -= taken;

        // Comutare automată de tip când slotul e golit
        if (slotStock == 0 &&
            pendingProduct != ProductType.None &&
            pendingProduct != slotProduct)
        {
            Debug.Log($"[{name}] Raft golit — schimbăm {slotProduct} -> {pendingProduct}");
            slotProduct = pendingProduct;
            pendingProduct = ProductType.None;
        }

        PublishStockChanged(slotProduct, oldStock, slotStock);
        _displayController?.RefreshDisplay();
        return taken;
    }

    /// <summary>Reumple complet slotul.</summary>
    public void Restock()
    {
        if (slotProduct == ProductType.None)
        {
            Debug.LogWarning($"[{name}] Nu se poate restock — niciun produs configurat!");
            return;
        }

        int oldStock = slotStock;
        slotStock = maxStock;
        PublishStockChanged(slotProduct, oldStock, slotStock);
        _displayController?.RefreshDisplay();
    }

    // ── STORAGE OPERATIONS — delegate catre StorageRacks ─────────────────────

    /// <summary>
    /// Distribuie marfa pe rafturile depozitului în ordine.
    /// Returnează cantitatea care nu a încăput pe niciun raft.
    /// </summary>
    public int AddToStorage(ProductType type, int amount)
    {
        if (!ValidateStorageOp(type, amount, "Add")) return amount;

        int oldStock = GetStorageStock(type);
        int remaining = amount;

        foreach (StorageRacks rack in storageRacks)
        {
            if (remaining <= 0) break;
            remaining = rack.AddProduct(type, remaining);
        }

        int added = amount - remaining;
        if (added > 0)
        {
            Debug.Log($"[DEPOZIT] +{added} x {type}. Total: {GetStorageStock(type)}");
            PublishStockChanged(type, oldStock, GetStorageStock(type));
        }

        if (remaining > 0)
            Debug.LogWarning($"[DEPOZIT] Nu a incaput {remaining} x {type} — rafturile sunt pline!");

        return remaining;
    }

    /// <summary>
    /// Scoate marfa din depozit cautand pe toate rafturile.
    /// Returneaza true daca intreaga cantitate a fost gasita.
    /// </summary>
    public bool RemoveFromStorage(ProductType type, int amount)
    {
        if (!ValidateStorageOp(type, amount, "Remove")) return false;

        int available = GetStorageStock(type);
        if (available < amount)
        {
            Debug.LogWarning($"[DEPOZIT] Stoc insuficient {type}. Disponibil: {available}, Cerut: {amount}");
            return false;
        }

        int oldStock = available;
        int remaining = amount;

        foreach (StorageRacks rack in storageRacks)
        {
            if (remaining <= 0) break;
            remaining -= rack.TakeProduct(type, remaining);
        }

        Debug.Log($"[DEPOZIT] -{amount} x {type}. Ramas: {GetStorageStock(type)}");
        PublishStockChanged(type, oldStock, GetStorageStock(type));
        return true;
    }

    /// <summary>Alias pentru compatibilitate cu cod existent.</summary>
    public bool TakeFromStorage(ProductType type, int amount) => RemoveFromStorage(type, amount);

    /// <summary>Stoc total al unui tip de produs, sumat din toate rafturile.</summary>
    public int GetStorageStock(ProductType type)
    {
        if (stationType != StationType.Storage) return 0;

        int total = 0;
        foreach (StorageRacks rack in storageRacks)
            total += rack.GetStockAmount(type);
        return total;
    }

    // ── PRODUCT FILTERING (via ProductDataSO) ─────────────────────────────────

    public List<ProductData> GetAllowedProductData()
    {
        if (productDatabase == null)
        {
            Debug.LogWarning($"[{name}] productDatabase nu e asignat!");
            return new List<ProductData>();
        }

        List<ProductData> allowed = new List<ProductData>();
        foreach (ProductData data in productDatabase.allProducts)
            if (data.compatibleShelfTypes.Contains(shelfVariant))
                allowed.Add(data);

        return allowed;
    }

    public List<ProductType> GetAllowedProductTypes()
    {
        List<ProductData> data = GetAllowedProductData();
        List<ProductType> result = new List<ProductType>(data.Count);
        foreach (ProductData d in data) result.Add(d.type);
        return result;
    }

    public bool IsProductAllowed(ProductType type)
    {
        if (productDatabase == null) return false;
        foreach (ProductData data in productDatabase.allProducts)
            if (data.type == type && data.compatibleShelfTypes.Contains(shelfVariant))
                return true;
        return false;
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    public Vector3 GetStandPosition()
    {
        if (interactionPoint != null) return interactionPoint.position;
        if (interactionPointSecondary != null) return interactionPointSecondary.position;

        return transform.position;
    }

    public Vector3 GetClosestStandPosition(Vector3 requesterPosition)
    {
        // Dacă nu avem niciun punct setat, returnăm centrul raftului
        if (interactionPoint == null && interactionPointSecondary == null)
            return transform.position;

        // Dacă avem doar unul din ele, îl returnăm pe cel valid
        if (interactionPoint != null && interactionPointSecondary == null)
            return interactionPoint.position;

        if (interactionPoint == null && interactionPointSecondary != null)
            return interactionPointSecondary.position;

        // Dacă ambele există, calculăm distanța pentru a vedea care e mai aproape de client/angajat
        float dist1 = Vector3.Distance(requesterPosition, interactionPoint.position);
        float dist2 = Vector3.Distance(requesterPosition, interactionPointSecondary.position);

        return (dist1 < dist2) ? interactionPoint.position : interactionPointSecondary.position;
    }

    // ── PROPERTIES ────────────────────────────────────────────────────────────

    public bool IsStorage => stationType == StationType.Storage;
    public bool IsShelf => stationType == StationType.Shelf;

    public bool NeedsRestocking =>
        stationType == StationType.Shelf &&
        slotProduct != ProductType.None &&
        slotStock < maxStock &&
        (pendingProduct == ProductType.None || pendingProduct == slotProduct);

    public bool NeedsClearing =>
        stationType == StationType.Shelf &&
        pendingProduct != ProductType.None &&
        pendingProduct != slotProduct &&
        slotStock > 0;

    public bool HasProducts =>
        stationType == StationType.Shelf && slotStock > 0;

    public float FillRatio =>
        maxStock > 0 ? (float)slotStock / maxStock : 0f;

    // ── INTERNAL ──────────────────────────────────────────────────────────────

    private void ValidateShelf()
    {
        if (stationType != StationType.Shelf) return;


        if (slotProduct != ProductType.None && !IsProductAllowed(slotProduct))
            Debug.LogWarning($"[{name}] Produsul {slotProduct} nu e permis pe {shelfVariant}!");
    }

    private bool ValidateStorageOp(ProductType type, int amount, string op)
    {
        if (stationType != StationType.Storage)
        {
            Debug.LogWarning($"[{name}] Nu e Storage! Operatie {op} refuzata.");
            return false;
        }
        if (type == ProductType.None)
        {
            Debug.LogWarning($"[{name}] {op} — ProductType.None nu e valid!");
            return false;
        }
        if (amount <= 0)
        {
            Debug.LogWarning($"[{name}] {op} — cantitate invalida: {amount}");
            return false;
        }
        if (storageRacks.Count == 0)
        {
            Debug.LogWarning($"[{name}] {op} — niciun StorageRacks asignat!");
            return false;
        }
        return true;
    }

    private void PublishStockChanged(ProductType type, int oldStock, int newStock)
    {
        if (!_hasEventBus || _eventBus == null) return;

        _eventBus.Publish(new StockChangedEvent
        {
            Product = type,
            OldStock = oldStock,
            NewStock = newStock,
            Location = stationType
        });
    }

    // ── GIZMOS ───────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {

        Gizmos.color = slotProduct != ProductType.None ? Color.green : Color.yellow;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.15f,
            $"{slotProduct}\n{slotStock}/{maxStock}"
        );
    }
#endif
}