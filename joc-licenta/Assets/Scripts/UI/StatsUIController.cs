using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class StatsUIController : MonoBehaviour
{
    [SerializeField] private UIDocument gameUIDoc;
    private GameManager gameManager;
    private ClockManager clockManager;
    private TimeManager timeManager;
    private BarChart barChart; // Am schimbat tipul aici
    private Label maxValLabel; // Doar o etichetă sus e suficientă la BarChart
    private Button statsButton;
    private VisualElement statsPanel; // Păstrăm referința aici
    private bool isStatsPanelOpen = false; // Ținem minte starea

    // Lista cu datele pe zile
    private List<DayData> history = new List<DayData>();

    // Tracking pentru ziua curentă
    private float lastKnownMoney;
    private float currentDayIncome = 0;
    private float currentDayExpense = 0;

    void Start()
    {
        if (gameUIDoc == null) gameUIDoc = GetComponent<UIDocument>();
        var root = gameUIDoc.rootVisualElement;

        // Găsim BarChart-ul (asigură-te că are numele "ProfitChart" în UXML)
        barChart = root.Q<BarChart>("ProfitChart");
        maxValLabel = root.Q<Label>("MaxMoneyLabel"); // Folosim label-ul de sus
        statsButton = root.Q<Button>("Stats");
        statsPanel = root.Q<VisualElement>("StatsPanel");

        gameManager = GetComponent<GameManager>();
        clockManager = GetComponent<ClockManager>();
        timeManager = GetComponent<TimeManager>();

        if (gameManager != null && barChart != null)
        {
            gameManager.OnMoneyChanged += UpdateCurrentDayStats;

            // Setăm referința de bani
            lastKnownMoney = gameManager.CurrentMoney;

            // --- GENERARE DATE FICTIVE PENTRU TEST ---
            // Ca să nu arate gol la început, simulăm "ultimele 5 zile"
            history.Clear();

            // Ziua Curentă (Ziua 5) - Începe de la 0
            history.Add(new DayData(clockManager.currentDate.ToString("dd MMM"), 0, 0));

            UpdateChartDisplay();
        }

        // Buton pentru deschiderea/închiderea meniului de statistici
        if (statsPanel != null)
        {
            statsPanel.style.display = DisplayStyle.None; // Îl ascundem vizual
            isStatsPanelOpen = false; // Setăm variabila pe false
        }

        if (statsButton != null)
        {
            statsButton.clicked += () =>
            {
                if (statsPanel == null) return;

                // Inversăm starea (True -> False, False -> True)
                isStatsPanelOpen = !isStatsPanelOpen;

                // Aplicăm vizual
                if (isStatsPanelOpen)
                    statsPanel.style.display = DisplayStyle.Flex;
                else
                    statsPanel.style.display = DisplayStyle.None;
            };
        }

        if (timeManager != null)
        {
            // Dacă evenimentul tău se numește 'OnDayChanged' sau 'OnShopOpen'
            // Folosește evenimentul care marchează ÎNCEPUTUL unei noi zile de muncă
            timeManager.OnDayChanged += OnNewDayStarted; // <--- LINIE NOUĂ IMPORTANTA
        }
    }

    void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnMoneyChanged -= UpdateCurrentDayStats;

        if (timeManager != null)
        {
            timeManager.OnDayChanged -= OnNewDayStarted; // <--- LINIE NOUĂ
        }
    }

    // Se apelează la fiecare tranzacție
    private void UpdateCurrentDayStats()
    {
        float currentMoney = gameManager.CurrentMoney;
        float diff = currentMoney - lastKnownMoney;

        // Identificăm dacă e venit sau cheltuială
        if (diff > 0)
        {
            currentDayIncome += diff;
        }
        else if (diff < 0)
        {
            currentDayExpense += Mathf.Abs(diff);
        }

        lastKnownMoney = currentMoney;

        // Actualizăm ULTIMA intrare din listă (Ziua Curentă)
        int lastIndex = history.Count - 1;
        string currentDateLabel = history[lastIndex].DateLabel;
        history[lastIndex] = new DayData(currentDateLabel, currentDayIncome, currentDayExpense);

        UpdateChartDisplay();
    }

    // Funcție opțională: Când se termină ziua în joc (TimeManager.OnDayChanged)
    // Poți apela asta ca să începi o bară nouă curată
    public void OnNewDayStarted()
    {
        // Resetăm tracker-ul zilnic
        currentDayIncome = 0;
        currentDayExpense = 0;

        // --- FIX AICI ---
        // Nu citim clockManager.currentDate (care e posibil să fie veche).
        // Calculăm data direct pe baza numărului zilei curente.

        int daysPassed = timeManager.CurrentDay - 1;
        System.DateTime newDate = clockManager.startDate.AddDays(daysPassed);

        // Folosim data calculată proaspăt
        history.Add(new DayData(newDate.ToString("dd MMM"), 0, 0));

        // Dacă lista e prea lungă, ștergem ziua cea mai veche
        if (history.Count > 5) history.RemoveAt(0);

        UpdateChartDisplay();
    }

    private void UpdateChartDisplay()
    {
        if (barChart == null) return;

        // Trimitem datele la grafic
        barChart.SetData(history);

        // Actualizăm Label-ul de sus cu cea mai mare valoare (pentru scară)
        float maxVal = 0;
        foreach (var d in history)
        {
            if (d.Income > maxVal) maxVal = d.Income;
            if (d.Expense > maxVal) maxVal = d.Expense;
        }
        if (maxValLabel != null) maxValLabel.text = maxVal.ToString("0") + " RON";
    }
}