using UnityEngine;

public class RemovingState : IBuldingState
{
    private int gameObjectIndex = -1;
    private bool isWall = false;
    Grid grid;
    PreviewSystem previewSystem;
    GridData floorData;
    GridData furnitureData;
    WallGridData wallData; // NOU
    ObjectPlacer objectPlacer;
    ObjectDataBase dataBase;

    public RemovingState(
        Grid grid,
        PreviewSystem previewSystem,
        GridData floorData,
        GridData furnitureData,
        ObjectPlacer objectPlacer,
        ObjectDataBase dataBase,
        WallGridData wallData = null)
    {
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.floorData = floorData;
        this.furnitureData = furnitureData;
        this.objectPlacer = objectPlacer;
        this.dataBase = dataBase;
        this.wallData = wallData;

        previewSystem.StartShowingRemovePreview();
    }

    public void EndState()
    {
        previewSystem.StopShowingPreview();
    }

    public void OnAction(Vector3Int gridPosition)
    {
        GridData selectedData = null;

        // 1. Verificăm dacă am dat click pe un perete
        if (wallData != null)
        {
            Vector3 worldPos = grid.CellToWorld(gridPosition);
            WallData wall = wallData.FindWallNearPoint(worldPos, grid.cellSize.x);

            if (wall != null)
            {
                // Găsit perete!
                isWall = true;

                // Verificăm dacă peretele consumă energie
                var objectSettings = dataBase.objectsData.Find(x => x.ID == wall.ID);
                if (objectSettings != null && objectSettings.PowerConsumption > 0)
                {
                    if (PowerManager.Instance != null)
                    {
                        PowerManager.Instance.UnregisterConsumer(objectSettings.PowerConsumption);
                    }
                }

                // Ștergem peretele
                wallData.RemoveWall(wall.StartPosition, wall.EndPosition);

                Debug.Log("Perete șters!");
                return;
            }
        }

        // 2. Dacă nu e perete, verificăm mobilă
        if (furnitureData.canPlaceObjectAt(gridPosition, Vector2Int.one) == false)
        {
            selectedData = furnitureData;
        }
        // 3. Dacă nu e mobilă, verificăm podea
        else if (floorData.canPlaceObjectAt(gridPosition, Vector2Int.one) == false)
        {
            selectedData = floorData;
        }

        if (selectedData == null)
        {
            Debug.Log("Nu există nimic de șters aici!");
        }
        else
        {
            int objectID = selectedData.GetObjectIDAt(gridPosition);
            var objectSettings = dataBase.objectsData.Find(x => x.ID == objectID);

            if (objectSettings != null && objectSettings.PowerConsumption > 0)
            {
                if (PowerManager.Instance != null)
                {
                    PowerManager.Instance.UnregisterConsumer(objectSettings.PowerConsumption);
                }
            }

            gameObjectIndex = selectedData.GetRepresentationIndex(gridPosition);
            if (gameObjectIndex == -1)
                return;

            selectedData.RemoveObjectAt(gridPosition);
            objectPlacer.RemoveObjectAt(gameObjectIndex);
        }

        Vector3 cellPosition = grid.CellToWorld(gridPosition);
        previewSystem.UpdatePosition(cellPosition, CheckIfSelectionIsValid(gridPosition));
    }

    public bool CheckIfSelectionIsValid(Vector3Int gridPosition)
    {
        // Verificăm dacă există ceva de șters
        bool hasFurniture = !furnitureData.canPlaceObjectAt(gridPosition, Vector2Int.one);
        bool hasFloor = !floorData.canPlaceObjectAt(gridPosition, Vector2Int.one);

        bool hasWall = false;
        if (wallData != null)
        {
            Vector3 worldPos = grid.CellToWorld(gridPosition);
            hasWall = wallData.FindWallNearPoint(worldPos, grid.cellSize.x) != null;
        }

        return hasFurniture || hasFloor || hasWall;
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        bool validity = CheckIfSelectionIsValid(gridPosition);
        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), validity);
    }
}