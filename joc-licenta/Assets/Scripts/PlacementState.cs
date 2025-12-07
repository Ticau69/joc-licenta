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

        // 1. Luăm poziția colțului
        Vector3 worldPosition = grid.CellToWorld(gridPosition);

        // 2. --- FIX: Calculăm poziția CENTRATĂ (exact ca în PreviewSystem) ---
        Vector3 centeredPosition = new Vector3(
            worldPosition.x + (currentSize.x / 2f),
            worldPosition.y,
            worldPosition.z + (currentSize.y / 2f)
        );
        // ---------------------------------------------------------------------

        // 3. Trimitem poziția CENTRATĂ la ObjectPlacer
        int index = objectPlacer.PlaceObject(
            dataBase.objectsData[selectedObjectIndex].Prefab,
            centeredPosition, // <--- AICI am schimbat din worldPosition în centeredPosition
            currentRotation);

        int consumption = dataBase.objectsData[selectedObjectIndex].PowerConsumption;
        if (consumption > 0)
        {
            if (PowerManager.Instance != null)
            {
                Debug.Log(consumption);
                PowerManager.Instance.RegisterConsumer(consumption);
            }
        }

        // ... Restul codului rămâne la fel (AddObjectAt folosește gridPosition, e corect) ...
        GridData selectedData = dataBase.objectsData[selectedObjectIndex].ID == 0
            ? floorData
            : furnitureData;

        selectedData.AddObjectAt(
            gridPosition,
            currentSize,
            dataBase.objectsData[selectedObjectIndex].ID,
            index,
            currentRotation);

        // Update vizual
        previewSystem.UpdatePosition(worldPosition, false);
    }

    private bool CheckPlacementValidity(Vector3Int gridPosition, Vector2Int size)
    {
        // 1. Identificăm ce fel de date folosim (Podea sau Mobilă?)
        GridData selectedData = dataBase.objectsData[selectedObjectIndex].ID == 0
            ? floorData
            : furnitureData;

        // 2. Verificarea Standard: Spațiul este liber de alte obiecte de același tip?
        // (Ex: Nu punem scaun peste scaun)
        if (selectedData.canPlaceObjectAt(gridPosition, size) == false)
        {
            return false; // E ocupat, deci invalid
        }

        // 3. --- VERIFICARE NOUĂ: Mobila are nevoie de Podea ---
        // Dacă obiectul curent NU este podea (deci e mobilă)
        if (dataBase.objectsData[selectedObjectIndex].ID != 0)
        {
            // Trebuie să verificăm fiecare pătrățel pe care îl ocupă mobila
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector3Int positionToCheck = gridPosition + new Vector3Int(x, 0, y);

                    // Întrebăm floorData: "Pot plasa o podea aici?"
                    // Dacă răspunsul este DA (true), înseamnă că e GOL -> deci NU avem podea.
                    // Dacă răspunsul este NU (false), înseamnă că e OCUPAT -> deci AVEM podea.

                    if (floorData.canPlaceObjectAt(positionToCheck, Vector2Int.one) == true)
                    {
                        // E gol pe jos (lipsă podea), deci nu putem pune mobila
                        return false;
                    }
                }
            }
        }
        // ------------------------------------------------------

        return true;
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector2Int currentSize = previewSystem.GetCurrentSize();
        bool placementValidity = CheckPlacementValidity(gridPosition, currentSize);
        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), placementValidity);
    }
}