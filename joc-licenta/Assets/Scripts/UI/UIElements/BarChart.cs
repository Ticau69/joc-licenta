using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public struct DayData
{
    public string DateLabel;
    public float Income;
    public float Expense;
    public float NetProfit => Income - Expense;

    public DayData(string date, float inc, float exp)
    {
        DateLabel = date;
        Income = inc;
        Expense = exp;
    }
}

[UxmlElement]
public partial class BarChart : VisualElement
{
    private const int MAX_HISTORY_DAYS = 5;

    // ── Culori ───────────────────────────────────────────────────────────────
    private Color profitColor = new Color(0.28f, 0.69f, 0.3f);   // verde
    private Color lossColor = new Color(0.85f, 0.3f, 0.3f);    // roșu
    private Color expenseColor = new Color(0.85f, 0.3f, 0.3f);    // roșu
    private Color breakEvenColor = new Color(1f, 0.85f, 0.2f, 0.7f); // galben
    private Color avgLineColor = new Color(0.4f, 0.7f, 1f, 0.6f);  // albastru deschis
    private Color gridColor = new Color(1f, 1f, 1f, 0.07f);

    // ── Date ─────────────────────────────────────────────────────────────────
    private List<DayData> allDays = new List<DayData>();
    private float _animProgress = 1f;  // 0..1 pentru animație
    private bool _isAnimating = false;
    private long _lastSetDataMs = 0;

    // ── Tooltip ───────────────────────────────────────────────────────────────
    private VisualElement _tooltip;
    private Label _tooltipLabel;
    private int _hoveredSlot = -1;

    // ── Sub-elemente ──────────────────────────────────────────────────────────
    private VisualElement graphArea;
    private VisualElement xAxisContainer;

    // ── Constructor ───────────────────────────────────────────────────────────

