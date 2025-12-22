using UnityEngine;

public enum StationType { CashRegister, Storage, Shelf }

public class WorkStation : MonoBehaviour
{
    public StationType stationType;

    [Header("Setări Inventar (Doar pentru Rafturi)")]
    public int maxProducts = 20;
    public int currentProducts = 0;

    // Proprietate care ne spune rapid dacă raftul are nevoie de marfă
    public bool NeedsRestocking => stationType == StationType.Shelf && currentProducts < maxProducts;

    // Proprietate care ne spune dacă un client are ce cumpăra
    public bool HasProducts => stationType == StationType.Shelf && currentProducts > 0;

    // Metodă pentru Angajat (Pune marfă)
    public void AddProduct()
    {
        if (currentProducts < maxProducts)
        {
            currentProducts++;
            // Aici poți adăuga logică vizuală (să apară obiecte pe raft)
            Debug.Log($"{name}: Produs adăugat! ({currentProducts}/{maxProducts})");
        }
    }

    // Metodă pentru Client (Ia marfă)
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