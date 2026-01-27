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
    Apa,
    SucNatural,
    Iaurt,
    // --- Produse Congelator ---
    Inghetata,
    PizzaCongelata,
    PuiCongelat
}

public enum ShelfType
{
    StandardShelf,  // Raft Lemn/Metal
    Fridge,         // Frigider
    Freezer         // Congelator
}

/// <summary>
/// WorkStation - Optimized with event support and validation
/// Compatible cu noul sistem de services
/// </summary>
public class WorkStation : MonoBehaviour
{
    [Header("Station Configuration")]
    public StationType stationType;

    [Header("Navigation")]
    public Transform interactionPoint;

    [Header("Shelf Type (Only if Shelf)")]
    public ShelfType shelfVariant;

    [Header("Slot Configuration")]
    public ProductType slot1Product = ProductType.None;
    public ProductType pendingProduct = ProductType.None;
    public int slot1Stock = 0;
    public int maxProductsPerSlot = 20;
    [Header("Visuals")]
    public SimpleDoorController doorController;

    [Header("Storage: General Inventory")]
    // Folosit DOAR dacă stationType == Storage
    public Dictionary<ProductType, int> storageInventory = new Dictionary<ProductType, int>();

    // Cache pentru event bus (dacă e disponibil)
    private IEventBus _eventBus;
    private bool _hasEventBus = false;
    private bool _isInitialized = false;

    void Awake()
    {
        // Încearcă să obții event bus-ul
        _hasEventBus = ServiceLocator.Instance.TryGet(out _eventBus);
    }

    void Start()
    {
        // Dacă event bus-ul nu era disponibil în Awake, încearcă din nou
        if (!_hasEventBus)
        {
            _hasEventBus = ServiceLocator.Instance.TryGet(out _eventBus);
        }

        InitializeStorage();
        _isInitialized = true;
    }

    private void InitializeStorage()
    {
        // Inițializare automată DOAR pentru Depozit
        if (stationType != StationType.Storage) return;

        foreach (ProductType type in System.Enum.GetValues(typeof(ProductType)))
        {
            if (type == ProductType.None) continue;

            if (!storageInventory.ContainsKey(type))
            {
                storageInventory.Add(type, 50);
            }
            else
            {
                storageInventory[type] = 50;
            }
        }

        Debug.Log($"[DEPOZIT] {name} a fost aprovizionat automat cu stoc de start!");
    }

    // === STORAGE OPERATIONS (cu event support) ===

    /// <summary>
    /// Adaugă marfă în depozit
    /// </summary>
    public void AddToStorage(ProductType type, int amount)
    {
        if (!ValidateStorageOperation(type, amount, "Add")) return;

        int oldStock = GetStorageStock(type);

        if (storageInventory.ContainsKey(type))
        {
            storageInventory[type] += amount;
        }
        else
        {
            storageInventory.Add(type, amount);
        }

        int newStock = storageInventory[type];

        Debug.Log($"[DEPOZIT] Am primit {amount} x {type}. Total: {newStock}");

        // Publish event dacă avem event bus
        PublishStockChangedEvent(type, oldStock, newStock);
    }

    /// <summary>
    /// Scoate marfă din depozit (metodă nouă pentru compatibilitate)
    /// </summary>
    public bool RemoveFromStorage(ProductType type, int amount)
    {
        if (!ValidateStorageOperation(type, amount, "Remove")) return false;

        if (!storageInventory.ContainsKey(type))
        {
            Debug.LogWarning($"[DEPOZIT] Produsul {type} nu există în inventar!");
            return false;
        }

        if (storageInventory[type] < amount)
        {
            Debug.LogWarning($"[DEPOZIT] Stoc insuficient pentru {type}. Disponibil: {storageInventory[type]}, Cerut: {amount}");
            return false;
        }

        int oldStock = storageInventory[type];
        storageInventory[type] -= amount;
        int newStock = storageInventory[type];

        Debug.Log($"[DEPOZIT] Am scos {amount} x {type}. Rămas: {newStock}");

        // Publish event
        PublishStockChangedEvent(type, oldStock, newStock);

        return true;
    }

    /// <summary>
    /// Scoate marfă din depozit (metodă originală - kept for backwards compatibility)
    /// </summary>
    public bool TakeFromStorage(ProductType type, int amount)
    {
        return RemoveFromStorage(type, amount);
    }

    /// <summary>
    /// Verifică stocul din depozit
    /// </summary>
    public int GetStorageStock(ProductType type)
    {
        if (stationType != StationType.Storage) return 0;
        return storageInventory.ContainsKey(type) ? storageInventory[type] : 0;
    }

    // === SHELF OPERATIONS ===

