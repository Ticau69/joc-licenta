using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

// Definim TOATE categoriile posibile de pe care pot pleca sau veni bani
public enum TransactionCategory
{
    Venituri_Vanzari,
    Salarii_Angajati,
    Marfa_Depozit,
    Constructii_Teren,
    Mobilier_Echipamente,
    Amenzi,
}

public class FinanceManager : MonoBehaviour
{
    public static FinanceManager Instance { get; private set; }

    [SerializeField] private UIDocument endDayReportPanel;
    private FinancePieChart myPieChart;
    private Label noActivityLabel;

    private Dictionary<TransactionCategory, int> dailyLedger = new Dictionary<TransactionCategory, int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        endDayReportPanel.gameObject.SetActive(true);
        var root = endDayReportPanel.rootVisualElement;

        myPieChart = root.Q<FinancePieChart>("FinanceChart");
        noActivityLabel = root.Q<Label>("NoActivityLabel");
        if (noActivityLabel != null) noActivityLabel.style.display = DisplayStyle.None;

        // Conectăm butoanele
        Button nextDayBtn = root.Q<Button>("StartNextDay");
        if (nextDayBtn != null)
        {
            nextDayBtn.clicked -= StartNextDay;
            nextDayBtn.clicked += StartNextDay;
        }

        Button waitNextDayBtn = root.Q<Button>("WaitForNextDay");
        if (waitNextDayBtn != null)
        {
            waitNextDayBtn.clicked -= ContinueNightShift;
            waitNextDayBtn.clicked += ContinueNightShift;
        }

        root.style.display = DisplayStyle.None;
    }

    private void OnEnable() => CustomerSpawner.OnStoreCompletelyEmpty += TriggerEndOfDaySequence;
    private void OnDisable() => CustomerSpawner.OnStoreCompletelyEmpty -= TriggerEndOfDaySequence;

    // ==========================================
    // NOU: FUNCȚIA UNIVERSALĂ DE BANI
    // ==========================================
    public void RegisterTransaction(TransactionCategory category, int amount)
    {
        // Dacă e prima dată azi când cheltuim pe categoria asta, o adăugăm în dicționar
        if (!dailyLedger.ContainsKey(category))
        {
            dailyLedger[category] = 0;
        }

        // Adăugăm suma
        dailyLedger[category] += amount;
    }

    // ==========================================
    // GENERAREA AUTOMATĂ A GRAFICULUI
    // ==========================================
    private void TriggerEndOfDaySequence()
    {
        if (EmployeeManager.Instance != null) EmployeeManager.Instance.EndWorkDay();
        if (TimeManager.Instance != null) TimeManager.Instance.isTimePaused = true;

        if (myPieChart != null)
        {
            List<ChartSlice> todayExpenses = new List<ChartSlice>();

            // Parcurgem automat TOATĂ lista de cheltuieli (fără if-uri kilometrice!)
            foreach (var transaction in dailyLedger)
            {
                // Ignorăm veniturile din graficul de cheltuieli
                if (transaction.Key == TransactionCategory.Venituri_Vanzari) continue;

                if (transaction.Value > 0)
                {
                    todayExpenses.Add(new ChartSlice
                    {
                        Name = FormatCategoryName(transaction.Key),
                        Value = transaction.Value,
                        SliceColor = GetColorForCategory(transaction.Key)
                    });
                }
            }

            // Empty State Logic
            if (todayExpenses.Count == 0)
            {
                myPieChart.style.display = DisplayStyle.None;
                if (noActivityLabel != null) noActivityLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                myPieChart.style.display = DisplayStyle.Flex;
                myPieChart.SetData(todayExpenses);
                if (noActivityLabel != null) noActivityLabel.style.display = DisplayStyle.None;
            }
        }

        int totalVenituri = 0;
        int totalCheltuieli = 0;

        foreach (var transaction in dailyLedger)
        {
            if (transaction.Key == TransactionCategory.Venituri_Vanzari)
                totalVenituri += transaction.Value;
            else
                totalCheltuieli += transaction.Value;
        }

        int profitNet = totalVenituri - totalCheltuieli;

        var root = endDayReportPanel.rootVisualElement;

        // Căutăm etichetele în interfață (ASIGURĂ-TE CĂ SE NUMESC AȘA ÎN UI BUILDER!)
        Label venituriLabel = root.Q<Label>("SalesLabel");
        if (venituriLabel != null) venituriLabel.text = $"+{totalVenituri} RON";

        Label cheltuieliLabel = root.Q<Label>("ExpensesLabel");
        if (cheltuieliLabel != null) cheltuieliLabel.text = $"-{totalCheltuieli} RON";

        Label profitLabel = root.Q<Label>("ProfitLabel");
        if (profitLabel != null) profitLabel.text = $"{profitNet} RON";

        // Notificăm sistemul de obiective cu profitul zilei
        ContextualObjectiveSystem.Instance?.NotifyDayProfit(profitNet);

        endDayReportPanel.rootVisualElement.style.display = DisplayStyle.Flex;
    }

    // Funcții utilitare pentru culori și nume frumos formatate
    private string FormatCategoryName(TransactionCategory cat) => cat.ToString().Replace("_", " ");

    private Color GetColorForCategory(TransactionCategory cat)
    {
        return cat switch
        {
            TransactionCategory.Salarii_Angajati => new Color(0.2f, 0.6f, 1f), // Albastru
            TransactionCategory.Marfa_Depozit => new Color(1f, 0.6f, 0.2f),    // Portocaliu
            TransactionCategory.Constructii_Teren => new Color(0.8f, 0.2f, 0.2f), // Roșu
            TransactionCategory.Mobilier_Echipamente => new Color(0.6f, 0.4f, 0.8f), // Mov
            TransactionCategory.Amenzi => new Color(1f, 0f, 0f), // Roșu
            _ => Color.gray
        };
    }

    /// <summary>Returnează profitul net al zilei curente (venituri - cheltuieli).</summary>
    public int GetTodayProfit()
    {
        int venituri = 0, cheltuieli = 0;
        foreach (var t in dailyLedger)
        {
            if (t.Key == TransactionCategory.Venituri_Vanzari) venituri += t.Value;
            else cheltuieli += t.Value;
        }
        return venituri - cheltuieli;
    }

    // Resetăm tot registrul a doua zi
    public void StartNextDay()
    {
        dailyLedger.Clear(); // <-- Curăță tot instant!
        endDayReportPanel.rootVisualElement.style.display = DisplayStyle.None;
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.isTimePaused = false;
            TimeManager.Instance.EndDay();
        }
    }

    public void ContinueNightShift()
    {
        dailyLedger.Clear(); // <-- Curăță tot instant!
        endDayReportPanel.rootVisualElement.style.display = DisplayStyle.None;
        if (TimeManager.Instance != null) TimeManager.Instance.isTimePaused = false;
    }
}