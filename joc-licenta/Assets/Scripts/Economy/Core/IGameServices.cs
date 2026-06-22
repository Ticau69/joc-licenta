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
    void SetMoney(int amount);
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
    int GetTotalCapacity();
    int GetUsedCapacity();
    int GetAvailableCapacity();
    int GetStock(ProductType type);
    bool HasStock(ProductType type, int minimumAmount = 1);
    void AddStock(ProductType type, int amount);           // ← NOU
    bool TryRemoveStock(ProductType type, int amount);     // ← NOU
    void ForceRefreshCache();                              // ← NOU
    StorageRacks FindRackWithProduct(ProductType type);
    StorageRacks FindRackWithSpace(ProductType type);
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

public struct DayEndedEvent
{
    public int DayNumber;
    public float TotalRevenue;
    public float FixedCosts;
    public float Fines;
    public float NetProfit;
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

/// <summary>
/// Eveniment declanșat în momentul autentificării cu succes a unui utilizator.
/// </summary>
public struct UserAuthenticatedEvent
{
    public string UserId;
    public string Username;
    public string Email; // Adăugat pentru a putea extrage un displayName dacă username nu e setat
}

/// <summary>
/// Eveniment declanșat atunci când operațiunea de login sau register eșuează.
/// </summary>
public struct AuthFailedEvent
{
    public string ErrorMessage;
}

public struct GameSaveDataEvent
{
    public int CurrentDay;
    public int PlayerLevel;
    public int CurrentMoney;
    public string ShopLayoutJson; // Pozițiile obiectelor, stocurilor și pereților transformate în text JSON
    public string ObjectivesJson;
    public string InventoryJson;
    public string EmployeesJson;
    public string BankLoansJson;
    public string ShelvesJson;
}

/// <summary>
/// Eveniment declanșat atunci când datele au fost descărcate cu succes din Cloud.
/// Transmite informațiile brute către managerii din joc pentru reconstituire.
/// </summary>
public struct GameDataLoadedEvent
{
    public int CurrentDay;
    public int CurrentMoney;
    public int PlayerLevel;
    public string ShopLayoutJson;
    public string ObjectivesJson;
    public string InventoryJson;
    public string EmployeesJson;
    public string BankLoansJson;
    public string ShelvesJson;
}

/// <summary>
/// Declanșat ori de câte ori jucătorul face o acțiune care îi crește scorul în Leaderboard.
/// </summary>
public struct ScoreGainedEvent
{
    public int Amount;      // Câte puncte a primit
    public string Source;   // De unde le-a primit (ex: "Obiectiv Completat", "Angajat Level Up")
}