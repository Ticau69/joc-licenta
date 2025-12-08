using System;
using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Grid grid;
    [SerializeField] private ObjectDataBase database;
    [SerializeField] private GameObject gridVisualization;
    [SerializeField] private PreviewSystem previewSystem;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private GameManager gameManager;

    private GridData floorData, furnitureData;
    private WallGridData wallData;
    private Vector3Int lastDetectedPosition = Vector3Int.zero;
    private IBuldingState buildingState;

    private bool isWallMode = false;

    private void Start()
    {
        gridVisualization.SetActive(false);
        floorData = new();
        furnitureData = new();
        wallData = new WallGridData();
    }

    private void Update()
    {
        if (buildingState == null)
            return;

        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        if (lastDetectedPosition != gridPosition || isWallMode)
        {
            buildingState.UpdateState(gridPosition);
            lastDetectedPosition = gridPosition;
        }
    }

    public void StartPlacement(int ID)
    {
        StopPlacement();
        gridVisualization.SetActive(true);

        if (ID == 0) // Podea
        {
            isWallMode = false;
            buildingState = new BoxPlacementState(
                ID, grid, previewSystem, database,
                floorData, objectPlacer, gameManager);

            // Box folosește click standard
            playerInput.OnClick += PlaceStructure;
        }
        else if (ID == 1) // Perete - MULTI-SEGMENT MODE
        {
            isWallMode = true;
            buildingState = new WallPlacementState(
                ID, grid, previewSystem, database,
                objectPlacer, gameManager, playerInput, wallData);

            // Wall folosește click pentru fiecare punct
            playerInput.OnClick += PlaceStructure; // Adaugă puncte
            playerInput.OnRightClick += CancelWallSegment; // Anulare
        }
        else // Mobilă
        {
            isWallMode = false;
            buildingState = new PlacementState(
                ID, grid, previewSystem, database,
                floorData, furnitureData, objectPlacer, gameManager);

            // Mobilă folosește click standard
            playerInput.OnClick += PlaceStructure;
        }

        playerInput.OnExit += StopPlacement;
        playerInput.OnRotate += RotateStructure;
    }

    public void StartRemoving()
    {
        StopPlacement();
        gridVisualization.SetActive(true);
        isWallMode = false;

        buildingState = new RemovingState(
            grid, previewSystem,
            floorData, furnitureData,
            objectPlacer, database, wallData);

        playerInput.OnClick += PlaceStructure;
        playerInput.OnExit += StopPlacement;
    }

    // Pentru obiecte normale și pereți multi-segment (click)
    private void PlaceStructure()
    {
        if (playerInput.IsPointerOverUI())
            return;

        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        buildingState.OnAction(gridPosition);
    }

    // Pentru anularea segmentului curent de perete
    private void CancelWallSegment()
    {
        if (!isWallMode) return;

        // Trimitem un mesaj special pentru anulare
        // Putem folosi o poziție specială sau o metodă dedicată
        StopPlacement();
        StartPlacement(1); // Restart wall mode
    }

    private void RotateStructure()
    {
        if (buildingState == null || isWallMode)
            return; // Nu rotim pereții

        previewSystem.RotatePreview();

        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);
        buildingState.UpdateState(gridPosition);
    }

    private void StopPlacement()
    {
        if (buildingState == null)
            return;

        gridVisualization.SetActive(false);
        buildingState.EndState();

        // Unsubscribe de la toate evenimentele
        playerInput.OnClick -= PlaceStructure;
        //playerInput.OnMouseDown -= StartWallDrag;
        //playerInput.OnMouseUp -= FinishWallDrag;
        playerInput.OnRightClick -= CancelWallSegment;
        playerInput.OnExit -= StopPlacement;
        playerInput.OnRotate -= RotateStructure;

        lastDetectedPosition = Vector3Int.zero;
        buildingState = null;
        isWallMode = false;
    }

    // Metode helper pentru debugging
    public void DebugShowAllWalls()
    {
        var walls = wallData.GetAllWalls();
        Debug.Log($"Total pereți: {walls.Count}");
        foreach (var wall in walls)
        {
            Debug.Log($"Perete: {wall.StartPosition} -> {wall.EndPosition}, Lungime: {wall.Length}");
        }
    }

    public WallGridData GetWallData() => wallData;
}