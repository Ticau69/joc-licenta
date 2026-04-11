using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WorkStationRegistry
{
    private List<WorkStation> cashRegisters = new();
    private List<CashRegisterQueue> cashRegisterQueues = new();
    private List<WorkStation> storages = new();
    private List<WorkStation> shelves = new();

    public void RefreshAllStations()
    {
        var allStations = Object.FindObjectsByType<WorkStation>(FindObjectsSortMode.None);

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
        cashRegisterQueues = Object.FindObjectsByType<CashRegisterQueue>(FindObjectsSortMode.None).ToList();
    }

    public WorkStation GetAnyCashRegister()
    {
        CleanNullStations(cashRegisters);
        return cashRegisters.Count > 0 ? cashRegisters[0] : null;
    }

    public List<WorkStation> GetAllShelves()
    {
        CleanNullStations(shelves);
        return new List<WorkStation>(shelves);
    }

    public List<WorkStation> GetAllStorages()
    {
        CleanNullStations(storages);
        return new List<WorkStation>(storages);
    }

    /// <summary>
    /// Returnează rafturile DESTINATE acestui produs, indiferent de stoc.
    /// Clientul nu știe dacă e sau nu în stoc — va descoperi când ajunge la raft.
    /// </summary>
    public List<WorkStation> GetShelvesForProduct(ProductType product)
    {
        CleanNullStations(shelves);

        return shelves
            .Where(s =>
                s != null &&
                s.stationType == StationType.Shelf &&
                s.slotProduct == product)
            .ToList();
    }

    /// <summary>
    /// Versiunea veche — folosită intern de angajați/sisteme care chiar au nevoie să știe stocul.
    /// NU folosiți pentru clienți.
    /// </summary>
    public List<WorkStation> GetShelvesWithProductInStock(ProductType product)
    {
        CleanNullStations(shelves);

        return shelves
            .Where(s =>
                s != null &&
                s.stationType == StationType.Shelf &&
                s.slotProduct == product &&
                s.slotStock > 0)
            .ToList();
    }

    public List<CashRegisterQueue> GetAllCashRegisterQueues()
    {
        cashRegisterQueues.RemoveAll(x => x == null);
        return new List<CashRegisterQueue>(cashRegisterQueues);
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