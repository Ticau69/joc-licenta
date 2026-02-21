using UnityEngine;
using System;
using System.Collections.Generic;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Setări Timp")]
    [Tooltip("Durata unei zile în joc în secunde reale (20 min = 1200 sec)")]
    [Min(1f)]
    public float dayDurationInSeconds = 1200f;

    [Tooltip("Dacă vrei ca timpul să pornească la o anumită oră (ex: 8 = 08:00).")]
    [Range(0, 23)]
    public int startHour = 0;

    [Tooltip("Minute la start (ex: 30 = 08:30).")]
    [Range(0, 59)]
    public int startMinute = 0;

    [Header("Orar Magazin")]
    [Range(0, 23)] public int openHour = 8;
    [Range(0, 24)] public int closeHour = 22;

    // Proprietăți Publice (Read-Only)
    public float CurrentTimeOfDay { get; private set; } // 0.0 la 1.0
    public int CurrentDay { get; private set; } = 1;
    public int CurrentHour { get; private set; }
    public int CurrentMinute { get; private set; }

    // Evenimente (compatibilitate)
    public event Action OnHourChanged;
    public event Action OnMinuteChanged;
    public event Action OnDayChanged;
    public event Action OnShopOpen;
    public event Action OnShopClose;

    // Evenimente noi (mai utile pentru UI / sisteme)
    public event Action<int, int> OnMinuteChangedDetailed; // (hour, minute)
    public event Action<int> OnHourChangedDetailed;         // (hour)
    public event Action<int> OnDayChangedDetailed;          // (day)

    private float timer;
    private bool isShopOpen;

    private void Awake()
    {
        // Singleton sigur
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Inițializare timp
        SetTime(startHour, startMinute);

        // Inițializează starea magazinului în funcție de ora de start
        isShopOpen = IsWithinShopHours(CurrentHour);
        // dacă vrei să declanșezi event la start, decomentează:
        // if (isShopOpen) OnShopOpen?.Invoke(); else OnShopClose?.Invoke();
    }

    private void Update()
    {
        if (dayDurationInSeconds <= 0.01f)
            return;

        // 1) Contorizarea Timpului
        timer += Time.deltaTime;

        // 2) Progresul zilei (clamp)
        CurrentTimeOfDay = Mathf.Clamp01(timer / dayDurationInSeconds);

        // 3) Calcul ore/minute + evenimente
        CalculateGameTime();

        // 4) Final de zi (folosim >=, dar CurrentTimeOfDay e deja clamp)
        if (timer >= dayDurationInSeconds)
        {
            EndDay();
        }
    }

    private void CalculateGameTime()
    {
        int previousHour = CurrentHour;
        int previousMinute = CurrentMinute;

        // 0..1 -> 0..24
        float rawHour = CurrentTimeOfDay * 24f;

        // La finalul zilei (CurrentTimeOfDay=1) rawHour=24.0 -> vrem 23:59, nu 24:00
        if (rawHour >= 24f)
            rawHour = 23.999f;

        CurrentHour = Mathf.FloorToInt(rawHour);
        CurrentMinute = Mathf.FloorToInt((rawHour - CurrentHour) * 60f);

        if (previousMinute != CurrentMinute)
        {
            OnMinuteChanged?.Invoke();
            OnMinuteChangedDetailed?.Invoke(CurrentHour, CurrentMinute);
        }

        if (previousHour != CurrentHour)
        {
            OnHourChanged?.Invoke();
            OnHourChangedDetailed?.Invoke(CurrentHour);
            CheckShopStatus();
        }
    }

    private bool IsWithinShopHours(int hour)
    {
        // closeHour poate fi 24 (dacă vrei "până la miezul nopții")
        int effectiveClose = Mathf.Clamp(closeHour, 0, 24);

        // Caz simplu: deschis între openHour și closeHour în aceeași zi
        if (openHour < effectiveClose)
            return hour >= openHour && hour < effectiveClose;

        // Caz peste miezul nopții (ex: 20 -> 4)
        if (openHour > effectiveClose)
            return hour >= openHour || hour < effectiveClose;

        // openHour == closeHour -> considerăm închis tot timpul
        return false;
    }

    private void CheckShopStatus()
    {
        bool shouldBeOpen = IsWithinShopHours(CurrentHour);

        if (!isShopOpen && shouldBeOpen)
        {
            isShopOpen = true;
            OnShopOpen?.Invoke();

            if (SoundFXManager.Instance != null)
                SoundFXManager.Instance.PlaySound(SoundType.ShopOpen, transform);

            Debug.Log("Magazin Deschis!");
        }
        else if (isShopOpen && !shouldBeOpen)
        {
            isShopOpen = false;
            OnShopClose?.Invoke();

            if (SoundFXManager.Instance != null)
            {
                SoundFXManager.Instance.PlaySound(SoundType.ShopClose_Register, transform);
                SoundFXManager.Instance.PlaySound(SoundType.ShopClose_Jingle, transform);
            }

            Debug.Log("Magazin Închis!");
        }
    }

    private void EndDay()
    {
        // dacă ziua se termină cu magazinul deschis, închide-l corect (eveniment + sunete)
        if (isShopOpen)
        {
            isShopOpen = false;
            OnShopClose?.Invoke();

            if (SoundFXManager.Instance != null)
            {
                SoundFXManager.Instance.PlaySound(SoundType.ShopClose_Register, transform);
                SoundFXManager.Instance.PlaySound(SoundType.ShopClose_Jingle, transform);
            }
        }

        CurrentDay++;

        // reset pentru noua zi (păstrează aceleași startHour/minute)
        SetTime(startHour, startMinute);

        // update economie
        var inflationManager = ServiceLocator.Instance.Get<InflationManager>();
        if (inflationManager != null)
        {
            inflationManager.SimulateDay();
        }

        OnDayChanged?.Invoke();
        OnDayChangedDetailed?.Invoke(CurrentDay);

        Debug.Log($"Ziua {CurrentDay} a început!");
    }

    /// <summary>
    /// Setează timpul intern la o oră/minut și recalculă timerul aferent.
    /// </summary>
    public void SetTime(int hour, int minute)
    {
        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);

        CurrentHour = hour;
        CurrentMinute = minute;

        // convert hour/minute -> CurrentTimeOfDay -> timer
        float dayFraction = (hour + (minute / 60f)) / 24f;
        CurrentTimeOfDay = Mathf.Clamp01(dayFraction);
        timer = CurrentTimeOfDay * dayDurationInSeconds;
    }

    public string GetFormattedTime()
    {
        return $"{CurrentHour:00}:{CurrentMinute:00}";
    }
}
