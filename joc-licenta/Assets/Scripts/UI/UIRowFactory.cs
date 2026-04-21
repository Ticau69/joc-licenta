using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Factory pentru rândurile din lista de inventar.
/// Folosește InventoryRowTemplate.uxml în loc să construiască din cod.
/// </summary>
public static class UIRowFactory
{
    private static VisualTreeAsset _rowTemplate;

    /// <summary>
    /// Asignează template-ul din InventoryUIController.
    /// Apelează o dată la Initialize().
    /// </summary>
    public static void SetRowTemplate(VisualTreeAsset template)
    {
        _rowTemplate = template;
    }

    /// <summary>
    /// Creează un rând de inventar complet cu toate datele populare.
    /// </summary>
    public static VisualElement CreateInventoryRow(
        ProductType  type,
        int          stockAmount,
        int          maxStock,
        float        profitPerUnit,
        string       status,
        Color        statusColor,
        System.Action onViewClicked)
    {
        // ── Instanțiem template-ul ────────────────────────────────────────────
        VisualElement row;

        if (_rowTemplate != null)
        {
            row = _rowTemplate.Instantiate();
        }
        else
        {
            // Fallback dacă template-ul nu e asignat
            Debug.LogWarning("[UIRowFactory] RowTemplate nu e asignat! Folosim fallback.");
            return CreateFallbackRow(type, stockAmount, status, statusColor, onViewClicked);
        }

        // ── Populăm elementele ────────────────────────────────────────────────

        // 1. Indicator bara verticală — culoarea din status
        var indicator = row.Q<VisualElement>("StockIndicatorBar");
        if (indicator != null)
            indicator.style.backgroundColor = new StyleColor(statusColor);

        // 2. Nume produs
        var nameLabel = row.Q<Label>("ProductNameLabel");
        if (nameLabel != null)
            nameLabel.text = type.ToString();

        // 3. Bara de progres stoc depozit
        var barFill = row.Q<VisualElement>("StockBarFill");
        if (barFill != null)
        {
            float fillPercent = maxStock > 0
                ? Mathf.Clamp01((float)stockAmount / maxStock) * 100f
                : 0f;

            barFill.style.width = Length.Percent(fillPercent);
            barFill.style.backgroundColor = new StyleColor(statusColor);
        }

        // 4. Cantitate stoc
        var stockLabel = row.Q<Label>("StockAmountLabel");
        if (stockLabel != null)
        {
            stockLabel.text       = stockAmount.ToString();
            stockLabel.style.color = new StyleColor(statusColor);
        }

        // 5. Profit per unitate
        var profitLabel = row.Q<Label>("ProfitLabel");
        if (profitLabel != null)
        {
            bool   isProfitable = profitPerUnit >= 0f;
            string sign        = isProfitable ? "+" : "";
            profitLabel.text         = $"{sign}{profitPerUnit:F1} RON";
            profitLabel.style.color  = new StyleColor(
                isProfitable ? new Color(0.3f, 0.85f, 0.3f)
                             : new Color(0.9f, 0.3f, 0.3f));
        }

        // 6. Buton VIEW
        var viewBtn = row.Q<Button>("ViewButton");
        if (viewBtn != null)
            viewBtn.clicked += onViewClicked;

        // Hover effect pe rând
        row.RegisterCallback<MouseEnterEvent>(_ =>
            row.Q<VisualElement>("InventoryRow").style.backgroundColor =
                new StyleColor(new Color(1f, 1f, 1f, 0.08f)));

        row.RegisterCallback<MouseLeaveEvent>(_ =>
            row.Q<VisualElement>("InventoryRow").style.backgroundColor =
                new StyleColor(new Color(1f, 1f, 1f, 0.04f)));

        return row;
    }

    // ── Fallback (cod vechi) ──────────────────────────────────────────────────

    private static VisualElement CreateFallbackRow(
        ProductType type, int amount, string status,
        Color color, System.Action onViewClicked)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection  = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems     = Align.Center;
        row.style.paddingTop     = 5;
        row.style.height         = 40;

        Label infoLabel = new Label($"{type} ({amount})");
        infoLabel.style.color    = color;
        infoLabel.style.fontSize = 14;
        row.Add(infoLabel);

        Button viewBtn  = new Button(onViewClicked);
        viewBtn.text    = "VIEW";
        viewBtn.style.width           = 60;
        viewBtn.style.height          = 25;
        viewBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        viewBtn.style.color           = Color.white;
        row.Add(viewBtn);

        return row;
    }
}