using UnityEngine;

/// <summary>
/// Creează toate serviciile jocului și le înregistrează în ServiceLocator.
/// 
/// EventBus-ul este primit de la GameInitializer (care persistă între scene)
/// sau creat local dacă jocul pornește direct din scena de joc (ex: în Editor).
/// </summary>
public class GameServiceInstaller : MonoBehaviour
{
    public IEconomyService Economy { get; private set; }
    public IMoneyService Money { get; private set; }
    public IInventoryService Inventory { get; private set; }
    public IShopService Shop { get; private set; }
    public IEventBus EventBus { get; private set; }
    public IObjectRegistry ObjectRegistry { get; private set; }

    private GameConfigSO _config;
    public bool Install(GameConfigSO config, ProductDataSO productDB)
    {
        _config = config;

        // Preluăm EventBus-ul global din GameInitializer (creat în Login scene, persistă între scene).
        // Fallback la un EventBus local doar dacă pornim direct în scena de joc (ex: în Editor).
        EventBus = GameInitializer.Instance?.EventBus ?? new EventBus();
        ObjectRegistry = new ObjectRegistry();
        ObjectRegistry.Register(GameManager.Instance);

        Economy = new EconomyManager(productDB, EventBus, config);
        Money = new MoneyManager(config.startingMoney, EventBus, config);
        Inventory = new InventoryService(ObjectRegistry, EventBus, config);
        Shop = new ShopManager(Money, Economy, Inventory, EventBus, config);

        RegisterInLocator(config);

        if (config.verboseLogging)
            Debug.Log("[GameServiceInstaller] Toate serviciile inițializate.");

        return true;
    }

    private void RegisterInLocator(GameConfigSO config)
    {
        var loc = ServiceLocator.Instance;
        loc.Register<IEconomyService>(Economy);
        loc.Register<IMoneyService>(Money);
        loc.Register<IInventoryService>(Inventory);
        loc.Register<IShopService>(Shop);
        loc.Register<IEventBus>(EventBus);
        loc.Register<IObjectRegistry>(ObjectRegistry);
        loc.Register(config);

        if (config.verboseLogging)
            loc.LogRegisteredServices();
    }

    public void Cleanup()
    {
        (Money as MoneyManager)?.Cleanup();
        ServiceLocator.Instance.Clear();

        if (_config.verboseLogging)
            Debug.Log("[GameServiceInstaller] Servicii eliberate.");
    }
}