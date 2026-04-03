using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DeliveryOrder
{
    public string id;
    public Dictionary<ProductType, int> products;

    public float totalTime;
    public float timeRemaining;

    // --- NOU: Flag pentru comenzile de noapte ---
    public bool isNightOrder;

    // O comandă e gata fie când timer-ul ajunge la 0 (ziua), fie dacă e forțată (dimineața)
    public bool IsCompleted => timeRemaining <= 0;

    public DeliveryOrder(Dictionary<ProductType, int> items, float duration, bool nightOrder)
    {
        id = $"CMD-{Random.Range(100, 999)}";
        products = new Dictionary<ProductType, int>(items);
        totalTime = duration;
        timeRemaining = duration;
        isNightOrder = nightOrder; // Salvăm dacă e comandă de noapte
    }
}