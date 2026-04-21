using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class LineSeries
{
    public List<float> Values { get; private set; }
    public Color LineColor { get; private set; }
    public float LineWidth { get; private set; }

    public LineSeries(List<float> values, Color color, float width = 3f)
    {
        Values = new List<float>(values);
        LineColor = color;
        LineWidth = width;
    }
}

/// <summary>
/// LineChart îmbunătățit pentru panoul de inflație.
/// Suportă: zonă colorată sub linie, bandă țintă, predicție,
/// evenimente marcate, tooltip hover, valoare curentă prominentă.
/// </summary>
[UxmlElement]
public partial class LineChart : VisualElement
{
    // ── Date ─────────────────────────────────────────────────────────────────
    private List<LineSeries> allSeries = new List<LineSeries>();
    private float _targetMin = 4f;   // banda verde min
    private float _targetMax = 6f;   // banda verde max
    private List<int> _shockIndices = new List<int>(); // indecși șocuri

    // ── Animație ─────────────────────────────────────────────────────────────
    private float _animProgress = 1f;
    private bool _isAnimating = false;

    // ── Tooltip ───────────────────────────────────────────────────────────────
    private VisualElement _tooltip;
    private Label _tooltipLabel;
    private int _hoveredIndex = -1;

    // ── Valoare curentă prominentă ────────────────────────────────────────────
    private Label _currentValueLabel;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LineChart()
    {
        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        RegisterCallback<MouseMoveEvent>(OnMouseMove);
        RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

        CreateTooltip();
        CreateCurrentValueLabel();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ClearData()
    {
        allSeries.Clear();
        _shockIndices.Clear();
        MarkDirtyRepaint();
    }

    public void AddSeries(List<float> values, Color color, float width = 3f)
    {
        if (values == null || values.Count == 0)
        {
            if (_currentValueLabel != null)
            {
                _currentValueLabel.text = "-";
                _currentValueLabel.style.color = new StyleColor(Color.gray);
            }
            MarkDirtyRepaint();
            return;
        }

        List<float> processed = new List<float>(values);

        // ── Dacă primul punct e 0% și avem mai multe puncte,
        //    îl înlocuim cu primul punct non-zero ca linia să nu pornească din podea ──
        if (processed.Count >= 2 && Mathf.Approximately(processed[0], 0f))
            processed[0] = processed[1];

        allSeries.Add(new LineSeries(processed, color, width));

        if (_currentValueLabel != null && processed.Count > 0)
        {
            float current = processed[processed.Count - 1];
            _currentValueLabel.text = $"{current:F1}%";
            _currentValueLabel.style.color = current > _targetMax
                ? new Color(0.9f, 0.3f, 0.3f)
                : current < _targetMin
                    ? new Color(0.3f, 0.7f, 1f)
                    : new Color(0.3f, 0.85f, 0.3f);
        }

        StartAnimation();
        MarkDirtyRepaint();
    }

    /// <summary>Setează banda de țintă (zona verde acceptabilă).</summary>
    public void SetTargetBand(float min, float max)
    {
        _targetMin = min;
        _targetMax = max;
        MarkDirtyRepaint();
    }

    /// <summary>Marchează indecșii unde au apărut șocuri de inflație.</summary>
    public void SetShockIndices(List<int> indices)
    {
        _shockIndices = new List<int>(indices);
        MarkDirtyRepaint();
    }

    // ── Animație ─────────────────────────────────────────────────────────────

    private void StartAnimation()
    {
        _animProgress = 0f;
        _isAnimating = true;
        schedule.Execute(AnimTick).Every(16);
    }

    private void AnimTick()
    {
        if (!_isAnimating) return;
        _animProgress += 0.04f;
        if (_animProgress >= 1f) { _animProgress = 1f; _isAnimating = false; }
        MarkDirtyRepaint();
    }

    // ── Tooltip ───────────────────────────────────────────────────────────────

    private void CreateTooltip()
    {
        _tooltip = new VisualElement();
        _tooltip.style.position = Position.Absolute;
        _tooltip.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        _tooltip.style.borderTopLeftRadius = 6;
        _tooltip.style.borderTopRightRadius = 6;
        _tooltip.style.borderBottomLeftRadius = 6;
        _tooltip.style.borderBottomRightRadius = 6;
        _tooltip.style.paddingTop = 6;
        _tooltip.style.paddingBottom = 6;
        _tooltip.style.paddingLeft = 10;
        _tooltip.style.paddingRight = 10;
        _tooltip.style.display = DisplayStyle.None;
        _tooltip.pickingMode = PickingMode.Ignore;

        _tooltipLabel = new Label();
        _tooltipLabel.style.color = Color.white;
        _tooltipLabel.style.fontSize = 11;
        _tooltipLabel.style.whiteSpace = WhiteSpace.Normal;
        _tooltip.Add(_tooltipLabel);
        Add(_tooltip);
    }

    private void CreateCurrentValueLabel()
    {
        _currentValueLabel = new Label("0%");
        _currentValueLabel.style.position = Position.Absolute;
        _currentValueLabel.style.right = 8;
        _currentValueLabel.style.top = 6;
        _currentValueLabel.style.fontSize = 22;
        _currentValueLabel.style.color = Color.white;
        _currentValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _currentValueLabel.pickingMode = PickingMode.Ignore;
        Add(_currentValueLabel);
    }

    // ── Mouse events ──────────────────────────────────────────────────────────

    private void OnMouseMove(MouseMoveEvent evt)
    {
        if (allSeries.Count == 0) return;

        List<float> vals = allSeries[0].Values;
        if (vals.Count < 2) return;

        float w = contentRect.width;
        float stepX = w / (vals.Count - 1);
        int idx = Mathf.RoundToInt(evt.localMousePosition.x / stepX);
        idx = Mathf.Clamp(idx, 0, vals.Count - 1);

        if (idx != _hoveredIndex)
        {
            _hoveredIndex = idx;
            MarkDirtyRepaint();
        }

        float val = vals[idx];
        bool isShock = _shockIndices.Contains(idx);
        string shockStr = isShock ? "\n⚡ Șoc de inflație!" : "";

        string status = val > _targetMax ? "Peste țintă ⚠" :
                        val < _targetMin ? "Sub țintă" : "În zona țintă ✓";

        _tooltipLabel.text =
            $"Ziua {idx + 1}\n" +
            $"Inflație: {val:F2}%\n" +
            $"Stare: {status}" +
            shockStr;

        _tooltipLabel.style.color = val > _targetMax
            ? new Color(0.9f, 0.4f, 0.4f)
            : val < _targetMin
                ? new Color(0.4f, 0.7f, 1f)
                : new Color(0.4f, 0.9f, 0.4f);

        float tx = evt.localMousePosition.x + 12f;
        float ty = evt.localMousePosition.y - 75f;
        ty = Mathf.Clamp(ty, 4f, contentRect.height - 90f);
        if (tx + 160f > w) tx = evt.localMousePosition.x - 165f;

        _tooltip.style.left = tx;
        _tooltip.style.top = ty;
        _tooltip.style.display = DisplayStyle.Flex;
    }

    private void OnMouseLeave(MouseLeaveEvent evt)
    {
        _hoveredIndex = -1;
        _tooltip.style.display = DisplayStyle.None;
        MarkDirtyRepaint();
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (evt.oldRect.size != evt.newRect.size) MarkDirtyRepaint();
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (allSeries.Count == 0 || allSeries[0].Values.Count == 0)
        {
            // Placeholder — nicio dată încă
            DrawPlaceholder(mgc.painter2D,
                contentRect.width,
                contentRect.height);
            return;
        }

        var painter = mgc.painter2D;
        float w = contentRect.width;
        float h = contentRect.height;

        List<float> vals = allSeries[0].Values;
        if (vals.Count < 2) return;

        // ── Calcul min/max cu padding ─────────────────────────────────────────
        float dataMin = vals.Min();
        float dataMax = vals.Max();

        // Includem banda de țintă în calcul
        float globalMin = Mathf.Min(dataMin, _targetMin);
        globalMin = globalMin >= 0
            ? globalMin * 0.85f
            : globalMin * 1.15f;
        float globalMax = Mathf.Max(dataMax, _targetMax) * 1.25f;
        if (globalMax - globalMin < 1f) globalMax = globalMin + 3f;

        // ── 1. Grilă ─────────────────────────────────────────────────────────
        DrawGrid(painter, w, h, globalMin, globalMax);

        // ── 2. Bandă țintă (zona verde) ───────────────────────────────────────
        DrawTargetBand(painter, w, h, globalMin, globalMax);

        // ── 3. Zonă colorată sub linie ────────────────────────────────────────
        DrawFilledArea(painter, vals, w, h, globalMin, globalMax);

        // ── 4. Linia principală ───────────────────────────────────────────────
        DrawMainLine(painter, vals, w, h, globalMin, globalMax);

        // ── 5. Predicție (linie punctată) ────────────────────────────────────
        DrawPrediction(painter, vals, w, h, globalMin, globalMax);

        // ── 6. Puncte hover + șocuri ─────────────────────────────────────────
        DrawPoints(painter, vals, w, h, globalMin, globalMax);
    }

    private void DrawPlaceholder(Painter2D p, float w, float h)
    {
        p.lineWidth = 1f;
        p.strokeColor = new Color(1f, 1f, 1f, 0.06f);

        // Grilă simplă
        for (int i = 1; i <= 4; i++)
        {
            float y = h / 4f * i;
            p.BeginPath();
            p.MoveTo(new Vector2(0, y));
            p.LineTo(new Vector2(w, y));
            p.Stroke();
        }
    }

    // ── Draw helpers ──────────────────────────────────────────────────────────

    private void DrawGrid(Painter2D p, float w, float h, float min, float max)
    {
        p.lineWidth = 1f;
        p.strokeColor = new Color(1f, 1f, 1f, 0.06f);

        for (int i = 1; i <= 5; i++)
        {
            float y = h - (h / 5f * i);
            p.BeginPath();
            p.MoveTo(new Vector2(0, y));
            p.LineTo(new Vector2(w, y));
            p.Stroke();
        }
    }

    private void DrawTargetBand(Painter2D p, float w, float h, float min, float max)
    {
        float yMin = ValueToY(_targetMin, h, min, max);
        float yMax = ValueToY(_targetMax, h, min, max);

        // Banda verde semi-transparentă
        p.fillColor = new Color(0.3f, 0.85f, 0.3f, 0.08f);
        p.BeginPath();
        p.MoveTo(new Vector2(0, yMin));
        p.LineTo(new Vector2(w, yMin));
        p.LineTo(new Vector2(w, yMax));
        p.LineTo(new Vector2(0, yMax));
        p.ClosePath();
        p.Fill();

        // Margini banda
        p.lineWidth = 1f;
        p.strokeColor = new Color(0.3f, 0.85f, 0.3f, 0.25f);
        p.BeginPath();
        p.MoveTo(new Vector2(0, yMin));
        p.LineTo(new Vector2(w, yMin));
        p.Stroke();

        p.BeginPath();
        p.MoveTo(new Vector2(0, yMax));
        p.LineTo(new Vector2(w, yMax));
        p.Stroke();
    }

    private void DrawFilledArea(Painter2D p, List<float> vals,
       float w, float h, float min, float max)
    {
        if (vals.Count < 2) return;

        int drawCount = Mathf.CeilToInt(vals.Count * _animProgress);
        drawCount = Mathf.Clamp(drawCount, 2, vals.Count);
        float stepX = w / (vals.Count - 1);

        p.BeginPath();
        p.MoveTo(new Vector2(0, h));

        for (int i = 0; i < drawCount; i++)
        {
            float x = i * stepX;
            // ── Clampăm Y să nu iasă din grafic ──────────────────────────────
            float y = Mathf.Clamp(ValueToY(vals[i], h, min, max), 0f, h);
            p.LineTo(new Vector2(x, y));
        }

        float lastX = (drawCount - 1) * stepX;
        p.LineTo(new Vector2(lastX, h));
        p.ClosePath();

        float lastVal = vals[drawCount - 1];
        Color areaColor = lastVal > _targetMax
            ? new Color(0.9f, 0.3f, 0.3f, 0.12f)
            : lastVal < _targetMin
                ? new Color(0.3f, 0.6f, 1f, 0.12f)
                : new Color(0.3f, 0.85f, 0.3f, 0.12f);

        p.fillColor = areaColor;
        p.Fill();
    }

    private void DrawMainLine(Painter2D p, List<float> vals,
        float w, float h, float min, float max)
    {
        if (vals.Count == 0) return;

        if (vals.Count == 1)
        {
            float cx = w / 2f;
            float cy = ValueToY(vals[0], h, min, max);
            p.fillColor = allSeries[0].LineColor;
            DrawCircle(p, cx, cy, 5f);
            return;
        }

        int drawCount = Mathf.CeilToInt(vals.Count * _animProgress);
        drawCount = Mathf.Clamp(drawCount, 2, vals.Count);
        float stepX = w / (vals.Count - 1);

        p.lineJoin = LineJoin.Round;
        p.lineCap = LineCap.Round;
        p.lineWidth = allSeries[0].LineWidth;
        p.strokeColor = allSeries[0].LineColor;

        p.BeginPath();
        for (int i = 0; i < drawCount; i++)
        {
            float x = i * stepX;
            float y = Mathf.Clamp(ValueToY(vals[i], h, min, max), 0f, h);
            if (i == 0) p.MoveTo(new Vector2(x, y));
            else p.LineTo(new Vector2(x, y));
        }
        p.Stroke();
    }

    private void DrawPrediction(Painter2D p, List<float> vals,
        float w, float h, float min, float max)
    {
        if (vals.Count < 3 || _animProgress < 1f) return;

        int n = vals.Count;
        float avg = (vals[n - 1] - vals[n - 3]) / 2f;
        float pred1 = Mathf.Clamp(vals[n - 1] + avg, min, max); // ← clamp valoare
        float pred2 = Mathf.Clamp(vals[n - 1] + avg * 2f, min, max); // ← clamp valoare

        float stepX = w / (vals.Count - 1);
        float lastX = (n - 1) * stepX;
        float lastY = ValueToY(vals[n - 1], h, min, max);
        float pred1Y = ValueToY(pred1, h, min, max);
        float pred2Y = ValueToY(pred2, h, min, max);

        // ── Fix 2: X-ul predicției nu depășește lățimea panoului ─────────────
        float px1 = Mathf.Min(lastX + stepX, w - 2f);
        float px2 = Mathf.Min(lastX + stepX * 2f, w - 2f);

        // Dacă ambele puncte ajung la marginea panoului, nu mai desenăm
        if (px1 >= w - 2f && px2 >= w - 2f) return;

        p.lineWidth = 1.5f;
        p.strokeColor = new Color(1f, 1f, 1f, 0.3f);

        if (px1 < w - 2f)
            DrawDashedSegment(p, lastX, lastY, px1, pred1Y);

        if (px2 < w - 2f && px1 < w - 2f)
            DrawDashedSegment(p, px1, pred1Y, px2, pred2Y);
    }

    private void DrawDashedSegment(Painter2D p,
        float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        float dash = 5f;
        float gap = 4f;
        float t = 0f;

        while (t < len)
        {
            float t0 = t / len;
            float t1 = Mathf.Min((t + dash) / len, 1f);
            p.BeginPath();
            p.MoveTo(new Vector2(x1 + dx * t0, y1 + dy * t0));
            p.LineTo(new Vector2(x1 + dx * t1, y1 + dy * t1));
            p.Stroke();
            t += dash + gap;
        }
    }

    private void DrawPoints(Painter2D p, List<float> vals,
        float w, float h, float min, float max)
    {
        if (vals.Count < 2) return;

        float stepX = w / (vals.Count - 1);

        for (int i = 0; i < vals.Count; i++)
        {
            float x = i * stepX;
            float y = ValueToY(vals[i], h, min, max);

            bool isShock = _shockIndices.Contains(i);
            bool isHovered = i == _hoveredIndex;

            if (isShock)
            {
                // Icon șoc — cerc galben mai mare
                p.fillColor = new Color(1f, 0.8f, 0.1f, 0.9f);
                DrawCircle(p, x, y, 6f);

                // Linie verticală la șoc
                p.lineWidth = 1f;
                p.strokeColor = new Color(1f, 0.8f, 0.1f, 0.3f);
                p.BeginPath();
                p.MoveTo(new Vector2(x, y));
                p.LineTo(new Vector2(x, h));
                p.Stroke();
            }
            else if (isHovered)
            {
                // Punct hover — alb cu ring
                p.fillColor = Color.white;
                DrawCircle(p, x, y, 5f);

                p.lineWidth = 1.5f;
                p.strokeColor = new Color(1f, 1f, 1f, 0.4f);
                p.BeginPath();
                p.Arc(new Vector2(x, y), 8f, 0f, 360f);
                p.Stroke();
            }
            else if (i == vals.Count - 1)
            {
                // Ultimul punct — evidențiat
                Color lastColor = vals[i] > _targetMax
                    ? new Color(0.9f, 0.3f, 0.3f)
                    : new Color(0.3f, 0.85f, 0.3f);
                p.fillColor = lastColor;
                DrawCircle(p, x, y, 5f);
            }
        }
    }

    private void DrawCircle(Painter2D p, float cx, float cy, float r)
    {
        p.BeginPath();
        p.Arc(new Vector2(cx, cy), r, 0f, 360f);
        p.Fill();
    }

    private float ValueToY(float value, float h, float min, float max)
    {
        if (Mathf.Approximately(max, min)) return h * 0.5f;
        return h - ((value - min) / (max - min)) * h * 0.9f - h * 0.05f;
    }
}