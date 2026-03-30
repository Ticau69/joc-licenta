using UnityEngine;

public class PlacementState : IBuldingState
{
    private int selectedObjectIndex = -1;
    int ID;
    Grid grid;
    PreviewSystem previewSystem;
    ObjectDataBase dataBase;
    GridData floorData;
    GridData furnitureData;
    ObjectPlacer objectPlacer;
    GameManager gameManager;

    public PlacementState(int iD,
                          Grid grid,
                          PreviewSystem previewSystem,
                          ObjectDataBase database,
                          GridData floorData,
                          GridData furnitureData,
                          ObjectPlacer objectPlacer,
                          GameManager gameManager)
    {
        ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.floorData = floorData;
        this.furnitureData = furnitureData;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;

        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);
        if (selectedObjectIndex > -1)
        {
            previewSystem.StartShowingPlacementPreview(
                database.objectsData[selectedObjectIndex].Prefab,
                database.objectsData[selectedObjectIndex].Size);
        }
        else
        {
            throw new System.Exception($"No ID found {iD}");
        }
    }

    public void EndState()
    {
        previewSystem.StopShowingPreview();
    }

    public void OnAction(Vector3Int gridPosition)
    {
        Vector2Int currentSize = previewSystem.GetCurrentSize();

        bool placementValidity = CheckPlacementValidity(gridPosition, currentSize);
        if (!placementValidity)
            return;

        int objectCost = dataBase.objectsData[selectedObjectIndex].Cost;

        if (gameManager.TrySpendMoney(objectCost) == false)
        {
            Debug.Log("Nu ai suficienți bani pentru a construi acest obiect!");
            return;
        }

        Quaternion currentRotation = previewSystem.GetCurrentRotation();
        Vector3 worldPosition = grid.CellToWorld(gridPosition);

        Vector3 centeredPosition = new Vector3(
            worldPosition.x + (currentSize.x / 2f),
            worldPosition.y,
            worldPosition.z + (currentSize.y / 2f)
        );

        int index = objectPlacer.PlaceObject(
            dataBase.objectsData[selectedObjectIndex].Prefab,
            centeredPosition,
            currentRotation);

        int consumption = dataBase.objectsData[selectedObjectIndex].PowerConsumption;
        if (consumption > 0 && PowerManager.Instance != null)
        {
            PowerManager.Instance.RegisterConsumer(consumption);
        }

        // --- REPARAT AICI: Folosim Categoria în loc de ID == 0 ---
        bool isFloor = dataBase.objectsData[selectedObjectIndex].Category == ObjectCategory.Structure;
        GridData selectedData = isFloor ? floorData : furnitureData;

        selectedData.AddObjectAt(
            gridPosition,
            currentSize,
            dataBase.objectsData[selectedObjectIndex].ID,
            index,
            currentRotation);

        if (EmployeeManager.Instance != null)
        {
            EmployeeManager.Instance.RefreshStations();
        }

        previewSystem.UpdatePosition(worldPosition, false);
    }

    private bool CheckPlacementValidity(Vector3Int gridPosition, Vector2Int size)
    {
        // 1. --- Extragem datele obiectului pe care vrem să-l plasăm ---
        ObjectData currentObjectData = dataBase.objectsData[selectedObjectIndex];

        bool isFloor = currentObjectData.Category == ObjectCategory.Structure;

        // Identificăm dacă este un raft de depozit folosind StationType!
        bool isStorageRack = currentObjectData.Category == ObjectCategory.Equipment &&
                             currentObjectData.StationType == StationType.Storage;

        GridData selectedData = isFloor ? floorData : furnitureData;

        // 2. --- Verificarea Standard (Sunt celulele libere pe layer-ul respectiv?) ---
        if (selectedData.canPlaceObjectAt(gridPosition, size) == false)
        {
            return false;
        }

        // 3. --- VERIFICARE: Mobila are nevoie de Podea (Și respectă regulile Depozitului) ---
        if (!isFloor)
        {
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector3Int positionToCheck = gridPosition + new Vector3Int(x, 0, y);

                    // A) Verificăm dacă lipsește podeaua complet
                    if (floorData.canPlaceObjectAt(positionToCheck, Vector2Int.one) == true)
                    {
                        return false; // Nu există podea deloc aici! Nu putem pune mobilă pe pământ.
                    }

                    // B) REGULA DEPOZITULUI
                    int floorID = floorData.GetObjectIDAt(positionToCheck);

                    if (isStorageRack)
                    {
                        // Raftul de depozit acceptă DOAR podeaua cu ID 5
                        if (floorID != 5)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // Orice altă mobilă (raft magazin, casă marcat) NU are voie pe podeaua 5
                        if (floorID == 5)
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector2Int currentSize = previewSystem.GetCurrentSize();
        bool placementValidity = CheckPlacementValidity(gridPosition, currentSize);
        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), placementValidity);
    }
}