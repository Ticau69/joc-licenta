using UnityEngine.UIElements;

/// <summary>
/// Shared UIElements extension methods.
/// Used by both the Inventory and Supplier UI systems.
/// If this file already exists in your project, you can delete this one.
/// </summary>
public static class SupplierUIExtensions
{
    public static void SetDisplay(this VisualElement el, bool visible)
    {
        if (el != null)
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}