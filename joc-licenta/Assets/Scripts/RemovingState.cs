using UnityEngine;
using System.Collections.Generic; // Necesar pentru List

public class RemovingState : IBuldingState
{
    private int gameObjectIndex = -1;
    private bool isWall = false;
    Grid grid;
    PreviewSystem previewSystem;
    GridData floorData;
    GridData furnitureData;
    WallGridData wallData;
    WallSegmentData segmentData; // <--- 1. ADĂUGAT: Referință la segmente
    ObjectPlacer objectPlacer;
    ObjectDataBase dataBase;

    public RemovingState(
        Grid grid,
        PreviewSystem previewSystem,
        GridData floorData,
        GridData furnitureData,
        ObjectPlacer objectPlacer,
        ObjectDataBase dataBase,
        WallGridData wallData = null,
        WallSegmentData segmentData = null) // <--- 2. ADĂUGAT: Parametru în constructor
    {
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.floorData = floorData;
        this.furnitureData = furnitureData;
        this.objectPlacer = objectPlacer;
        this.dataBase = dataBase;
        this.wallData = wallData;
        this.segmentData = segmentData; // <--- 3. Salvăm referința

        previewSystem.StartShowingRemovePreview();
    }

    public void EndState()
    {
        previewSystem.StopShowingPreview();
    }

    public void OnAction(Vector3Int gridPosition)
    {
        GridData selectedData = null;
        Vector3 worldPos = grid.CellToWorld(gridPosition);

        // --- FIX: ȘTERGERE SEGMENTE ---
        // 1. Verificăm mai întâi sistemul nou de segmente
        if (segmentData != null)
        {
            // Căutăm segmente aproape de punctul click-ului (folosim o rază egală cu jumătate de celulă)
            int removedCount = 0;
            segmentData.RemoveSegmentsInRange(worldPos, grid.cellSize.x * 0.7f, out removedCount);

            if (removedCount > 0)
            {
                Debug.Log("Segment de perete șters!");

                // Opțional: Curățăm și datele vechi din wallData dacă nu mai există segmente
                // Dar pentru moment vizual e suficient.
                return;
            }
        }

        // 2. Fallback la sistemul vechi (pentru pereți vechi sau compatibilitate)
        if (wallData != null)
        {
            WallData wall = wallData.FindWallNearPoint(worldPos, grid.cellSize.x);

            if (wall != null)
            {
                // Verificăm consumul de energie
                var objectSettings = dataBase.objectsData.Find(x => x.ID == wall.ID);
                if (objectSettings != null && objectSettings.PowerConsumption > 0)
                {
                    if (PowerManager.Instance != null)
                    {
                        PowerManager.Instance.UnregisterConsumer(objectSettings.PowerConsumption);
                    }
                }

                wallData.RemoveWall(wall.StartPosition, wall.EndPosition);
                Debug.Log("Perete (Legacy) șters!");
                return;
            }
        }
        // -----------------------------

        // 3. Verificăm mobilă
        if (furnitureData.canPlaceObjectAt(gridPosition, Vector2Int.one) == false)
        {
            selectedData = furnitureData;
        }
        // 4. Verificăm podea
        else if (floorData.canPlaceObjectAt(gridPosition, Vector2Int.one) == false)
        {
            selectedData = floorData;
        }

        if (selectedData == null)
        {
            // Debug.Log("Nu există nimic de șters aici!");
        }
        else
        {
            int objectID = selectedData.GetObjectIDAt(gridPosition);
            if (objectID != -1)
            {
                var objectSettings = dataBase.objectsData.Find(x => x.ID == objectID);
                if (objectSettings != null && objectSettings.PowerConsumption > 0)
                {
                    if (PowerManager.Instance != null)
                    {
                        PowerManager.Instance.UnregisterConsumer(objectSettings.PowerConsumption);
                    }
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
        bool hasFurniture = !furnitureData.canPlaceObjectAt(gridPosition, Vector2Int.one);
        bool hasFloor = !floorData.canPlaceObjectAt(gridPosition, Vector2Int.one);

        bool hasWall = false;
        Vector3 worldPos = grid.CellToWorld(gridPosition);

        // Verificăm segmente
        if (segmentData != null)
        {
            var segments = segmentData.FindSegmentsNearPoint(worldPos, grid.cellSize.x * 0.5f);
            if (segments.Count > 0) hasWall = true;
        }

        // Verificăm pereți vechi
        if (!hasWall && wallData != null)
        {
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