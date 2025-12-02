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

    public PlacementState(int iD,
                          Grid grid,
                          PreviewSystem previewSystem,
                          ObjectDataBase database,
                          GridData floorData,
                          GridData furnitureData,
                          ObjectPlacer objectPlacer)
    {
        ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.floorData = floorData;
        this.furnitureData = furnitureData;
        this.objectPlacer = objectPlacer;

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
        GridData selectedData = dataBase.objectsData[selectedObjectIndex].ID == 0
            ? floorData
            : furnitureData;

        return selectedData.canPlaceObjectAt(gridPosition, size);
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector2Int currentSize = previewSystem.GetCurrentSize();
        bool placementValidity = CheckPlacementValidity(gridPosition, currentSize);
        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), placementValidity);
    }
}