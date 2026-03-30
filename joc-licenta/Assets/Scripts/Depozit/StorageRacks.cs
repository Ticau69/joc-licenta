using UnityEngine;
using System.Collections.Generic;

public class StorageRacks : MonoBehaviour
{
    [Header("Configurare Raft Depozit")]
    [Tooltip("Câte cutii încap maxim pe acest raft?")]
    public int maxBoxes = 4;
    [Tooltip("Câte produse încap într-o singură cutie?")]
    public int maxAmountPerBox = 50;

    [Header("Vizual")]
    [Tooltip("Punctele de pe raft unde vor apărea cutiile (Empty GameObjects)")]
    public Transform[] boxSpawnPoints;
    [Tooltip("Prefab-ul unei cutii de carton (Storage Box)")]
    public GameObject boxPrefab;

    // Lista cu ce cutii avem efectiv pe raft acum
    public List<StorageBox> storedBoxes = new List<StorageBox>();

    /// <summary>
    /// Încearcă să pună marfă pe raft (ex: când vine camionul).
    /// </summary>
    public int AddProduct(ProductType type, int amountToAdd)
    {
        int remainingAmount = amountToAdd;

        // 1. Mai întâi încercăm să umplem cutiile DEJA EXISTENTE cu acest produs
        foreach (var box in storedBoxes)
        {
            if (box.Product == type && box.Amount < maxAmountPerBox)
            {
                int spaceLeft = maxAmountPerBox - box.Amount;
                int amountToPut = Mathf.Min(spaceLeft, remainingAmount);

                box.Amount += amountToPut;
                remainingAmount -= amountToPut;

                if (remainingAmount <= 0) return 0; // Am pus tot!
            }
        }

        // 2. Dacă a mai rămas marfă, creăm CUTII NOI (dacă avem loc pe raft)
        while (remainingAmount > 0 && storedBoxes.Count < maxBoxes && storedBoxes.Count < boxSpawnPoints.Length)
        {
            int amountToPut = Mathf.Min(maxAmountPerBox, remainingAmount);

            // Creăm vizual cutia de carton
            Transform spawnPoint = boxSpawnPoints[storedBoxes.Count];
            GameObject newVisualBox = null;

            if (boxPrefab != null)
            {
                newVisualBox = Instantiate(boxPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
                // Opțional: Aici am putea schimba textura cutiei în funcție de Produs
            }

            // Înregistrăm cutia în memorie
            storedBoxes.Add(new StorageBox
            {
                Product = type,
                Amount = amountToPut,
                VisualBox = newVisualBox
            });

            remainingAmount -= amountToPut;
        }

        // Returnăm cât a rămas nepus (dacă raftul s-a umplut)
        return remainingAmount;
    }

    /// <summary>
    /// Angajatul ia marfă din depozit ca să ducă în magazin.
    /// </summary>
    public int TakeProduct(ProductType type, int requestedAmount)
    {
        int amountTaken = 0;

        // Căutăm de la coadă la cap ca să ștergem cutiile goale mai ușor
        for (int i = storedBoxes.Count - 1; i >= 0; i--)
        {
            var box = storedBoxes[i];
            if (box.Product == type)
            {
                int availableToTake = Mathf.Min(box.Amount, requestedAmount - amountTaken);
                box.Amount -= availableToTake;
                amountTaken += availableToTake;

                // Dacă s-a golit cutia de carton, o distrugem!
                if (box.Amount <= 0)
                {
                    if (box.VisualBox != null) Destroy(box.VisualBox);
                    storedBoxes.RemoveAt(i);
                }

                if (amountTaken >= requestedAmount) break;
            }
        }

        return amountTaken;
    }

    /// <summary>
    /// Verifică cât stoc există din acest produs pe acest raft specific.
    /// </summary>
    public int GetStockAmount(ProductType type)
    {
        int total = 0;
        foreach (var box in storedBoxes)
        {
            if (box.Product == type) total += box.Amount;
        }
        return total;
    }
}

[System.Serializable]
public class StorageBox
{
    public ProductType Product;
    public int Amount;
    public GameObject VisualBox; // Referința către cutia 3D de pe raft
}
