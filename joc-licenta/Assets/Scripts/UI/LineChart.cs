using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

// Clasă simplă pentru a defini o linie
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

[UxmlElement]
public partial class LineChart : VisualElement
{
    // Lista care ține toate liniile pe care vrem să le desenăm
    private List<LineSeries> allSeries = new List<LineSeries>();

    public LineChart()
    {
        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (evt.oldRect.size != evt.newRect.size) MarkDirtyRepaint();
    }

    // --- METODE NOI PENTRU GESTIONAREA DATELOR ---

    // Șterge tot și pregătește pentru date noi
    public void ClearData()
    {
        allSeries.Clear();
        MarkDirtyRepaint();
    }

    // Adaugă o linie nouă pe grafic
    public void AddSeries(List<float> values, Color color, float width = 3f)
    {
        if (values == null || values.Count == 0) return;

        // Fix pentru 1 singur punct (duplicăm ca să se vadă linia)
        List<float> processedValues = new List<float>(values);
        if (processedValues.Count == 1) processedValues.Add(processedValues[0]);

        allSeries.Add(new LineSeries(processedValues, color, width));
        MarkDirtyRepaint();
    }

    // --- LOGICA DE DESENARE ---

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (allSeries.Count == 0) return;

        var painter = mgc.painter2D;
        painter.lineJoin = LineJoin.Round;
        painter.lineCap = LineCap.Round;

        DrawGrid(painter, contentRect.width, contentRect.height);

        // 1. Calculăm Min/Max GLOBAL (din toate seriile)
        // Asta asigură că liniile sunt proporționale între ele
        float globalMin = float.MaxValue;
        float globalMax = float.MinValue;

        foreach (var series in allSeries)
        {
            if (series.Values.Count > 0)
            {
                float localMin = series.Values.Min();
                float localMax = series.Values.Max();
                if (localMin < globalMin) globalMin = localMin;
                if (localMax > globalMax) globalMax = localMax;
            }
        }

        // Evităm împărțirea la 0
        if (globalMax - globalMin == 0) globalMax = globalMin + 1;

        float width = contentRect.width;
        float height = contentRect.height;

        // 2. Desenăm fiecare serie
        foreach (var series in allSeries)
        {
            DrawSingleLine(painter, series, globalMin, globalMax, width, height);
        }
    }

    private void DrawSingleLine(Painter2D painter, LineSeries series, float min, float max, float w, float h)
    {
        painter.strokeColor = series.LineColor;
        painter.lineWidth = series.LineWidth;
        painter.BeginPath();

        float stepX = w / (series.Values.Count - 1);

        for (int i = 0; i < series.Values.Count; i++)
        {
            // Normalizare folosind valorile GLOBALE
            float normalizedValue = (series.Values[i] - min) / (max - min);

            float x = i * stepX;
            float y = h - (normalizedValue * h); // Inversăm Y
            y = Mathf.Lerp(h * 0.95f, h * 0.05f, normalizedValue); // Padding

            Vector2 point = new Vector2(x, y);

            if (i == 0) painter.MoveTo(point);
            else painter.LineTo(point);
        }

        painter.Stroke();
    }

    private void DrawGrid(Painter2D painter, float width, float height)
    {
        float originalWidth = painter.lineWidth;
        painter.lineWidth = 1f;
        painter.strokeColor = new Color(1, 1, 1, 0.1f);
        painter.BeginPath();
        for (int i = 1; i < 5; i++)
        {
            float y = (height / 5) * i;
            painter.MoveTo(new Vector2(0, y));
            painter.LineTo(new Vector2(width, y));
        }
        painter.Stroke();
        painter.lineWidth = originalWidth;
    }
}