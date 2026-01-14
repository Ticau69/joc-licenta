using UnityEngine;

public enum StationType { CashRegister, Storage, Shelf }

public class WorkStation : MonoBehaviour
{
    public StationType stationType;

    // --- NOU: Punctul unde trebuie să stea angajatul ---
    [Header("Navigație")]
    public Transform interactionPoint;
    // ---------------------------------------------------

    [Header("Setări Inventar (Doar pentru Rafturi)")]
    public int maxProducts = 20;
    public int currentProducts = 0;

    // Proprietatea ajutătoare pentru a obține poziția corectă
    public Vector3 GetStandPosition()
    {
        // Dacă am setat un interactionPoint, îl folosim pe acela.
        // Dacă am uitat să îl setăm, folosim poziția obiectului curent ca fallback.
        if (interactionPoint != null) return interactionPoint.position;
        return transform.position;
    }

    public bool NeedsRestocking => stationType == StationType.Shelf && currentProducts < maxProducts;
    public bool HasProducts => stationType == StationType.Shelf && currentProducts > 0;

    public void AddProduct()
    {
        if (currentProducts < maxProducts)
        {
            currentProducts++;
            Debug.Log($"{name}: Produs adăugat! ({currentProducts}/{maxProducts})");
        }
    }

    public bool TakeProduct()
    {
        if (currentProducts > 0)
        {
            currentProducts--;
            Debug.Log($"{name}: Produs vândut! ({currentProducts}/{maxProducts})");
            return true;
        }
        return false;
    }
}