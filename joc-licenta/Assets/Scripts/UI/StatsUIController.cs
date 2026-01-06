using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class StatsUIController : MonoBehaviour
{
    [SerializeField] private UIDocument gameUIDoc;
    private Button openStatsButton;
    private VisualElement statsPanel;
    private GameManager gameManager;

    private LineChart profitChart;
    private Label maxMoneyLabel;
    private Label minMoneyLabel;
    private Label startTimeLabel;
    private List<float> balanceHistory = new List<float>();
    private List<float> expensesHistory = new List<float>();

    // Câte puncte păstrăm în istoric? (ca să nu devină linia o mâzgălitură)
    private int maxDataPoints = 50;

    void Start()
    {
        if (gameUIDoc == null) gameUIDoc = GetComponent<UIDocument>();

        // Așteptăm ca UI-ul să fie gata
        var root = gameUIDoc.rootVisualElement;

        openStatsButton = root.Q<Button>("Stats");
        statsPanel = root.Q<VisualElement>("StatsPanel");

        profitChart = root.Q<LineChart>("ProfitChart");
        maxMoneyLabel = root.Q<Label>("MaxMoneyLabel");
        minMoneyLabel = root.Q<Label>("MinMoneyLabel");
        startTimeLabel = root.Q<Label>("StartTimeLabel");

        // 1. Încercăm să găsim graficul
        profitChart = root.Q<LineChart>("ProfitChart"); // <--- Verifică dacă acest nume e corect în UXML!

        gameManager = GetComponent<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnMoneyChanged += UpdateChartData;

            // Date inițiale
            float startMoney = gameManager.CurrentMoney;
            balanceHistory.Add(startMoney); balanceHistory.Add(startMoney);

            // Inițializăm cheltuielile cu 0
            expensesHistory.Add(0); expensesHistory.Add(0);

            UpdateChartData();
        }

        if (openStatsButton != null)
        {
            openStatsButton.clicked += () =>
            {
                ToggleStatsPanel();
            };
        }
    }

    private void ToggleStatsPanel()
    {
        if (statsPanel != null)
        {
            bool isVisible = statsPanel.style.display == DisplayStyle.Flex;
            statsPanel.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnMoneyChanged -= UpdateChartData;
        }
    }

    private void UpdateChartData()
    {
        if (profitChart == null) return;

        // 1. Actualizăm Istoricul Balanței
        float currentMoney = gameManager.CurrentMoney;
        balanceHistory.Add(currentMoney);
        if (balanceHistory.Count > maxDataPoints) balanceHistory.RemoveAt(0);

        // 2. Actualizăm Istoricul Cheltuielilor (Logică Exemplu)
        // Aici ar trebui să iei o variabilă reală, ex: GameManager.Instance.TotalExpenses
        // De dragul exemplului, generez o valoare fluctuantă mică
        float currentExpense = Random.Range(100f, 500f);
        expensesHistory.Add(currentExpense);
        if (expensesHistory.Count > maxDataPoints) expensesHistory.RemoveAt(0);


        // 3. DESENARE MULTI-LINE

        // A. Ștergem ce era înainte
        profitChart.ClearData();

        // B. Adăugăm Linia VERDE (Profit/Balanță)
        // Width: 4px, Culoare: Verde
        profitChart.AddSeries(balanceHistory, new Color(0, 1, 0, 1), 4f);

        // C. Adăugăm Linia ROȘIE (Cheltuieli)
        // Width: 2px, Culoare: Roșu
        profitChart.AddSeries(expensesHistory, new Color(1, 0.3f, 0.3f, 1), 2f);

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        // Trebuie să găsim Min și Max GLOBAL (din toate listele active)
        float globalMax = float.MinValue;
        float globalMin = float.MaxValue;

        // Verificăm lista de Balanță
        if (balanceHistory.Count > 0)
        {
            float maxB = balanceHistory.Max();
            float minB = balanceHistory.Min();
            if (maxB > globalMax) globalMax = maxB;
            if (minB < globalMin) globalMin = minB;
        }

        // Verificăm lista de Cheltuieli
        if (expensesHistory.Count > 0)
        {
            float maxE = expensesHistory.Max();
            float minE = expensesHistory.Min();
            if (maxE > globalMax) globalMax = maxE;
            if (minE < globalMin) globalMin = minE;
        }

        // Dacă nu avem date, punem 0
        if (globalMax == float.MinValue) globalMax = 100;
        if (globalMin == float.MaxValue) globalMin = 0;

        // Setăm textul în UI
        if (maxMoneyLabel != null) maxMoneyLabel.text = $"{globalMax:0} RON";
        if (minMoneyLabel != null) minMoneyLabel.text = $"{globalMin:0} RON";

        if (startTimeLabel != null)
        {
            // Luăm count-ul de la lista cea mai lungă (de obicei sunt egale)
            int count = Mathf.Max(balanceHistory.Count, expensesHistory.Count);
            startTimeLabel.text = $"-{count} Tranzacții";
        }
    }
}