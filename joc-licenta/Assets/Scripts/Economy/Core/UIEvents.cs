public struct CloseAllUIEvent { }
public struct PlotUnlockedEvent { }

public struct ToggleExpansionModeEvent
{
    public bool IsActive;
    public ToggleExpansionModeEvent(bool isActive) { IsActive = isActive; }
}

public struct ProductPricedTooHighEvent
{
    public ProductType Product;
    public ProductPricedTooHighEvent(ProductType product) { Product = product; }
}