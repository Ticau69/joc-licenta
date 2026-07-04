using System.Collections.Generic;
using UnityEngine;

// 1. Clasa pentru a salva un raft sau o podea de pe Grid
[System.Serializable]
public class GridObjectSaveData
{
    public int ID;
    public Vector3Int AnchorPosition;
    public Quaternion Rotation;
}

// 2. Clasa pentru a salva un perete
[System.Serializable]
public class WallSaveData
{
    public Vector3 StartPos;
    public Vector3 EndPos;
    public int ID;
}

// Clasa care reține fix datele care se pot schimba la un obiectiv
[System.Serializable]
public class ObjectiveSaveData
{
    public string Id;
    public int CurrentProgress;
    public bool IsUnlocked;
    public bool IsCompleted;
}

// Clasa-pachet care le adună pe toate
[System.Serializable]
public class ObjectivesSaveState
{
    public List<ObjectiveSaveData> SavedObjectives = new List<ObjectiveSaveData>();
}

// "Cutia" pentru un singur tip de produs
[System.Serializable]
public class ProductStockData
{
    public ProductType Product;
    public int Quantity;
}

// Pachetul mare care conține tot depozitul
[System.Serializable]
public class InventorySaveState
{
    public List<ProductStockData> StockList = new List<ProductStockData>();
}

[System.Serializable]
public class EmployeeSaveData
{
    public string EmployeeType; // ex: "Cashier", "Janitor", "Replenisher"
    public int Level;
    public float CurrentMood;
    public string Name;
    public EmployeeRole Role;
    public EmployeeGender Gender;
}

// Pachetul care conține toată echipa
[System.Serializable]
public class EmployeesSaveState
{
    public List<EmployeeSaveData> HiredEmployees = new List<EmployeeSaveData>();
}

[System.Serializable]
public class StructureSaveData
{
    public int ObjectID;             // ID-ul din baza ta de date (Catalog/ScriptableObjects)
    public Vector3Int GridPosition;  // Coordonatele discrete de pe grid
    public int RotationIndex;        // Rotația aplicată obiectului
    public int CurrentStock;         // Dacă e raft, salvăm câte produse mai are pe el
}

[System.Serializable]
public class ShopSaveState
{
    public List<GridObjectSaveData> Floors = new List<GridObjectSaveData>();
    public List<GridObjectSaveData> Furniture = new List<GridObjectSaveData>();
    public List<WallSaveData> Walls = new List<WallSaveData>();
    public List<EmployeeSaveData> Employees = new List<EmployeeSaveData>();
    public List<DoorSaveData> Doors = new List<DoorSaveData>();
    public List<string> UnlockedPlots = new List<string>();
}

[System.Serializable]
public class LoanSaveData
{
    public string BankName; // Identificatorul SO-ului
    public float Principal;
    public float WeeklyPayment;
    public float AnnualRateSnapshot;
    public float TotalOwed;
    public float TotalPaid;
    public int TermDays;
    public int DayTaken;
    public int NextPaymentDay;
    public int WeeksRemaining;
}

[System.Serializable]
public class BankSaveState
{
    public List<LoanSaveData> ActiveLoans = new();
}

[System.Serializable]
public class ShelfSaveData
{
    // "CNP"-ul raftului (Poziția exactă în grid, reținută ca text pentru JSON simplu)
    public string PositionXYZ;

    // Conținutul
    public ProductType ConfiguredProduct;
    public ProductType PendingProduct;
    public int CurrentStock;
}

public struct GameUIRefreshEvent { }

[System.Serializable]
public class ShelvesSaveState
{
    public List<ShelfSaveData> ActiveShelves = new();
}

[System.Serializable]
public class DoorSaveData
{
    public int ID;
    public Vector3 Position;
    public Quaternion Rotation;
}

