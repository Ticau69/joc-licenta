using UnityEngine;
using System.Collections.Generic;

public enum StationType { CashRegister, Storage, Shelf }

public class WorkStation : MonoBehaviour
{
    public StationType stationType;
    [Header("Navigație")]
    public Transform interactionPoint;

    [Header("Tip Raft (Doar dacă e Shelf)")]
    public ShelfType shelfVariant; // Aici selectezi în Inspector: Frigider, Raft, etc.

    [Header("Configurare Sloturi")]
    // Momentan facem logică pentru 1 produs, dar pregătită pentru 2
    public ProductType slot1Product = ProductType.None;
    public ProductType pendingProduct = ProductType.None;
    public int slot1Stock = 0;

    [Header("Depozit: Inventar General")]
    // Acest dicționar va fi folosit DOAR dacă stationType == Storage
    // Cheie: Tip Produs, Valoare: Cantitate
    public Dictionary<ProductType, int> storageInventory = new Dictionary<ProductType, int>();

    // Configurare generală
    public int maxProductsPerSlot = 20;

    // --- LOGICA DE FILTRARE ---
    // Această funcție returnează lista de produse permise în funcție de tipul raftului
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

    void Start()
    {
        // Inițializare automată DOAR pentru Depozit
        if (stationType == StationType.Storage)
        {
            // Trecem prin toate tipurile posibile de produse din Enum
            foreach (ProductType type in System.Enum.GetValues(typeof(ProductType)))
            {
                // Ignorăm "None"
                if (type != ProductType.None)
                {
                    // Adăugăm un stoc de start (ex: 50 bucăți din fiecare)
                    if (!storageInventory.ContainsKey(type))
                    {
                        storageInventory.Add(type, 50);
                    }
                    else
                    {
                        storageInventory[type] = 50;
                    }
                }
            }
            Debug.Log($"[DEPOZIT] {name} a fost aprovizionat automat cu stoc de start!");
        }
    }

    public Vector3 GetStandPosition()
    {
        // Dacă am setat un interactionPoint, îl folosim pe acela.
        // Dacă am uitat să îl setăm, folosim poziția obiectului curent ca fallback.
        if (interactionPoint != null) return interactionPoint.position;
        return transform.position;
    }

    // Adaugă marfă în depozit (ex: când scoatem de pe un raft)
    public void AddToStorage(ProductType type, int amount)
    {
        if (stationType != StationType.Storage) return;

        if (storageInventory.ContainsKey(type))
        {
            storageInventory[type] += amount;
        }
        else
        {
            storageInventory.Add(type, amount);
        }
        Debug.Log($"[DEPOZIT] Am primit {amount} x {type}. Total: {storageInventory[type]}");
    }

    // Scoate marfă din depozit (pentru Angajați care vin să ia marfă)
    public bool TakeFromStorage(ProductType type, int amount)
    {
        if (stationType != StationType.Storage) return false;

        if (storageInventory.ContainsKey(type) && storageInventory[type] >= amount)
        {
            storageInventory[type] -= amount;
            return true;
        }
        return false;
    }

    // Verifică stocul din depozit
    public int GetStorageStock(ProductType type)
    {
        if (storageInventory.ContainsKey(type)) return storageInventory[type];
        return 0;
    }

    public void RestockSlot1()
    {
        if (slot1Product != ProductType.None)
        {
            slot1Stock = maxProductsPerSlot;
            Debug.Log($"[WorkStation] {name} reumplut cu {slot1Product}");
        }
    }



    // Proprietăți ajutătoare
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
    public bool HasProducts => stationType == StationType.Shelf && slot1Stock > 0;

    // Metodă pentru Angajat (Restocker) - Adaugă o bucată
    public void AddProduct(int amount)
    {
        if (slot1Product != ProductType.None)
        {
            slot1Stock += amount;
            if (slot1Stock > maxProductsPerSlot) slot1Stock = maxProductsPerSlot;
        }
    }

    // Metodă pentru Client - Ia o bucată
    public int TakeProduct(int requestedAmount)
    {
        if (slot1Stock > 0)
        {
            int amountToTake = Mathf.Min(requestedAmount, slot1Stock);
            slot1Stock -= amountToTake;

            // --- LOGICĂ NOUĂ: SCHIMBARE AUTOMATĂ TIP ---
            // Dacă s-a golit raftul și aveam o comandă de schimbare în așteptare
            if (slot1Stock == 0 && pendingProduct != ProductType.None && pendingProduct != slot1Product)
            {
                Debug.Log($"[WorkStation] Raft golit! Schimbăm tipul din {slot1Product} în {pendingProduct}");
                slot1Product = pendingProduct; // Aplicăm schimbarea
                pendingProduct = ProductType.None; // Resetăm comanda
            }
            // -------------------------------------------

            return amountToTake;
        }
        return 0;
    }
}

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