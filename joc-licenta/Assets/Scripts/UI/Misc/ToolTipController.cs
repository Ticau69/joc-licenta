using UnityEngine;
using UnityEngine.UIElements;

public class ToolTipController : MonoBehaviour
{
    [SerializeField] private UIDocument uIDocument;
    [SerializeField, Tooltip("Offset de la pointer")] Vector2 Offset = new(x: 20f, y: 50f);

    VisualElement root;
    VisualElement toolTip;
    Label toolTipText;

    void Awake()
    {
        root = uIDocument.rootVisualElement;
        toolTip = root.Q<VisualElement>(name: "ToolTipBox");
        toolTipText = toolTip.Q<Label>(name: "ToolTipText");
    }

    void OnEnable()
    {
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        root.RegisterCallback<PointerDownEvent>(OnPointerDown);
    }

    void OnDisable()
    {
        root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        root.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        root.UnregisterCallback<PointerDownEvent>(OnPointerDown);
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.target is not VisualElement hovered)
        {
            HideTooltip();
            return;
        }

        VisualElement current = hovered;

        while (current != null && string.IsNullOrEmpty(current.tooltip))
        {
            current = current.parent;
        }

        if (current == null)
        {
            HideTooltip();
            return;
        }

        ShowTooltip(current.tooltip, (Vector2)evt.position + Offset);
    }

    void OnPointerLeave(PointerLeaveEvent evt)
    {
        HideTooltip();
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        HideTooltip();
    }

    void ShowTooltip(string text, Vector2 position)
    {
        toolTipText.text = text;

        toolTip.style.left = position.x;
        toolTip.style.top = position.y;
        toolTip.style.display = DisplayStyle.Flex;
    }

    void HideTooltip()
    {
        toolTip.style.display = DisplayStyle.None;
    }
}
