using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class InflationPanelUI : MonoBehaviour
{
    public static InflationPanelUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    [Header("Chart Settings")]
    [SerializeField] private int daysToShow = 5;
    [SerializeField] private float targetInflationPercent = 5f; // ex: 5%
    [SerializeField] private bool showAsPercent = true;

    private readonly List<float> lastDays = new List<float>();

    private LineChart chart;
    private Label maxLabel;
    private Label targetLabel;
    private Label status;
    private VisualElement dot;
    private Label period;
    private Label maxText;

    private void Awake()
    {
        // Singleton sigur
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        var root = uiDocument.rootVisualElement;

        chart = root.Q<LineChart>("InflationChart");
        maxLabel = root.Q<Label>("MaxInflationLabel");
        targetLabel = root.Q<Label>("TargetInflationLabel");
        status = root.Q<Label>("InflationStatusLabel");
        dot = root.Q<VisualElement>("InflationLegendDot");

        period = root.Q<Label>("InflationPeriodLabel");
        maxText = root.Q<Label>("max");

        if (chart == null) Debug.LogError("InflationChart nu a fost găsit în UXML (verifică name).");
        if (maxLabel == null) Debug.LogError("MaxInflationLabel nu a fost găsit în UXML.");

        if (targetLabel != null)
            targetLabel.text = $"{targetInflationPercent:0.#}%";

        if (period != null)
            period.text = $"Ultimele {daysToShow} zile({(showAsPercent ? "(%)" : "(multiplicator)")})";
        if (maxText != null)
            maxText.text = $"MAX({daysToShow} zile)";
    }

    private void Start()
    {
        Debug.Log("InflationPanelUI Start subscribing...");

        if (TimeManager.Instance == null)
        {
            Debug.LogWarning("TimeManager.Instance is NULL in Start");
            return;
        }

        TimeManager.Instance.OnDayChanged += OnNewDay;
        Debug.Log("Subscribed OK");

        RefreshFromCurrentInflation();
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= OnNewDay;
    }

    private void OnNewDay()
    {
        // La EndDay(), tu chemi deja inflationManager.UpdateEconomyForNewDay();
        // Deci aici citim noua valoare și o adăugăm în istoric.
        RefreshFromCurrentInflation();
    }

    private void RefreshFromCurrentInflation()
    {
        // AICI e singura linie posibil de ajustat, în funcție de InflationManager-ul tău:
        // Exemplu 1: inflation = ServiceLocator.Instance.Get<InflationManager>().CurrentInflation;
        // Exemplu 2: inflation = InflationManager.Instance.CurrentInflation;

        var inflationManager = InflationManager.Instance;
        if (inflationManager == null)
            return;

        // Încearcă să folosești o proprietate numită CurrentInflation.
        // Dacă la tine are alt nume, schimbă această linie.
        float inflation = inflationManager.CurrentInflation;
        Debug.Log("Inflation val: " + inflation);
        AddDayValue(ConvertValue(inflation));
        UpdateUI();
    }

    private float ConvertValue(float inflationMultiplier)
    {
        if (!showAsPercent) return inflationMultiplier;

        // ex: 1.10 -> 10%
        return (inflationMultiplier - 1f) * 100f;
    }

    private void AddDayValue(float value)
    {
        lastDays.Add(value);
        if (lastDays.Count > daysToShow)
            lastDays.RemoveAt(0);
    }

    private void UpdateUI()
    {
        if (chart == null) return;
        Debug.Log("Updating chart with values: " + string.Join(", ", lastDays.Select(v => v.ToString("0.##"))));
        chart.ClearData();
        chart.AddSeries(lastDays, Color.red, 3f);

        if (lastDays.Count > 0 && maxLabel != null)
        {
            float max = lastDays.Max();
            maxLabel.text = showAsPercent ? $"{max:0.#}%" : $"{max:0.###}";
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (status == null || dot == null || lastDays.Count == 0) return;

        float latest = lastDays[lastDays.Count - 1];

        // Dacă nu afișezi în %, comparăm multiplicatorul cu targetInflationPercent transformat
        float latestPercent = showAsPercent ? latest : (latest - 1f) * 100f;

        // Praguri simple (le poți ajusta)
        float diff = latestPercent - targetInflationPercent;

        if (diff <= -1f)
        {
            status.text = "Stare: sub țintă";
            dot.style.backgroundColor = new StyleColor(new Color(0.3f, 0.8f, 0.3f)); // verde
        }
        else if (diff < 1f)
        {
            status.text = "Stare: stabilă";
            dot.style.backgroundColor = new StyleColor(new Color(1f, 0.85f, 0.2f)); // galben
        }
        else if (diff < 5f)
        {
            status.text = "Stare: în creștere";
            dot.style.backgroundColor = new StyleColor(new Color(1f, 0.5f, 0.2f)); // portocaliu
        }
        else
        {
            status.text = "Stare: criză";
            dot.style.backgroundColor = new StyleColor(new Color(0.9f, 0.2f, 0.2f)); // roșu
        }
    }
}
