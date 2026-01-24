using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// Object Pool pentru VisualElements - elimină garbage collection constant
/// </summary>
public class UIElementPool
{
    private readonly Queue<VisualElement> _availableElements = new Queue<VisualElement>();
    private readonly HashSet<VisualElement> _activeElements = new HashSet<VisualElement>();
    private readonly int _maxPoolSize;
    private int _createdCount = 0;

    public UIElementPool(int maxSize = 50)
    {
        _maxPoolSize = maxSize;
    }

    public VisualElement Get()
    {
        VisualElement element;

        if (_availableElements.Count > 0)
        {
            element = _availableElements.Dequeue();
        }
        else
        {
            element = CreateNewElement();
            _createdCount++;
        }

        _activeElements.Add(element);
        element.style.display = DisplayStyle.Flex;

        return element;
    }

    public void Return(VisualElement element)
    {
        if (element == null) return;

        if (!_activeElements.Remove(element))
        {
            Debug.LogWarning("[UIElementPool] Attempted to return element not from this pool");
            return;
        }

        // Clean up the element
        element.Clear();
        element.style.display = DisplayStyle.None;
        ResetStyles(element);

        if (_availableElements.Count < _maxPoolSize)
        {
            _availableElements.Enqueue(element);
        }
    }

    public void ReturnAll(ScrollView container)
    {
        if (container == null) return;

        // Return all children to pool
        List<VisualElement> children = new List<VisualElement>(container.Children());
        foreach (var child in children)
        {
            Return(child);
        }

        container.Clear();
    }

    private VisualElement CreateNewElement()
    {
        VisualElement element = new VisualElement();
        return element;
    }

    private void ResetStyles(VisualElement element)
    {
        // Reset common style properties
        element.style.flexDirection = StyleKeyword.Null;
        element.style.justifyContent = StyleKeyword.Null;
        element.style.alignItems = StyleKeyword.Null;
        element.style.paddingTop = StyleKeyword.Null;
        element.style.borderBottomColor = StyleKeyword.Null;
        element.style.borderBottomWidth = StyleKeyword.Null;
        element.style.height = StyleKeyword.Null;
        element.style.width = StyleKeyword.Null;
        element.style.backgroundColor = StyleKeyword.Null;
        element.style.color = StyleKeyword.Null;
    }

    public void Clear()
    {
        _availableElements.Clear();
        _activeElements.Clear();
        _createdCount = 0;
    }

    public int ActiveCount => _activeElements.Count;
    public int AvailableCount => _availableElements.Count;
    public int TotalCreated => _createdCount;
}

/// <summary>
/// Factory pentru crearea de UI rows - centralizat și reusable
/// </summary>
public static class UIRowFactory
{
    public static VisualElement CreateInventoryRow(
        ProductType type,
        int amount,
        string status,
        Color color,
        System.Action onViewClicked)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 5;
        row.style.borderBottomColor = new Color(1, 1, 1, 0.1f);
        row.style.borderBottomWidth = 1;
        row.style.height = 40;

        Label infoLabel = new Label($"{type} ({amount})");
        infoLabel.style.color = color;
        infoLabel.style.fontSize = 14;
        row.Add(infoLabel);

        Button viewBtn = new Button(onViewClicked);
        viewBtn.text = "VIEW";
        viewBtn.style.width = 60;
        viewBtn.style.height = 25;
        viewBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        viewBtn.style.color = Color.white;

        row.Add(viewBtn);
        return row;
    }

    public static Label CreateInfoLabel(string text, Color color)
    {
        Label label = new Label(text);
        label.style.color = color;
        return label;
    }

    public static Button CreateStyledButton(string text, System.Action onClick)
    {
        Button button = new Button(onClick);
        button.text = text;
        button.style.marginTop = 5;
        button.style.marginBottom = 5;
        return button;
    }
}