    public BarChart()
    {
        // Zona de desen
        graphArea = new VisualElement();
        graphArea.style.flexGrow = 1;
        graphArea.generateVisualContent += OnGenerateVisualContent;
        Add(graphArea);

        // Axa X
        xAxisContainer = new VisualElement();
        xAxisContainer.style.flexDirection = FlexDirection.Row;
        xAxisContainer.style.height = 18;
        xAxisContainer.style.justifyContent = Justify.SpaceAround;
        Add(xAxisContainer);

        // Tooltip
        CreateTooltip();

        // Hover pe zona de desen
        graphArea.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        graphArea.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

        RegisterCallback<GeometryChangedEvent>(evt =>
        {
            if (evt.oldRect.size != evt.newRect.size)
                graphArea.MarkDirtyRepaint();
        });

        // Pre-populăm cu zile goale
        for (int i = 0; i < MAX_HISTORY_DAYS; i++)
            allDays.Add(new DayData($"Ziua {i + 1}", 0, 0));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetData(List<DayData> data)
    {
        allDays = new List<DayData>(data);

        // Completăm cu zile goale dacă sunt mai puține de MAX
        while (allDays.Count < MAX_HISTORY_DAYS)
            allDays.Insert(0, new DayData("", 0, 0));

        // Limităm la MAX
        if (allDays.Count > MAX_HISTORY_DAYS)
            allDays = allDays.GetRange(allDays.Count - MAX_HISTORY_DAYS, MAX_HISTORY_DAYS);

        RebuildXAxis();
        StartAnimation();
        graphArea.MarkDirtyRepaint();
    }

    // ── Animație ─────────────────────────────────────────────────────────────

    private void StartAnimation()
    {
        _animProgress = 0f;
        _isAnimating = true;
        schedule.Execute(AnimationTick).Every(16); // ~60fps
    }

    private void AnimationTick()
    {
        if (!_isAnimating) return;

        _animProgress += 0.05f;
        if (_animProgress >= 1f)
        {
            _animProgress = 1f;
            _isAnimating = false;
        }
        graphArea.MarkDirtyRepaint();
    }

    // ── Tooltip ───────────────────────────────────────────────────────────────

    private void CreateTooltip()
    {
        _tooltip = new VisualElement();
        _tooltip.style.position = Position.Absolute;
        _tooltip.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        _tooltip.style.borderTopLeftRadius = 6;
        _tooltip.style.borderTopRightRadius = 6;
        _tooltip.style.borderBottomLeftRadius = 6;
        _tooltip.style.borderBottomRightRadius = 6;
        _tooltip.style.paddingTop = 6;
        _tooltip.style.paddingBottom = 6;
        _tooltip.style.paddingLeft = 8;
        _tooltip.style.paddingRight = 8;
        _tooltip.style.display = DisplayStyle.None;

        _tooltipLabel = new Label();
        _tooltipLabel.style.color = Color.white;
        _tooltipLabel.style.fontSize = 11;
        _tooltip.Add(_tooltipLabel);

        Add(_tooltip);
    }

    private void OnMouseMove(MouseMoveEvent evt)
    {
        float w = graphArea.contentRect.width;
        float stepX = w / MAX_HISTORY_DAYS;

        // ── Detectăm slot-ul bazat pe X-ul mouse-ului în toată coloana ──
        int slot = Mathf.FloorToInt(evt.localMousePosition.x / stepX);
        slot = Mathf.Clamp(slot, 0, allDays.Count - 1);

        if (slot != _hoveredSlot)
        {
            _hoveredSlot = slot;
            graphArea.MarkDirtyRepaint();
        }

        DayData day = allDays[slot];

        // Nu afișăm tooltip pentru zilele goale (placeholder)
        if (string.IsNullOrEmpty(day.DateLabel) ||
            (day.Income == 0 && day.Expense == 0))
        {
            _tooltip.style.display = DisplayStyle.None;
            return;
        }

        float net = day.NetProfit;
        string netStr = net >= 0 ? $"+{net:F0}" : $"{net:F0}";

        _tooltipLabel.text =
            $"{day.DateLabel}\n" +
            $"Venituri:    {day.Income:F0} RON\n" +
            $"Cheltuieli:  {day.Expense:F0} RON\n" +
            $"Profit net:  {netStr} RON";

        _tooltipLabel.style.color = net >= 0
            ? new Color(0.3f, 0.9f, 0.3f)
            : new Color(0.9f, 0.3f, 0.3f);

        // ── Poziționare tooltip să nu iasă din grafic ──────────────────────
        float tx = evt.localMousePosition.x + 12f;
        float ty = evt.localMousePosition.y - 85f;

        ty = Mathf.Clamp(ty, 4f, graphArea.contentRect.height - 90f);
        if (tx + 150f > w) tx = evt.localMousePosition.x - 155f;

        _tooltip.style.left = tx;
        _tooltip.style.top = ty;
        _tooltip.style.display = DisplayStyle.Flex;
    }

    private void OnMouseLeave(MouseLeaveEvent evt)
    {
        _hoveredSlot = -1;
        _tooltip.style.display = DisplayStyle.None;
        graphArea.MarkDirtyRepaint();
    }

    // ── X Axis ────────────────────────────────────────────────────────────────

    private void RebuildXAxis()
    {
        xAxisContainer.Clear();
        foreach (var day in allDays)
        {
            var label = new Label(day.DateLabel);
            label.style.fontSize = 9;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            label.style.marginTop = 3;
            label.style.width = Length.Percent(100f / MAX_HISTORY_DAYS);
            xAxisContainer.Add(label);
        }
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (allDays == null || allDays.Count == 0) return;

        var painter = mgc.painter2D;
        float w = graphArea.contentRect.width;
        float h = graphArea.contentRect.height;

        float stepX = w / MAX_HISTORY_DAYS;
        float padding = stepX * 0.2f;
        float barGroupW = stepX - padding;
        float singleBarW = barGroupW / 2f;

        // ── Calcul maxim global ───────────────────────────────────────────────
        float maxVal = 1f;
        float avgExp = 0f;
        int validDays = 0;

        foreach (var day in allDays)
        {
            float absNet = Mathf.Abs(day.NetProfit);
            if (absNet > maxVal) maxVal = absNet;
            if (day.Expense > maxVal) maxVal = day.Expense;
            if (day.Income > 0 || day.Expense > 0)
            {
                avgExp += day.Expense;
                validDays++;
            }
        }
        maxVal *= 1.15f; // padding sus
        avgExp = validDays > 0 ? avgExp / validDays : 0f;

        // ── 1. Grilă orizontală cu etichete ──────────────────────────────────
        DrawGrid(painter, w, h, maxVal);

        // ── 2. Linie break-even (Y=0 pentru profit net) ───────────────────────
        float breakEvenY = h; // break-even e la baza graficului
        DrawHorizontalLine(painter, breakEvenY, w, breakEvenColor, 1f);

        // ── 3. Linie medie cheltuieli ─────────────────────────────────────────
        if (avgExp > 0f)
        {
            float avgY = h - (avgExp / maxVal) * h;
            DrawDashedLine(painter, avgY, w, avgLineColor);
        }

        // ── 4. Bare ──────────────────────────────────────────────────────────
        for (int i = 0; i < allDays.Count; i++)
        {
            DayData day = allDays[i];
            if (day.Income == 0 && day.Expense == 0) continue;

            float xBase = i * stepX + padding / 2f;
            float anim = _animProgress;
            bool hovered = i == _hoveredSlot;
            float alpha = hovered ? 1f : 0.85f;

            // ── Bara profit net ──────────────────────────────────────────────
            float net = day.NetProfit * anim;
            float hNet = Mathf.Abs(net / maxVal) * h;

            if (hNet > 1f)
            {
                Color netColor = net >= 0
                    ? new Color(profitColor.r, profitColor.g, profitColor.b, alpha)
                    : new Color(lossColor.r, lossColor.g, lossColor.b, alpha);

                painter.fillColor = netColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(xBase, h));
                painter.LineTo(new Vector2(xBase, h - hNet));
                painter.LineTo(new Vector2(xBase + singleBarW, h - hNet));
                painter.LineTo(new Vector2(xBase + singleBarW, h));
                painter.ClosePath();
                painter.Fill();
            }

            // ── Bara cheltuieli ──────────────────────────────────────────────
            float hExp = (day.Expense * anim / maxVal) * h;
            if (hExp > 1f)
            {
                Color expColor = new Color(expenseColor.r, expenseColor.g,
                                           expenseColor.b, alpha * 0.7f);
                painter.fillColor = expColor;
                painter.BeginPath();
                float xExp = xBase + singleBarW;
                painter.MoveTo(new Vector2(xExp, h));
                painter.LineTo(new Vector2(xExp, h - hExp));
                painter.LineTo(new Vector2(xExp + singleBarW, h - hExp));
                painter.LineTo(new Vector2(xExp + singleBarW, h));
                painter.ClosePath();
                painter.Fill();
            }

            // ── Highlight hover ──────────────────────────────────────────────
            if (hovered)
            {
                painter.fillColor = new Color(1f, 1f, 1f, 0.05f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(xBase - 2f, 0));
                painter.LineTo(new Vector2(xBase + barGroupW + 2f, 0));
                painter.LineTo(new Vector2(xBase + barGroupW + 2f, h));
                painter.LineTo(new Vector2(xBase - 2f, h));
                painter.ClosePath();
                painter.Fill();
            }

            // ── Trend indicator (față de ziua precedentă) ───────────────────
            if (i > 0 && day.Income > 0)
            {
                DayData prev = allDays[i - 1];
                float prevNet = prev.NetProfit;
                float currNet = day.NetProfit;

                if (prevNet != 0f)
                {
                    float pct = (currNet - prevNet) / Mathf.Abs(prevNet) * 100f;
                    // Trend-ul se desenează ca un triunghi mic deasupra barei
                    DrawTrendArrow(painter,
                        xBase + singleBarW / 2f,
                        h - hNet - 8f,
                        pct >= 0f);
                }
            }
        }
    }

