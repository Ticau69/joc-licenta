using UnityEngine;
using System;

public class FleetManager : MonoBehaviour
{
    [Header("Configurare Flotă")]
    [SerializeField] private int initialTrucks = 2;       // Începi cu 2 camioane
    [SerializeField] private int maxTrucksLimit = 10;     // Maxim absolut
    [SerializeField] private int upgradeCostBase = 500;   // Cât costă primul upgrade
    [SerializeField] private float costMultiplier = 1.5f; // Cât de mult crește prețul (x1.5)

    // State
    public int CurrentMaxTrucks { get; private set; }
    public int ActiveTrucks { get; private set; }
    private int currentLevel = 0;

    // Eveniment pentru UI (ca să știe DeliveryManager să actualizeze textul 3/5)
    public event Action OnFleetStatusChanged;

    void Awake()
    {
        CurrentMaxTrucks = initialTrucks;
    }

    // --- LOGICA DE UTILIZARE ---

    public bool HasAvailableTrucks(int amountNeeded = 1)
    {
        return (ActiveTrucks + amountNeeded) <= CurrentMaxTrucks;
    }

    public void RentTruck()
    {
        if (ActiveTrucks < CurrentMaxTrucks)
        {
            ActiveTrucks++;
            OnFleetStatusChanged?.Invoke();
        }
        else
        {
            Debug.LogError("[FleetManager] Eroare: Încerci să folosești un camion inexistent!");
        }
    }

    public void ReturnTruck()
    {
        if (ActiveTrucks > 0)
        {
            ActiveTrucks--;
            OnFleetStatusChanged?.Invoke();
        }
    }

    // --- LOGICA DE UPGRADE ---

    public int GetNextUpgradeCost()
    {
        // Formula: 500 * (1.5 ^ Level)
        return Mathf.RoundToInt(upgradeCostBase * Mathf.Pow(costMultiplier, currentLevel));
    }

    public bool CanUpgrade()
    {
        return CurrentMaxTrucks < maxTrucksLimit;
    }

    public void TryUpgradeFleet()
    {
        if (!CanUpgrade()) return;

        int cost = GetNextUpgradeCost();

        if (GameManager.Instance.TrySpendMoney(cost))
        {
            currentLevel++;
            CurrentMaxTrucks++; // Adăugăm un camion nou
            Debug.Log($"[Fleet] Flotă upgradată! Acum ai {CurrentMaxTrucks} camioane.");

            OnFleetStatusChanged?.Invoke();
        }
        else
        {
            Debug.Log("[Fleet] Nu ai destui bani pentru upgrade!");
        }
    }
}