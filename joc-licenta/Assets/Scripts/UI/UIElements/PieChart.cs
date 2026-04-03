using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Structură pentru a ține datele fiecărei felii
public struct ChartSlice
{
    public string Name;
    public float Value;
    public Color SliceColor;
}

[UxmlElement]
public partial class FinancePieChart : VisualElement
{
    private List<ChartSlice> _slices = new List<ChartSlice>();
    private float _thickness = 25f;
    private float _lineLength = 30f; // Cât de mult iese linia în afara cercului

    // Proprietate expusă în UI Builder pentru grosimea cercului
    [UxmlAttribute]
    public float Thickness
    {
        get => _thickness;
        set { _thickness = value; MarkDirtyRepaint(); }
    }

    public FinancePieChart()
    {
        // Ne asigurăm că elementul are o dimensiune minimă în UI
        style.minWidth = 300;
        style.minHeight = 300;

        generateVisualContent += OnGenerateVisualContent;
    }

    // Funcția pe care o vei apela din scriptul tău de UI la finalul zilei
    public void SetData(List<ChartSlice> newSlices)
    {
        _slices = newSlices;

        // Ștergem etichetele (Label-urile) vechi dacă există
        Clear();

        MarkDirtyRepaint(); // Forțăm redesenarea cercului
    }

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (_slices == null || _slices.Count == 0) return;

        var painter = mgc.painter2D;
        float width = contentRect.width;
        float height = contentRect.height;
        Vector2 center = new Vector2(width / 2f, height / 2f);

        // 1. MĂRIM MARGINEA (Padding): Scădem 90f în loc de 50f ca să facem loc textelor lungi pe laterale
        float textPadding = 90f;
        float radius = (Mathf.Min(width, height) / 2f) - (Thickness / 2f) - _lineLength - textPadding;

        if (radius <= 0) return;

        float totalValue = 0f;
        foreach (var slice in _slices) totalValue += slice.Value;
        if (totalValue <= 0) return;

        float currentAngle = -90f;

        // Memorie pentru sistemul de anti-coliziune
        float lastRightY = -1000f;
        float lastLeftY = 10000f;
        float minSpacing = 35f;

        foreach (var slice in _slices)
        {
            if (slice.Value <= 0) continue;

            float sweepAngle = (slice.Value / totalValue) * 360f;
            float endAngle = currentAngle + sweepAngle;

            // Desenăm felia
            painter.lineWidth = Thickness;
            painter.lineCap = LineCap.Butt;
            painter.strokeColor = slice.SliceColor;

            painter.BeginPath();
            painter.Arc(center, radius, currentAngle, endAngle);
            painter.Stroke();

            // Calculăm linia de ghidare
            float midAngle = currentAngle + (sweepAngle / 2f);
            float midRad = midAngle * Mathf.Deg2Rad;

            Vector2 edgePoint = center + new Vector2(Mathf.Cos(midRad), Mathf.Sin(midRad)) * (radius + (Thickness / 2f));
            Vector2 outerPoint = center + new Vector2(Mathf.Cos(midRad), Mathf.Sin(midRad)) * (radius + (Thickness / 2f) + _lineLength);

            float directionX = Mathf.Cos(midRad) >= 0 ? 1f : -1f;
            Vector2 textAnchorPoint = outerPoint + new Vector2(10f * directionX, 0); // Am redus distanța brațului orizontal de la 20 la 10

            // SISTEM ANTI-COLIZIUNE + CLAMP
            float targetY = textAnchorPoint.y - 15f;

            if (directionX > 0) // Partea Dreaptă
            {
                if (targetY < lastRightY + minSpacing)
                    targetY = lastRightY + minSpacing;

                lastRightY = targetY;
            }
            else // Partea Stângă
            {
                if (targetY > lastLeftY - minSpacing)
                    targetY = lastLeftY - minSpacing;

                lastLeftY = targetY;
            }

            // 2. CLAMP: Nu lăsăm textul să iasă din ecran sus (10f) sau jos (height - 30f)
            targetY = Mathf.Clamp(targetY, 10f, height - 35f);

            // Desenăm linia
            painter.lineWidth = 2f;
            painter.strokeColor = Color.white;
            painter.BeginPath();
            painter.MoveTo(edgePoint);
            painter.LineTo(outerPoint);
            painter.LineTo(new Vector2(textAnchorPoint.x, targetY + 15f));
            painter.Stroke();

            // Adăugăm sau mutăm textul
            string labelName = $"Label_{slice.Name}";
            Label existingLabel = this.Q<Label>(labelName);

            if (existingLabel == null)
            {
                Label sliceLabel = new Label($"{slice.Name}\n{slice.Value} RON");
                sliceLabel.name = labelName;
                sliceLabel.style.position = Position.Absolute;
                sliceLabel.style.color = slice.SliceColor;

                // 3. FONT MAI MIC: Am redus de la 14f la 12f ca să încapă sigur
                sliceLabel.style.fontSize = 12f;
                sliceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                if (directionX > 0)
                {
                    sliceLabel.style.left = textAnchorPoint.x + 5f;
                    sliceLabel.style.top = targetY;
                }
                else
                {
                    sliceLabel.style.right = width - textAnchorPoint.x + 5f;
                    sliceLabel.style.top = targetY;
                    sliceLabel.style.unityTextAlign = TextAnchor.UpperRight;
                }

                Add(sliceLabel);
            }
            else
            {
                if (directionX > 0) existingLabel.style.top = targetY;
                else existingLabel.style.top = targetY;
            }

            currentAngle += sweepAngle;
        }
    }
}
