using UnityEngine;
using System.Collections.Generic;

public class BoxPlacementState : IBuldingState
{
    private int selectedObjectIndex = -1;
    int ID;
    Grid grid;
    PreviewSystem previewSystem;
    ObjectDataBase dataBase;
    GridData floorData;
    ObjectPlacer objectPlacer;
    GameManager gameManager;
    ToolTipController toolTip;

    // Stare Dragging
    private Vector3Int startPosition;
    private bool isDragging = false;
    private List<GameObject> tilePreviewObjects = new List<GameObject>();

    public BoxPlacementState(int iD,
                          Grid grid,
                          PreviewSystem previewSystem,
                          ObjectDataBase database,
                          GridData floorData,
                          ObjectPlacer objectPlacer,
                          GameManager gameManager,
                          ToolTipController toolTip)
    {
        ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.floorData = floorData;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;
        this.toolTip = toolTip;

        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);
        if (selectedObjectIndex > -1)
        {
            // Pornim cu un cursor simplu 1x1
            previewSystem.StartShowingPlacementPreview(
                database.objectsData[selectedObjectIndex].Prefab,
                new Vector2Int(1, 1));
        }
    }

    public void EndState()
    {
        ClearTilePreviews();
        toolTip?.HidePlacementInfo();
        previewSystem.StopShowingPreview();
    }

    private void ClearTilePreviews()
    {
        foreach (var tile in tilePreviewObjects)
            if (tile != null) GameObject.Destroy(tile);
        tilePreviewObjects.Clear();
    }

    public void OnAction(Vector3Int gridPosition)
    {
        // CLICK 1: Începem selecția
        if (!isDragging)
        {
            startPosition = gridPosition;
            isDragging = true;
        }
        // CLICK 2: Confirmăm și construim
        else
        {
            // Executăm construcția doar dacă e validă
            if (CheckBoxValidity(startPosition, gridPosition))
            {
                PlaceBox(startPosition, gridPosition);
            }

            isDragging = false;
            ClearTilePreviews();
            previewSystem.SetCursorSize(Vector2Int.one);

            // Resetăm starea
            isDragging = false;
            previewSystem.SetCursorSize(Vector2Int.one); // Resetăm cursorul la 1x1
        }
    }

    public void UpdateState(Vector3Int currentGridPosition)
    {
        if (!isDragging)
        {
            bool isValid = floorData.canPlaceObjectAt(currentGridPosition, Vector2Int.one);
            previewSystem.UpdatePosition(grid.CellToWorld(currentGridPosition), isValid);

            // Tooltip hover - 1 celulă
            int costPerTile = dataBase.objectsData[selectedObjectIndex].Cost;
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            toolTip?.ShowPlacementInfo(
                $"Cost: {costPerTile}RON/celulă\nClick pentru a începe", mousePos);
        }
        else
        {
            UpdateBoxPreview(currentGridPosition);
        }
    }

    private void UpdateBoxPreview(Vector3Int currentPos)
    {
        int minX = Mathf.Min(startPosition.x, currentPos.x);
        int maxX = Mathf.Max(startPosition.x, currentPos.x);
        int minZ = Mathf.Min(startPosition.z, currentPos.z);
        int maxZ = Mathf.Max(startPosition.z, currentPos.z);

        Vector2Int size = new Vector2Int(Mathf.Abs(maxX - minX) + 1, Mathf.Abs(maxZ - minZ) + 1);
        int totalTiles = size.x * size.y;
        int costPerTile = dataBase.objectsData[selectedObjectIndex].Cost;
        int totalCost = totalTiles * costPerTile;
        bool canAfford = gameManager.CurrentMoney >= totalCost;

        // Tooltip
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        string info = $"Suprafață: {size.x}x{size.y} ({totalTiles} celule)\nCost: {totalCost}RON";
        if (!canAfford)
            info += "\n<color=red>Fonduri insuficiente!</color>";
        toolTip?.ShowPlacementInfo(info, mousePos);

        // Cursorului îi setăm dimensiunea pentru gridul alb
        previewSystem.SetCursorSize(size);
        Vector3 worldMin = grid.CellToWorld(new Vector3Int(minX, 0, minZ));
        previewSystem.UpdatePosition(worldMin, canAfford);

        // Regenerăm preview-urile per celulă
        RefreshTilePreviews(minX, maxX, minZ, maxZ, canAfford);
    }

    private void RefreshTilePreviews(int minX, int maxX, int minZ, int maxZ, bool isValid)
    {
        int needed = (maxX - minX + 1) * (maxZ - minZ + 1);

        while (tilePreviewObjects.Count < needed)
        {
            GameObject tile = GameObject.Instantiate(dataBase.objectsData[selectedObjectIndex].Prefab);
            DisableCollidersOn(tile);

            // Centrăm vizual prefab-ul față de pivot (ca în ObjectPlacer)
            Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                // Offset local ca să fie centrat pe celulă
                Vector3 localCenter = tile.transform.InverseTransformPoint(bounds.center);
                foreach (Transform child in tile.transform)
                    child.localPosition -= new Vector3(localCenter.x, 0, localCenter.z);
            }

            previewSystem.PreparePreviewObject(tile);
            tilePreviewObjects.Add(tile);
        }

        for (int i = needed; i < tilePreviewObjects.Count; i++)
            tilePreviewObjects[i].SetActive(false);

        float halfCellX = grid.cellSize.x / 2f;
        float halfCellZ = grid.cellSize.z / 2f;

        int index = 0;
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                GameObject tile = tilePreviewObjects[index++];
                tile.SetActive(true);

                Vector3 worldPos = grid.CellToWorld(new Vector3Int(x, 0, z));

                // Centrăm pe celulă folosind cellSize real
                tile.transform.position = new Vector3(
                    worldPos.x + halfCellX,
                    worldPos.y + 0.06f,
                    worldPos.z + halfCellZ
                );

                bool cellValid = isValid && floorData.canPlaceObjectAt(new Vector3Int(x, 0, z), Vector2Int.one);
                previewSystem.ApplyFeedbackToObject(tile, cellValid);
            }
        }
    }

    private void DisableCollidersOn(GameObject obj)
    {
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private bool CheckBoxValidity(Vector3Int start, Vector3Int end)
    {
        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        int minZ = Mathf.Min(start.z, end.z);
        int maxZ = Mathf.Max(start.z, end.z);

        int totalCost = 0;
        int costPerTile = dataBase.objectsData[selectedObjectIndex].Cost;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector3Int pos = new Vector3Int(x, 0, z);

                // Dacă e ocupat -> Invalid
                if (!floorData.canPlaceObjectAt(pos, Vector2Int.one))
                    return false;

                totalCost += costPerTile;
            }
        }

        // Dacă nu avem bani pentru TOT dreptunghiul -> Invalid
        if (gameManager.CurrentMoney < totalCost)
            return false;

        return true;
    }

    private void PlaceBox(Vector3Int start, Vector3Int end)
    {
        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        int minZ = Mathf.Min(start.z, end.z);
        int maxZ = Mathf.Max(start.z, end.z);

        int costPerTile = dataBase.objectsData[selectedObjectIndex].Cost;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector3Int pos = new Vector3Int(x, 0, z);

                if (floorData.canPlaceObjectAt(pos, Vector2Int.one))
                {
                    if (gameManager.TrySpendMoney(costPerTile))
                    {
                        // Calculăm poziția centrată pentru fiecare dală individuală
                        Vector3 worldPos = grid.CellToWorld(pos);
                        Vector3 centeredPos = new Vector3(worldPos.x + 0.5f, worldPos.y, worldPos.z + 0.5f);

                        int index = objectPlacer.PlaceObject(
                            dataBase.objectsData[selectedObjectIndex].Prefab,
                            centeredPos,
                            Quaternion.identity);

                        floorData.AddObjectAt(pos, Vector2Int.one, ID, index, Quaternion.identity);
                    }
                }
            }
        }
    }
}