using UnityEngine;
using TMPro;

public class ClockManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI dayText;    // Ex: "Ziua 1"
    [SerializeField] private TextMeshProUGUI clockText;  // Ex: "08:00"
    [SerializeField] private TextMeshProUGUI statusText; // Ex: "DESCHIS"

    [Header("Colors")]
    [SerializeField] private Color openColor = Color.green;
    [SerializeField] private Color closeColor = Color.red;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 1. Ne abonăm la evenimentele din TimeManager
        // Astfel, acest script reacționează DOAR când TimeManager "strigă" că s-a schimbat ceva.
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnMinuteChanged += UpdateClock;
            TimeManager.Instance.OnDayChanged += UpdateDay;
            TimeManager.Instance.OnShopOpen += SetShopOpen;
            TimeManager.Instance.OnShopClose += SetShopClosed;

            // 2. Inițializăm textul la start (ca să nu aștepte primul minut)
            UpdateDay();
            UpdateClock();

            // Verificăm starea inițială manual
            // (Poți adăuga o proprietate publică IsShopOpen în TimeManager pentru asta)
            UpdateStatusText(false);
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
            TimeManager.Instance.OnShopOpen -= SetShopOpen;
            TimeManager.Instance.OnShopClose -= SetShopClosed;
        }
    }

    // --- Metodele care actualizează efectiv Textul ---

    private void UpdateClock()
    {
        // Formatare: "00" asigură că ora 8 apare ca "08"
        clockText.text = TimeManager.Instance.GetFormattedTime();
    }

    private void UpdateDay()
    {
        dayText.text = "ZIUA " + TimeManager.Instance.CurrentDay;
    }

    private void SetShopOpen()
    {
        UpdateStatusText(true);
    }

    private void SetShopClosed()
    {
        UpdateStatusText(false);
    }

    private void UpdateStatusText(bool isOpen)
    {
        if (isOpen)
        {
            statusText.text = "DESCHIS";
            statusText.color = openColor;
        }
        else
        {
            statusText.text = "ÎNCHIS";
            statusText.color = closeColor;
        }
    }
}
