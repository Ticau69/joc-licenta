using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WorkStationRegistry
{
    private static WorkStationRegistry _instance;
    public static WorkStationRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new WorkStationRegistry();
                // NOU: Își populează automat listele la prima utilizare!
                _instance.RefreshAllStations();
            }
            return _instance;
        }
    }

    private List<WorkStation> cashRegisters = new();
    private List<CashRegisterQueue> cashRegisterQueues = new();
    private List<WorkStation> storages = new();
    private List<WorkStation> shelves = new();
    private readonly List<WorkStation> _queryBuffer = new(16);

    public void RefreshAllStations()
    {
        // NOU: Forțăm Unity să găsească stațiile chiar dacă sunt temporar dezactivate!
        var allStations = Object.FindObjectsByType<WorkStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        cashRegisters = allStations
            .Where(x => x != null && x.stationType == StationType.CashRegister)
            .ToList();

        storages = allStations
            .Where(x => x != null && x.stationType == StationType.Storage)
            .ToList();

        shelves = allStations
            .Where(x => x != null && x.stationType == StationType.Shelf)
            .ToList();

        LogStationCounts();

        // NOU: Și cozile trebuie găsite chiar dacă sunt inactive!
        cashRegisterQueues = Object.FindObjectsByType<CashRegisterQueue>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
    }

    public WorkStation GetAnyCashRegister()
    {
        CleanNullStations(cashRegisters);
        return cashRegisters.Count > 0 ? cashRegisters[0] : null;
    }

    public IReadOnlyList<WorkStation> GetAllShelves()
    {
        CleanNullStations(shelves);
        return shelves;
    }

    public IReadOnlyList<WorkStation> GetAllStorages()
    {
        CleanNullStations(storages);
        return storages;
    }

    /// <summary>
    /// Returnează rafturile DESTINATE acestui produs, indiferent de stoc.
    /// Clientul nu știe dacă e sau nu în stoc — va descoperi când ajunge la raft.
    /// </summary>
    public List<WorkStation> GetShelvesForProduct(ProductType product)
    {
        CleanNullStations(shelves);
        _queryBuffer.Clear();

        for (int i = 0; i < shelves.Count; i++)
        {
            var s = shelves[i];
            if (s != null && s.stationType == StationType.Shelf && s.slotProduct == product)
                _queryBuffer.Add(s);
        }

        return _queryBuffer;
    }

    /// <summary>
    /// Versiunea veche — folosită intern de angajați/sisteme care chiar au nevoie să știe stocul.
    /// NU folosiți pentru clienți.
    /// </summary>
    public List<WorkStation> GetShelvesWithProductInStock(ProductType product)
    {
        CleanNullStations(shelves);
        _queryBuffer.Clear();

        for (int i = 0; i < shelves.Count; i++)
        {
            var s = shelves[i];
            if (s != null && s.stationType == StationType.Shelf && s.slotProduct == product && s.slotStock > 0)
                _queryBuffer.Add(s);
        }

        return _queryBuffer;
    }

    public IReadOnlyList<CashRegisterQueue> GetAllCashRegisterQueues()
    {
        cashRegisterQueues.RemoveAll(x => x == null);
        return cashRegisterQueues;
    }

    private void CleanNullStations(List<WorkStation> stationList)
    {
        stationList.RemoveAll(x => x == null);
    }

    private void LogStationCounts()
    {
        Debug.Log($"[WorkStationRegistry] Found stations - " +
                  $"Cash Registers: {cashRegisters.Count}, " +
                  $"Storages: {storages.Count}, " +
                  $"Shelves: {shelves.Count}");
    }
}