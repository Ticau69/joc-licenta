using UnityEngine;
using UnityEngine.UIElements;

public class ClockManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    private Label dayText;
    private Label clockText;
    private VisualElement LeftPanel;

    [Header("Colors")]
    [SerializeField] private Color openColor = Color.green;
    [SerializeField] private Color closeColor = Color.red;

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        var hotbar = root.Q<VisualElement>("HotBar");
        LeftPanel = hotbar.Q<VisualElement>("LeftPanel");

        dayText = LeftPanel.Q<Label>("Day");
        clockText = LeftPanel.Q<Label>("Time");
    }

    void Start()
    {
        // 1. Ne abonăm la evenimentele din TimeManager
        // Astfel, acest script reacționează DOAR când TimeManager "strigă" că s-a schimbat ceva.
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnMinuteChanged += UpdateClock;
            TimeManager.Instance.OnDayChanged += UpdateDay;
            //TimeManager.Instance.OnShopOpen += SetShopOpen;
            //TimeManager.Instance.OnShopClose += SetShopClosed;

            // 2. Inițializăm textul la start (ca să nu aștepte primul minut)
            UpdateDay();
            UpdateClock();

            // Verificăm starea inițială manual
            // (Poți adăuga o proprietate publică IsShopOpen în TimeManager pentru asta)
            //UpdateStatusText(false);
        }
    }

    void OnDestroy()
    {
        // 3. FOARTE IMPORTANT: Ne dezabonăm când obiectul e distrus
        // Dacă nu faci asta, vor apărea erori când schimbi scena.
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnMinuteChanged -= UpdateClock;
            TimeManager.Instance.OnDayChanged -= UpdateDay;
            //TimeManager.Instance.OnShopOpen -= SetShopOpen;
            //TimeManager.Instance.OnShopClose -= SetShopClosed;
        }
    }



    // --- Metodele care actualizează efectiv Textul ---

    private void UpdateClock()
    {
        // Formatare: "00" asigură că ora 8 apare ca "08"
        if (clockText != null)
        {
            clockText.text = "Clock: " + TimeManager.Instance.CurrentHour.ToString("00") + ":" + TimeManager.Instance.CurrentMinute.ToString("00");
        }
    }

    private void UpdateDay()
    {
        if (dayText != null)
        {
            dayText.text = "DAY " + TimeManager.Instance.CurrentDay;
        }
    }

    // private void SetShopOpen()
    // {
    //     UpdateStatusText(true);
    // }

    // private void SetShopClosed()
    // {
    //     UpdateStatusText(false);
    // }

    // private void UpdateStatusText(bool isOpen)
    // {
    //     if (isOpen)
    //     {
    //         statusText.text = "DESCHIS";
    //         statusText.color = openColor;
    //     }
    //     else
    //     {
    //         statusText.text = "ÎNCHIS";
    //         statusText.color = closeColor;
    //     }
    // }
}