    // ── Helper Draw ───────────────────────────────────────────────────────────

    private void DrawGrid(Painter2D p, float w, float h, float maxVal)
    {
        int steps = 4;
        p.lineWidth = 1f;
        p.strokeColor = gridColor;

        for (int i = 1; i <= steps; i++)
        {
            float y = h - (h / steps * i);
            float value = maxVal / steps * i;

            p.BeginPath();
            p.MoveTo(new Vector2(0, y));
            p.LineTo(new Vector2(w, y));
            p.Stroke();
        }
    }

    private void DrawHorizontalLine(Painter2D p, float y, float w, Color color, float width)
    {
        p.lineWidth = width;
        p.strokeColor = color;
        p.BeginPath();
        p.MoveTo(new Vector2(0, y));
        p.LineTo(new Vector2(w, y));
        p.Stroke();
    }

    private void DrawDashedLine(Painter2D p, float y, float w, Color color)
    {
        p.lineWidth = 1f;
        p.strokeColor = color;
        float dashLen = 6f;
        float gapLen = 4f;
        float x = 0f;

        while (x < w)
        {
            float end = Mathf.Min(x + dashLen, w);
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(end, y));
            p.Stroke();
            x += dashLen + gapLen;
        }
    }

    private void DrawTrendArrow(Painter2D p, float cx, float cy, bool up)
    {
        float size = 4f;
        p.fillColor = up
            ? new Color(0.3f, 0.9f, 0.3f, 0.9f)
            : new Color(0.9f, 0.3f, 0.3f, 0.9f);

        p.BeginPath();
        if (up)
        {
            p.MoveTo(new Vector2(cx, cy));
            p.LineTo(new Vector2(cx - size, cy + size * 1.5f));
            p.LineTo(new Vector2(cx + size, cy + size * 1.5f));
        }
        else
        {
            p.MoveTo(new Vector2(cx, cy + size * 1.5f));
            p.LineTo(new Vector2(cx - size, cy));
            p.LineTo(new Vector2(cx + size, cy));
        }
        p.ClosePath();
        p.Fill();
    }
}