using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DeliveryOrder
{
    public string id;

    // --- SCHIMBARE: Acum ține o listă cu toate produsele din comandă ---
    public Dictionary<ProductType, int> products;

    public float totalTime;
    public float timeRemaining;

    public bool IsCompleted => timeRemaining <= 0;

    // --- SCHIMBARE: Constructorul primește dicționarul ---
    public DeliveryOrder(Dictionary<ProductType, int> items, float duration)
    {
        id = $"CMD-{Random.Range(100, 999)}";

        // Copiem itemele pentru a le salva în această comandă
        products = new Dictionary<ProductType, int>(items);

        totalTime = duration;
        timeRemaining = duration;
    }
}