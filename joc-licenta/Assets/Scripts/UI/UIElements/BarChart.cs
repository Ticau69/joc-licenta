using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

// 1. Modificăm structura să includă Data (String)
public struct DayData
{
    public string DateLabel; // Ex: "22 Mar"
    public float Income;
    public float Expense;

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
    private Color incomeColor = new Color(0.28f, 0.69f, 0.3f);
    private Color expenseColor = new Color(0.85f, 0.3f, 0.3f);

    private List<DayData> allDays = new List<DayData>();

    // Containerul pentru etichetele de jos
    private VisualElement xAxisContainer;
    private VisualElement graphArea; // Zona unde desenăm barele

    public BarChart()
    {
        // --- STRUCTURA NOUĂ ---
        // Împărțim graficul în 2: Zona de Desen (Sus) și Zona de Text (Jos)

        // 1. Zona de desen (ocupă tot spațiul disponibil)
        graphArea = new VisualElement();
        graphArea.style.flexGrow = 1;
        graphArea.generateVisualContent += OnGenerateVisualContent;
        Add(graphArea);

        // 2. Axa X (Bandă jos pentru date)
        xAxisContainer = new VisualElement();
        xAxisContainer.style.flexDirection = FlexDirection.Row; // Așezăm datele pe orizontală
        xAxisContainer.style.height = 20; // Înălțime fixă pentru text
        xAxisContainer.style.justifyContent = Justify.SpaceAround; // Distribuire egală
        Add(xAxisContainer);

        RegisterCallback<GeometryChangedEvent>(evt =>
        {
            if (evt.oldRect.size != evt.newRect.size)
            {
                graphArea.MarkDirtyRepaint();
            }
        });
    }

    public void SetData(List<DayData> data)
    {
        allDays = new List<DayData>(data);

        // --- GENERARE ETICHETE (LABELS) ---
        xAxisContainer.Clear(); // Ștergem etichetele vechi

        foreach (var day in allDays)
        {
            Label dateLabel = new Label(day.DateLabel);

            // Stilizare ca să arate bine și să se alinieze
            dateLabel.style.fontSize = 9;              // Font mic și fin
            dateLabel.style.color = new Color(0.7f, 0.7f, 0.7f); // Gri deschis (nu alb pur, e prea strident)
            dateLabel.style.unityTextAlign = TextAnchor.UpperCenter; // Centrat
            dateLabel.style.marginTop = 5;             // Puțin spațiu față de bare

            // TRUC: Forțăm lățimea etichetei să fie egală cu spațiul barei
            // Astfel textul va fi centrat perfect sub bara lui
            float percentWidth = 100f / allDays.Count;
            dateLabel.style.width = Length.Percent(percentWidth);

            xAxisContainer.Add(dateLabel);
        }

        graphArea.MarkDirtyRepaint(); // Redesenăm barele
    }

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (allDays == null || allDays.Count == 0) return;

        var painter = mgc.painter2D;
        float width = graphArea.contentRect.width;   // Folosim dimensiunile zonei de desen
        float height = graphArea.contentRect.height;

        float maxVal = 0;
        foreach (var day in allDays)
        {
            if (day.Income > maxVal) maxVal = day.Income;
            if (day.Expense > maxVal) maxVal = day.Expense;
        }
        if (maxVal == 0) maxVal = 100;

        float stepX = width / allDays.Count;
        float padding = stepX * 0.2f;
        float barGroupWidth = stepX - padding;
        float singleBarWidth = barGroupWidth / 2f;

        for (int i = 0; i < allDays.Count; i++)
        {
            // Centrarea barelor pe slotul lor pentru a se alinia cu textul
            // Textul e centrat pe slotul de lățime 'stepX'. 
            // Barele trebuie să fie și ele centrate în acel slot.
            float xBase = (i * stepX) + (padding / 2);

            // Bara Verde
            float hIncome = (allDays[i].Income / maxVal) * height;
            if (hIncome > 1)
            {
                painter.fillColor = incomeColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(xBase, height));
                painter.LineTo(new Vector2(xBase, height - hIncome));
                painter.LineTo(new Vector2(xBase + singleBarWidth, height - hIncome));
                painter.LineTo(new Vector2(xBase + singleBarWidth, height));
                painter.ClosePath();
                painter.Fill();
            }

            // Bara Roșie
            float hExpense = (allDays[i].Expense / maxVal) * height;
            if (hExpense > 1)
            {
                painter.fillColor = expenseColor;
                painter.BeginPath();
                float xRed = xBase + singleBarWidth;

                painter.MoveTo(new Vector2(xRed, height));
                painter.LineTo(new Vector2(xRed, height - hExpense));
                painter.LineTo(new Vector2(xRed + singleBarWidth, height - hExpense));
                painter.LineTo(new Vector2(xRed + singleBarWidth, height));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}