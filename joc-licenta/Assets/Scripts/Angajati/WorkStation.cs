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
    public int slot1Stock = 0;

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

    public Vector3 GetStandPosition()
    {
        // Dacă am setat un interactionPoint, îl folosim pe acela.
        // Dacă am uitat să îl setăm, folosim poziția obiectului curent ca fallback.
        if (interactionPoint != null) return interactionPoint.position;
        return transform.position;
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
     slot1Product != ProductType.None;
    public bool HasProducts => stationType == StationType.Shelf && slot1Stock > 0;

    // Metodă pentru Angajat (Restocker) - Adaugă o bucată
    public void AddProduct()
    {
        // Adăugăm doar dacă e loc și dacă avem un tip de produs setat
        if (slot1Stock < maxProductsPerSlot && slot1Product != ProductType.None)
        {
            slot1Stock++;
            // Aici poți adăuga instanțierea vizuală a produsului pe raft
            // UpdateVisuals(); 
        }
    }

    // Metodă pentru Client - Ia o bucată
    public bool TakeProduct()
    {
        if (slot1Stock > 0)
        {
            slot1Stock--;

            // Dacă s-a golit raftul, decidem dacă păstrăm eticheta produsului sau nu
            // De obicei e bine să o păstrăm ca să știe angajatul ce să aducă

            return true; // Clientul a reușit să ia produsul
        }
        return false; // Raftul e gol, clientul pleacă supărat
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