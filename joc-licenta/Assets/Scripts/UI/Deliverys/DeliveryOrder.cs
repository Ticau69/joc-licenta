using UnityEngine;

[System.Serializable]
public class DeliveryOrder
{
    public string id;               // ID unic (ex: "CMD-104")
    public ProductType product;     // Ce aduce
    public int amount;              // Cantitate
    public float totalTime;         // Cât durează total
    public float timeRemaining;     // Cât a mai rămas

    public bool IsCompleted => timeRemaining <= 0;

    public DeliveryOrder(ProductType type, int qty, float duration)
    {
        id = $"CMD-{Random.Range(100, 999)}";
        product = type;
        amount = qty;
        totalTime = duration;
        timeRemaining = duration;
    }
}