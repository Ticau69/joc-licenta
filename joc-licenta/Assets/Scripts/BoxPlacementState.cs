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

    // Stare Dragging
    private Vector3Int startPosition;
    private bool isDragging = false;

    public BoxPlacementState(int iD,
                          Grid grid,
                          PreviewSystem previewSystem,
                          ObjectDataBase database,
                          GridData floorData,
                          ObjectPlacer objectPlacer,
                          GameManager gameManager)
    {
        ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.floorData = floorData;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;

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
        previewSystem.StopShowingPreview();
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

            // Resetăm starea
            isDragging = false;
            previewSystem.SetCursorSize(Vector2Int.one); // Resetăm cursorul la 1x1
        }
    }

    public void UpdateState(Vector3Int currentGridPosition)
    {
        if (!isDragging)
        {
            // Modul simplu (hover 1x1)
            bool isValid = floorData.canPlaceObjectAt(currentGridPosition, Vector2Int.one);
            previewSystem.UpdatePosition(grid.CellToWorld(currentGridPosition), isValid);
        }
        else
        {
            // Modul DRAG (extindere elastică)
            UpdateBoxPreview(currentGridPosition);
        }
    }

    private void UpdateBoxPreview(Vector3Int currentPos)
    {
        // 1. Calculăm colțurile dreptunghiului (Min/Max)
        int minX = Mathf.Min(startPosition.x, currentPos.x);
        int maxX = Mathf.Max(startPosition.x, currentPos.x);
        int minZ = Mathf.Min(startPosition.z, currentPos.z);
        int maxZ = Mathf.Max(startPosition.z, currentPos.z);

        // 2. Calculăm dimensiunile (ex: 3x2)
        Vector2Int size = new Vector2Int(Mathf.Abs(maxX - minX) + 1, Mathf.Abs(maxZ - minZ) + 1);

        // 3. Calculăm Centrul Geometric (pentru a muta cursorul acolo)
        Vector3 worldMin = grid.CellToWorld(new Vector3Int(minX, 0, minZ));

        // Offset pentru centrare (folosim logica ta de jumătate de celulă)
        // Atenție: Aici centrăm un obiect mare, deci adăugăm jumătate din TOATĂ mărimea
        Vector3 centerPos = new Vector3(
            worldMin.x + (size.x / 2f),
            worldMin.y,
            worldMin.z + (size.y / 2f)
        );

        // 4. Actualizăm Vizualul
        previewSystem.SetCursorSize(size); // Mărim pătratul alb

        // Verificăm dacă toată aria e validă (nu e ocupată și avem bani)
        bool isValid = CheckBoxValidity(startPosition, currentPos);

        // Mutăm cursorul și aplicăm culoarea (Alb/Roșu)
        // NOTĂ: Folosim o funcție specială de move sau UpdatePosition-ul existent
        // UpdatePosition-ul tău existent mută cursorul la "position".
        // Pentru Box, vrem să mutăm cursorul manual:

        // Hack: folosim logica internă a previewSystem dar adaptată
        // Cel mai simplu e să expui o metodă "SetPositionAndColor" în PreviewSystem
        // Sau să folosim UpdatePosition păcălind "currentSize"

        previewSystem.UpdatePosition(worldMin, isValid); // worldMin e colțul, UpdatePosition adaugă size/2
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