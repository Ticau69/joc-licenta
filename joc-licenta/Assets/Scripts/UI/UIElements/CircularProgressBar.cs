using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class CircularProgress : VisualElement
{
    private float _progress = 50f;
    private float _thickness = 10f;
    private Color _trackColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    private Color _progressColor = Color.green;

    // --- NOI VARIABILE PENTRU TEXT ---
    private string _centerText = "1";
    private Color _textColor = Color.white;
    private float _textSize = 24f;
    private Label _labelElement; // Referința către elementul de text intern

    // --- PROPRIETĂȚI EXISTENTE ---
    [UxmlAttribute]
    public float Progress
    {
        get => _progress;
        set { _progress = Mathf.Clamp(value, 0f, 100f); MarkDirtyRepaint(); }
    }

    [UxmlAttribute]
    public float Thickness
    {
        get => _thickness;
        set { _thickness = value; MarkDirtyRepaint(); }
    }

    [UxmlAttribute]
    public Color TrackColor
    {
        get => _trackColor;
        set { _trackColor = value; MarkDirtyRepaint(); }
    }

    [UxmlAttribute]
    public Color ProgressColor
    {
        get => _progressColor;
        set { _progressColor = value; MarkDirtyRepaint(); }
    }

    // --- NOI PROPRIETĂȚI PENTRU TEXT (Vor apărea în Inspector) ---
    [UxmlAttribute]
    public string CenterText
    {
        get => _centerText;
        set
        {
            _centerText = value;
            if (_labelElement != null) _labelElement.text = value;
        }
    }

    [UxmlAttribute]
    public Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            if (_labelElement != null) _labelElement.style.color = value;
        }
    }

    [UxmlAttribute]
    public float TextSize
    {
        get => _textSize;
        set
        {
            _textSize = value;
            if (_labelElement != null) _labelElement.style.fontSize = value;
        }
    }

    // Constructorul
    public CircularProgress()
    {
        // 1. Creăm textul automat când se creează elementul
        _labelElement = new Label(_centerText);

        // 2. Îl stilizăm să ocupe tot spațiul și să fie pe centru (Absolute Fill)
        _labelElement.style.position = Position.Absolute;
        _labelElement.style.top = 0;
        _labelElement.style.bottom = 0;
        _labelElement.style.left = 0;
        _labelElement.style.right = 0;
        _labelElement.style.unityTextAlign = TextAnchor.MiddleCenter; // Aliniere pe ambele axe
        _labelElement.style.unityFontStyleAndWeight = FontStyle.Bold; // Text îngroșat (opțional)

        // Aplicăm culorile și mărimea implicite
        _labelElement.style.color = _textColor;
        _labelElement.style.fontSize = _textSize;

        // 3. Adăugăm Label-ul ca un "copil" al acestui element
        Add(_labelElement);

        // 4. Ne abonăm la desenarea cercului
        generateVisualContent += OnGenerateVisualContent;
    }

    // Logica de desenare a cercului (Rămâne neschimbată)
    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        var painter = mgc.painter2D;
        if (painter == null) return;

        float width = contentRect.width;
        float height = contentRect.height;
        Vector2 center = new Vector2(width / 2f, height / 2f);

        float radius = (Mathf.Min(width, height) / 2f) - (Thickness / 2f);

        if (radius <= 0) return;

        painter.lineWidth = Thickness;
        painter.lineCap = LineCap.Round;
        painter.strokeColor = TrackColor;

        painter.BeginPath();
        painter.Arc(center, radius, 0, 360f);
        painter.Stroke();

        if (Progress > 0)
        {
            painter.strokeColor = ProgressColor;
            painter.BeginPath();

            float startAngle = -90f;
            float endAngle = startAngle + (Progress / 100f) * 360f;

            painter.Arc(center, radius, startAngle, endAngle);
            painter.Stroke();
        }
    }
}