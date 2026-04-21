using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class InflationPanelUI : MonoBehaviour
{
    public static InflationPanelUI Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    [Header("Chart Settings")]
    [SerializeField] private int daysToShow = 10;
    [SerializeField] private float targetInflationPercent = 5f;
    [SerializeField] private float targetBandWidth = 1f;  // ±1% față de țintă
    [SerializeField] private bool showAsPercent = true;

    [Header("Product Impact")]
    [SerializeField] private ProductType impactProductType = ProductType.None;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private readonly List<float> _lastDays = new List<float>();
    private readonly List<int> _shockIndices = new List<int>(); // indecși șocuri

    // ── UI Elements ───────────────────────────────────────────────────────────
    private LineChart _chart;
    private Label _maxLabel;
    private Label _targetLabel;
    private Label _statusLabel;
    private VisualElement _dot;
    private Label _periodLabel;
    private Label _maxText;
    private Label _productImpactLabel; // NOU — impactul pe prețuri

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var root = uiDocument.rootVisualElement;

        _chart = root.Q<LineChart>("InflationChart");
        _maxLabel = root.Q<Label>("MaxInflationLabel");
        _targetLabel = root.Q<Label>("TargetInflationLabel");
        _statusLabel = root.Q<Label>("InflationStatusLabel");
        _dot = root.Q<VisualElement>("InflationLegendDot");
        _periodLabel = root.Q<Label>("InflationPeriodLabel");
        _maxText = root.Q<Label>("max");
        _productImpactLabel = root.Q<Label>("ProductImpactLabel"); // NOU

        if (_chart == null)
            Debug.LogError("[InflationPanelUI] InflationChart nu a fost găsit în UXML!");

        if (_targetLabel != null)
            _targetLabel.text = $"{targetInflationPercent:0.#}%";

        if (_periodLabel != null)
            _periodLabel.text = $"Ultimele {daysToShow} zile" +
                                $"({(showAsPercent ? "%" : "multiplicator")})";

        if (_maxText != null)
            _maxText.text = $"MAX({daysToShow} zile)";

        // Setăm banda de țintă pe chart
        if (_chart != null)
            _chart.SetTargetBand(
                targetInflationPercent - targetBandWidth,
                targetInflationPercent + targetBandWidth);
    }

    private void Start()
    {
        if (TimeManager.Instance == null) return;
        TimeManager.Instance.OnDayChanged += OnNewDay;

        if (InflationManager.Instance != null)
            InflationManager.Instance.OnShockApplied += OnShockApplied;

        RefreshFromCurrentInflation();
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= OnNewDay;

        if (InflationManager.Instance != null)
            InflationManager.Instance.OnShockApplied -= OnShockApplied;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnNewDay() => RefreshFromCurrentInflation();

    /// <summary>
    /// Apelat din InflationManager când apare un șoc — înregistrăm indexul zilei.
    /// </summary>
    private void OnShockApplied()
    {
        int shockIndex = _lastDays.Count; // indexul zilei curente (încă neadăugate)
        if (!_shockIndices.Contains(shockIndex))
            _shockIndices.Add(shockIndex);
    }

    // ── Core ──────────────────────────────────────────────────────────────────

    private void RefreshFromCurrentInflation()
    {
        var mgr = InflationManager.Instance;
        if (mgr == null) return;

        float inflation = mgr.CurrentInflation;
        AddDayValue(ConvertValue(inflation));
        UpdateUI();
    }

    private float ConvertValue(float inflationMultiplier)
    {
        return showAsPercent ? (inflationMultiplier - 1f) * 100f : inflationMultiplier;
    }

    private void AddDayValue(float value)
    {
        _lastDays.Add(value); // ← adăugăm întotdeauna, inclusiv 0%

        if (_lastDays.Count > daysToShow)
        {
            _lastDays.RemoveAt(0);
            for (int i = _shockIndices.Count - 1; i >= 0; i--)
            {
                _shockIndices[i]--;
                if (_shockIndices[i] < 0) _shockIndices.RemoveAt(i);
            }
        }
    }

    private void UpdateUI()
    {
        if (_chart == null) return;

        // ── Chart ─────────────────────────────────────────────────────────────
        _chart.ClearData();
        _chart.SetTargetBand(
            targetInflationPercent - targetBandWidth,
            targetInflationPercent + targetBandWidth);
        _chart.SetShockIndices(_shockIndices);
        _chart.AddSeries(_lastDays, Color.red, 3f);

        // ── Max label ─────────────────────────────────────────────────────────
        if (_lastDays.Count > 0 && _maxLabel != null)
        {
            float max = _lastDays.Max();
            _maxLabel.text = showAsPercent ? $"{max:0.#}%" : $"{max:0.###}";
        }

        // ── Status + dot ──────────────────────────────────────────────────────
        UpdateStatus();

        // ── Impact pe prețuri ─────────────────────────────────────────────────
        UpdateProductImpact();
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null || _dot == null || _lastDays.Count == 0) return;

        float latest = _lastDays[_lastDays.Count - 1];
        float latestPct = showAsPercent ? latest : (latest - 1f) * 100f;
        float diff = latestPct - targetInflationPercent;

        if (diff <= -targetBandWidth)
        {
            _statusLabel.text = "Stare: sub țintă";
            _dot.style.backgroundColor = new StyleColor(new Color(0.3f, 0.6f, 1f));
        }
        else if (diff <= targetBandWidth)
        {
            _statusLabel.text = "Stare: stabilă ✓";
            _dot.style.backgroundColor = new StyleColor(new Color(1f, 0.85f, 0.2f));
        }
        else if (diff < 5f)
        {
            _statusLabel.text = "Stare: în creștere ⚠";
            _dot.style.backgroundColor = new StyleColor(new Color(1f, 0.5f, 0.2f));
        }
        else
        {
            _statusLabel.text = "Stare: criză ⛔";
            _dot.style.backgroundColor = new StyleColor(new Color(0.9f, 0.2f, 0.2f));
        }
    }

    private void UpdateProductImpact()
    {
        if (_productImpactLabel == null) return;
        if (InflationManager.Instance == null) return;
        if (impactProductType == ProductType.None)
        {
            _productImpactLabel.style.display = DisplayStyle.None;
            return;
        }

        // Găsim prețul de bază al produsului selectat
        if (!ServiceLocator.Instance.TryGet(out IEconomyService economy)) return;
        if (!economy.TryGetProductData(impactProductType, out ProductEconomics data)) return;

        float inflatedPrice = InflationManager.Instance.GetPrice(data.CurrentBaseCost);
        _productImpactLabel.text =
            $"La inflația actuală, {impactProductType}: {inflatedPrice:F2} RON";
        _productImpactLabel.style.display = DisplayStyle.Flex;
    }
}