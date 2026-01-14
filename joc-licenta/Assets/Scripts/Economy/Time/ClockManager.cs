using UnityEngine;
using UnityEngine.UIElements;
using System; // NECESAR pentru DateTime
using System.Globalization; // NECESAR pentru limba română (numele lunilor)

public class ClockManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;

    private Label dayText;
    private Label clockText;
    public DateTime currentDate { get; private set; }

    // --- CONFIGURARE DATA START ---
    // Anul, Luna, Ziua (2025, 3, 22)
    public DateTime startDate { get; private set; } = new DateTime(2025, 3, 22);

    private void Awake()
    {
        currentDate = startDate;
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        // Căutăm elementele UI
        dayText = root.Q<Label>("Date");   // Sau "Date", depinde cum ai numit Label-ul în UXML
        clockText = root.Q<Label>("Hour");
    }

    void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnMinuteChanged += UpdateClock;
            TimeManager.Instance.OnDayChanged += UpdateDay;

            // Inițializare imediată
            UpdateDay();
            UpdateClock();
        }
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnMinuteChanged -= UpdateClock;
            TimeManager.Instance.OnDayChanged -= UpdateDay;
        }
    }

    private void UpdateClock()
    {
        if (clockText != null)
        {
            // Format HH:MM (ex: 08:05)
            clockText.text = $"{TimeManager.Instance.CurrentHour:00}:{TimeManager.Instance.CurrentMinute:00}";
        }
    }

    private void UpdateDay()
    {
        if (dayText != null)
        {
            // 1. Calculăm câte zile au trecut de la început
            // Scădem 1 pentru că "Ziua 1" este chiar data de start
            int daysPassed = TimeManager.Instance.CurrentDay - 1;

            // 2. Adăugăm zilele la data de start
            DateTime currentDate = startDate.AddDays(daysPassed);

            // 3. Formatom textul în limba română
            // "dd" = ziua (2 cifre), "MMMM" = luna (nume complet), "yyyy" = anul
            CultureInfo roCulture = CultureInfo.CreateSpecificCulture("en-EN");

            // Rezultat: "22 MARTIE 2025"
            dayText.text = currentDate.ToString("dd MMMM yyyy", roCulture).ToUpper();
        }
    }
}