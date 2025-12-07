using UnityEngine;
using System;
using TMPro;

public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    [Header("Configurare")]
    [SerializeField] private int maxCapacity = 500;
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI powerConsumptionText;

    public int CurrentConsumption { get; private set; } = 0;
    public int MaxCapacity => maxCapacity;
    public bool IsPowerOn { get; private set; } = true;

    public event Action OnPowerGridUpdated;
    public event Action<bool> OnPowerStateChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        OnPowerGridUpdated += UpdateUI;
    }

    void Start()
    {
        UpdateUI();
    }

    void OnDestroy()
    {
        OnPowerGridUpdated -= UpdateUI;
    }

    public void RegisterConsumer(int consumptionAmount)
    {
        CurrentConsumption += consumptionAmount;
        CheckPowerState();
        OnPowerGridUpdated?.Invoke();
        Debug.Log($"Consum actual de energie: {CurrentConsumption} kW / {maxCapacity} kW");
    }

    public void UnregisterConsumer(int consumptionAmount)
    {
        CurrentConsumption -= consumptionAmount;
        if (CurrentConsumption < 0) CurrentConsumption = 0;

        CheckPowerState();
        OnPowerGridUpdated?.Invoke();
    }

    public void UpgradeCapacity(int amount)
    {
        maxCapacity += amount;
        CheckPowerState();
        OnPowerGridUpdated?.Invoke();
    }

    private void UpdateUI()
    {
        if (powerConsumptionText != null)
        {
            powerConsumptionText.color = IsPowerOn ? Color.white : Color.red;
            powerConsumptionText.text = $"Curent: {CurrentConsumption} kW / {maxCapacity} kW";
        }
    }

    private void CheckPowerState()
    {
        bool shouldBeOn = CurrentConsumption <= maxCapacity;

        if (IsPowerOn != shouldBeOn)
        {
            IsPowerOn = shouldBeOn;
            OnPowerStateChanged?.Invoke(IsPowerOn);

            if (!IsPowerOn)
            {
                Debug.Log("BLACKOUT! Ai depășit capacitatea electrică!");
                // Aici poți opri muzica, stinge luminile globale, etc.
            }
            else
            {
                Debug.Log("Curentul a revenit.");
            }
        }
    }

}
