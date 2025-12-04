using UnityEngine;
using System;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Setări Timp")]
    [Tooltip("Durata unei zile în joc în secunde reale (20 min = 1200 sec)")]
    public float dayDurationInSeconds = 1200f;

    [Header("Orar Magazin")]
    public int openHour = 8;
    public int closeHour = 22;

    // Proprietăți Publice (Read-Only)
    public float CurrentTimeOfDay { get; private set; } // 0.0 la 1.0 (0% la 100% din zi)
    public int CurrentDay { get; private set; } = 1;
    public int CurrentHour { get; private set; }
    public int CurrentMinute { get; private set; }

    // Evenimente la care se pot abona alte sisteme (ex: Clienții, UI-ul)
    public event Action OnHourChanged;
    public event Action OnMinuteChanged;
    public event Action OnDayChanged;
    public event Action OnShopOpen;
    public event Action OnShopClose;

    private float timer;
    private bool isShopOpen = false;

    void Awake()
    {
        // Singleton Pattern
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // 1. Contorizarea Timpului
        timer += Time.deltaTime;

        // 2. Calculul Progresului Zilei (0.0 -> 1.0)
        CurrentTimeOfDay = timer / dayDurationInSeconds;

        // 3. Calculul Orelor și Minutelor din Joc (00:00 -> 24:00)
        CalculateGameTime();

        // 4. Verificarea Sfârșitului de Zi
        if (timer >= dayDurationInSeconds)
        {
            EndDay();
        }
    }

    private void CalculateGameTime()
    {
        // O zi are 24 ore. Timpul (0-1) * 24 = Ora curentă
        float timeMultiplier = 24f;

        int previousHour = CurrentHour;
        int previousMinute = CurrentMinute;

        // Matematica: Transformăm 0.5 (miezul zilei) în ora 12
        float rawHour = CurrentTimeOfDay * timeMultiplier;
        CurrentHour = Mathf.FloorToInt(rawHour);

        // Minutele: Restul zecimal din oră * 60
        CurrentMinute = Mathf.FloorToInt((rawHour - CurrentHour) * 60);

        // --- DECLANȘARE EVENIMENTE ---

        // Dacă s-a schimbat minutul
        if (previousMinute != CurrentMinute)
        {
            OnMinuteChanged?.Invoke();
        }

        // Dacă s-a schimbat ora
        if (previousHour != CurrentHour)
        {
            OnHourChanged?.Invoke();
            CheckShopStatus(); // Verificăm dacă deschidem/închidem
        }
    }

    private void CheckShopStatus()
    {
        if (!isShopOpen && CurrentHour >= openHour && CurrentHour < closeHour)
        {
            isShopOpen = true;
            OnShopOpen?.Invoke(); // "Ding! Magazinul s-a deschis!"
            Debug.Log("Magazin Deschis!");
        }
        else if (isShopOpen && CurrentHour >= closeHour)
        {
            isShopOpen = false;
            OnShopClose?.Invoke(); // "Gata programul, nu mai intră clienți!"
            Debug.Log("Magazin Închis!");
        }
    }

    private void EndDay()
    {
        CurrentDay++;
        timer = 0;
        CurrentTimeOfDay = 0;

        // Resetăm starea magazinului pentru siguranță
        isShopOpen = false;

        OnDayChanged?.Invoke(); // Aici poți deschide panoul de "Raport Zilnic"
        Debug.Log($"Ziua {CurrentDay} a început!");
    }

    // Helper pentru formatare UI (ex: "14:05")
    public string GetFormattedTime()
    {
        return $"{CurrentHour:00}:{CurrentMinute:00}";
    }
}
