using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the three sidebar step-indicator circles.
/// Plain C# class — no Unity dependencies beyond UIElements.
/// </summary>
public class SupplierStepIndicator
{
    private static readonly Color ColorComplete = new(0.3f, 0.7f, 0.3f);
    private static readonly Color ColorActive = new(0.2f, 0.6f, 1f);
    private static readonly Color ColorInactive = new(0.2f, 0.2f, 0.2f);
    private static readonly Color ColorTextOn = Color.white;
    private static readonly Color ColorTextOff = new(0.5f, 0.5f, 0.5f);

    private readonly VisualElement[] _indicators;

    public SupplierStepIndicator(
        VisualElement ind1,
        VisualElement ind2,
        VisualElement ind3)
    {
        _indicators = new[] { ind1, ind2, ind3 };
    }

    public void Refresh(int currentStep)
    {
        for (int i = 0; i < _indicators.Length; i++)
            UpdateIndicator(_indicators[i], i, currentStep);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void UpdateIndicator(VisualElement indicator, int stepIndex, int currentStep)
    {
        if (indicator == null) return;

        // First child = circle, second child = text label
        var circle = indicator.ElementAt(0);
        var label = indicator.ElementAt(1);

        bool isActive = currentStep == stepIndex;
        bool isComplete = currentStep > stepIndex;

        if (circle != null)
        {
            circle.style.backgroundColor = new StyleColor(
                isComplete ? ColorComplete :
                isActive ? ColorActive :
                             ColorInactive
            );
        }

        if (label != null)
        {
            label.style.color = new StyleColor(
                isActive || isComplete ? ColorTextOn : ColorTextOff
            );
        }
    }
}