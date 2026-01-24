using System;
using System.Collections.Generic;

/// <summary>
/// Contract pentru serviciul de economie
/// </summary>
public interface IEconomyService
{
    IReadOnlyDictionary<ProductType, ProductEconomics> MarketData { get; }
    bool TryGetProductData(ProductType type, out ProductEconomics data);
    void UpdateSellingPrice(ProductType type, float newPrice);
    float GetSellingPrice(ProductType type);
    float GetBaseCost(ProductType type);
    float GetProfit(ProductType type);
    bool IsProductValid(ProductType type);
    event Action<ProductType, float> OnPriceChanged;
}

/// <summary>
/// Contract pentru serviciul de bani
/// </summary>
public interface IMoneyService
{
    int CurrentAmount { get; }
    bool TrySpend(int amount);
    void Add(int amount);
    bool CanAfford(int amount);
    event Action<int, int> OnMoneyChanged; // (oldAmount, newAmount)
}

/// <summary>
/// Contract pentru serviciul de shop
/// </summary>
public interface IShopService
{
    bool CanAfford(ProductType type, int quantity);
    void BuySupply(ProductType type, int quantity, WorkStation target, Action<bool> callback);
    void BuyDefaultSupply(ProductType type, WorkStation target, Action<bool> callback);
    bool CanAffordDefaultSupply(ProductType type);
    int GetPurchaseCost(ProductType type, int quantity);
}

/// <summary>
/// Contract pentru serviciul de inventory
/// </summary>
public interface IInventoryService
{
    WorkStation MainStorage { get; }
    int GetStock(ProductType type);
    bool HasStock(ProductType type, int minimumAmount = 1);
    event Action<ProductType, int> OnStockChanged;
}

/// <summary>
/// Contract pentru object registry - elimină FindObjects
/// </summary>
public interface IObjectRegistry
{
    void Register<T>(T obj) where T : class;
    void Unregister<T>(T obj) where T : class;
    T Get<T>() where T : class;
    IEnumerable<T> GetAll<T>() where T : class;
    bool TryGet<T>(out T result) where T : class;
}

/// <summary>
/// Contract pentru event bus - decoupling complet
/// </summary>
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : struct;
    void Unsubscribe<T>(Action<T> handler) where T : struct;
    void Publish<T>(T eventData) where T : struct;
}

/// <summary>
/// Events pentru sistemul de joc
/// </summary>
public struct MoneyChangedEvent
{
    public int OldAmount;
    public int NewAmount;
    public int Delta;
}

public struct ProductPriceChangedEvent
{
    public ProductType Product;
    public float OldPrice;
    public float NewPrice;
}

public struct StockChangedEvent
{
    public ProductType Product;
    public int OldStock;
    public int NewStock;
    public StationType Location;
}

public struct ShelfSelectedEvent
{
    public WorkStation Shelf;
}

public struct SupplyPurchasedEvent
{
    public ProductType Product;
    public int Quantity;
    public int Cost;
    public bool Success;
}