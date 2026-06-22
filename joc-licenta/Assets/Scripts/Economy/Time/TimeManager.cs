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
    public int startHour = 6;

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
    public bool isTimePaused = false;

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
        // 1. Oprim timpul dacă suntem în pauza de raport
        if (dayDurationInSeconds <= 0.01f || isTimePaused)
            return;

        timer += Time.deltaTime;

        // 2. NOU: Când ajungem la 24 de ore (dayDurationInSeconds), 
        // trecem de miezul nopții natural și o luăm de la 00:00!
        if (timer >= dayDurationInSeconds)
        {
            timer -= dayDurationInSeconds; // Păstrăm restul de milisecunde pentru o trecere fluidă
            AdvanceToNextDayNatural();
        }

        CurrentTimeOfDay = Mathf.Clamp01(timer / dayDurationInSeconds);
        CalculateGameTime();
    }

    public void SetDay(int day)
    {
        CurrentDay = day;
        // Anunțăm pe toată lumea că ziua s-a schimbat
        OnDayChanged?.Invoke();
    }

    private void AdvanceToNextDayNatural()
    {
        CurrentDay++;

        // update economie
        var inflationManager = ServiceLocator.Instance.Get<InflationManager>();
        if (inflationManager != null)
        {
            inflationManager.SimulateDay();
        }

        OnDayChanged?.Invoke();
        OnDayChangedDetailed?.Invoke(CurrentDay);

        Debug.Log($"Miezul Nopții! Ziua {CurrentDay} a început.");
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

    public void EndDay()
    {
        // dacă ziua se termină cu magazinul deschis, închide-l corect
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

        // Calculăm matematic ora de start (ex: 08:00 = 0.33 dintr-o zi întreagă)
        float targetDayFraction = (startHour + (startMinute / 60f)) / 24f;

        // Dacă e ora 22:00 și apăsăm Skip, timpul nostru actual (ex: 0.91) e mai mare 
        // decât ținta (0.33). Asta înseamnă că vom sări peste 00:00, deci avansăm ziua.
        // Dar dacă am stat treji până la 02:00 AM (0.08), ziua a avansat deja natural,
        // așa că doar sărim direct la 08:00 fără să mai adăugăm o zi!
        if (CurrentTimeOfDay > targetDayFraction)
        {
            AdvanceToNextDayNatural();
        }

        // Setăm timpul la ora de start (ex: 08:00)
        SetTime(startHour, startMinute);
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
