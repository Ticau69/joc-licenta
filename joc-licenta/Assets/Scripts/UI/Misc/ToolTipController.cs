using UnityEngine;
using UnityEngine.UIElements;

public class ToolTipController : MonoBehaviour
{
    [SerializeField] private UIDocument uIDocument;
    [SerializeField, Tooltip("Offset de la pointer")] Vector2 Offset = new(x: 20f, y: 50f);

    VisualElement root;
    VisualElement toolTip;
    Label toolTipText;

    private bool isPlacementMode = false;

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

    void ShowTooltip(string text, Vector2 screenPosition)
    {
        toolTipText.text = text;

        // Screen space: (0,0) = bottom-left
        // UI Toolkit panel space: (0,0) = top-left → inversăm Y
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
            root.panel,
            new Vector2(screenPosition.x, Screen.height - screenPosition.y)
        );

        // Offset ca să nu fie sub cursor
        panelPos += Offset;

        // Clamp ca să nu iasă din ecran
        float tooltipWidth = toolTip.resolvedStyle.width;
        float tooltipHeight = toolTip.resolvedStyle.height;
        float panelWidth = root.resolvedStyle.width;
        float panelHeight = root.resolvedStyle.height;

        panelPos.x = Mathf.Clamp(panelPos.x, 0, panelWidth - tooltipWidth);
        panelPos.y = Mathf.Clamp(panelPos.y, 0, panelHeight - tooltipHeight);

        toolTip.style.left = panelPos.x;
        toolTip.style.top = panelPos.y;
        toolTip.style.display = DisplayStyle.Flex;
    }

    public void ShowPlacementInfo(string text, Vector2 screenPosition)
    {
        isPlacementMode = true;
        ShowTooltip(text, screenPosition); // fără Offset adăugat aici, e adăugat în ShowTooltip
    }

    public void HidePlacementInfo()
    {
        isPlacementMode = false;
        HideTooltip();
    }


    void OnPointerMove(PointerMoveEvent evt)
    {
        if (isPlacementMode) return;

        if (evt.target is not VisualElement hovered) { HideTooltip(); return; }

        VisualElement current = hovered;
        while (current != null && string.IsNullOrEmpty(current.tooltip))
            current = current.parent;

        if (current == null) { HideTooltip(); return; }

        ShowTooltipAtPanelPos(current.tooltip, evt.position); // panel space direct
    }

    void OnPointerLeave(PointerLeaveEvent evt)
    {
        HideTooltip();
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        HideTooltip();
    }

    private void ShowTooltipAtPanelPos(string text, Vector2 panelPosition)
    {
        toolTipText.text = text;
        toolTip.style.left = panelPosition.x + Offset.x;
        toolTip.style.top = panelPosition.y + Offset.y;
        toolTip.style.display = DisplayStyle.Flex;
    }

    void HideTooltip()
    {
        toolTip.style.display = DisplayStyle.None;
    }
}
