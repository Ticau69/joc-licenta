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