    /// <summary>
    /// Reumple complet slot-ul 1
    /// </summary>
    public void RestockSlot1()
    {
        if (slot1Product == ProductType.None)
        {
            Debug.LogWarning($"[WorkStation] {name} nu are produs setat!");
            return;
        }

        int oldStock = slot1Stock;
        slot1Stock = maxProductsPerSlot;

        Debug.Log($"[WorkStation] {name} reumplut cu {slot1Product}");

        PublishStockChangedEvent(slot1Product, oldStock, slot1Stock);
    }

    /// <summary>
    /// Adaugă produse în slot (pentru Restocker)
    /// </summary>
    public void AddProduct(int amount)
    {
        if (slot1Product == ProductType.None)
        {
            Debug.LogWarning($"[WorkStation] {name} - nu se poate adăuga, slot gol!");
            return;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[WorkStation] {name} - cantitate invalidă: {amount}");
            return;
        }

        int oldStock = slot1Stock;
        slot1Stock += amount;

        if (slot1Stock > maxProductsPerSlot)
        {
            slot1Stock = maxProductsPerSlot;
        }

        PublishStockChangedEvent(slot1Product, oldStock, slot1Stock);
    }

    /// <summary>
    /// Ia produse din slot (pentru Client)
    /// </summary>
    public int TakeProduct(int requestedAmount)
    {
        if (slot1Stock <= 0)
        {
            return 0;
        }

        if (requestedAmount <= 0)
        {
            Debug.LogWarning($"[WorkStation] {name} - cantitate cerută invalidă: {requestedAmount}");
            return 0;
        }

        int oldStock = slot1Stock;
        int amountToTake = Mathf.Min(requestedAmount, slot1Stock);
        slot1Stock -= amountToTake;

        // Logică schimbare automată tip
        if (slot1Stock == 0 && pendingProduct != ProductType.None && pendingProduct != slot1Product)
        {
            Debug.Log($"[WorkStation] Raft golit! Schimbăm tipul din {slot1Product} în {pendingProduct}");
            slot1Product = pendingProduct;
            pendingProduct = ProductType.None;
        }

        PublishStockChangedEvent(slot1Product, oldStock, slot1Stock);

        return amountToTake;
    }

    // === HELPER METHODS ===

    /// <summary>
    /// Returnează lista de produse permise în funcție de tipul raftului
    /// </summary>
    public List<ProductType> GetAllowedProducts()
    {
        List<ProductType> allowed = new List<ProductType>();

        switch (shelfVariant)
        {
            case ShelfType.StandardShelf:
                allowed.Add(ProductType.Paine);
                allowed.Add(ProductType.Chipsuri);
                allowed.Add(ProductType.Biscuiti);
                allowed.Add(ProductType.Conserve);
                break;

            case ShelfType.Fridge:
                allowed.Add(ProductType.Cola);
                allowed.Add(ProductType.Apa);
                allowed.Add(ProductType.SucNatural);
                allowed.Add(ProductType.Iaurt);
                break;

            case ShelfType.Freezer:
                allowed.Add(ProductType.Inghetata);
                allowed.Add(ProductType.PizzaCongelata);
                allowed.Add(ProductType.PuiCongelat);
                break;
        }

        return allowed;
    }

    public Vector3 GetStandPosition()
    {
        if (interactionPoint != null)
        {
            return interactionPoint.position;
        }
        return transform.position;
    }

    // === VALIDATION ===

    private bool ValidateStorageOperation(ProductType type, int amount, string operation)
    {
        if (stationType != StationType.Storage)
        {
            Debug.LogWarning($"[WorkStation] {name} nu e Storage! Nu se poate executa {operation}.");
            return false;
        }

        if (type == ProductType.None)
        {
            Debug.LogWarning($"[WorkStation] {operation} - ProductType.None nu e valid!");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[WorkStation] {operation} - cantitate invalidă: {amount}");
            return false;
        }

        return true;
    }

    // === EVENT PUBLISHING ===

    private void PublishStockChangedEvent(ProductType type, int oldStock, int newStock)
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

    // === PROPERTIES ===

    public bool NeedsRestocking =>
        stationType == StationType.Shelf &&
        slot1Stock < maxProductsPerSlot &&
        slot1Product != ProductType.None &&
        (pendingProduct == ProductType.None || pendingProduct == slot1Product);

    public bool NeedsClearing =>
        stationType == StationType.Shelf &&
        pendingProduct != ProductType.None &&
        pendingProduct != slot1Product &&
        slot1Stock > 0;

    public bool HasProducts =>
        stationType == StationType.Shelf && slot1Stock > 0;

    public bool IsStorage =>
        stationType == StationType.Storage;

    public bool IsShelf =>
        stationType == StationType.Shelf;
}