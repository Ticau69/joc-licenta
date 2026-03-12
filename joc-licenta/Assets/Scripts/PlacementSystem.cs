using UnityEngine;
using System.Collections.Generic;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Grid grid;
    [SerializeField] private ObjectDataBase database;
    [Header("Grid Visuals")]
    [Tooltip("Trage aici toate obiectele gridVisualization din scenă")]
    [SerializeField] private List<GameObject> gridVisualizations = new List<GameObject>();
    [SerializeField] private PreviewSystem previewSystem;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Shader wallPreviewShader;

    private GridData floorData, furnitureData;
    private WallGridData wallData;
    private WallSegmentData segmentData;
    private DoorData doorData; // NOU: Tracking pentru uși
    private Vector3Int lastDetectedPosition = Vector3Int.zero;
    private IBuldingState buildingState;

    private bool isWallMode = false;

    private void Start()
    {
        ToggleGridVisuals(false);
        floorData = new();
        furnitureData = new();
        wallData = new WallGridData();
        segmentData = new WallSegmentData(0.5f);
        doorData = new DoorData(); // NOU: Inițializăm tracking-ul pentru uși
    }

    private void Update()
    {
        if (buildingState == null)
            return;

        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        // --- NOU: Verificăm dacă suntem în modul de ștergere ---
        bool isRemoving = buildingState is RemovingState;

        // Dacă suntem pe un grid nou SAU e modul de perete SAU e modul de ștergere, dăm update!
        if (lastDetectedPosition != gridPosition || isWallMode || isRemoving)
        {
            buildingState.UpdateState(gridPosition);
            lastDetectedPosition = gridPosition;
        }
    }

    private void ToggleGridVisuals(bool isActive)
    {
        foreach (GameObject visual in gridVisualizations)
        {
            if (visual != null)
            {
                visual.SetActive(isActive);
            }
        }
    }

    public void AddGridVisual(GameObject newVisual)
    {
        if (newVisual != null && !gridVisualizations.Contains(newVisual))
        {
            gridVisualizations.Add(newVisual);

            // Verificăm dacă primul grid din listă (cel al magazinului) este aprins.
            // Dacă da, înseamnă că suntem în modul Build, deci aprindem și noul grid instant!
            if (gridVisualizations.Count > 0 && gridVisualizations[0].activeSelf)
            {
                newVisual.SetActive(true);
            }
            else
            {
                newVisual.SetActive(false); // Altfel îl ținem ascuns până deschide meniul
            }
        }
    }

    public void StartPlacement(int ID)
    {
        StopPlacement();
        ToggleGridVisuals(true);

        playerInput.canInteract = false;

        if (ID == 0) // Podea
        {
            isWallMode = false;
            buildingState = new BoxPlacementState(
                ID, grid, previewSystem, database,
                floorData, objectPlacer, gameManager);

            playerInput.OnClick += PlaceStructure;
        }
        else if (ID == 1) // Perete - MULTI-SEGMENT MODE
        {
            isWallMode = true;
            buildingState = new WallPlacementState(
                ID, grid, previewSystem, database,
                objectPlacer, gameManager, playerInput, wallData, segmentData, wallPreviewShader);

            playerInput.OnClick += PlaceStructure; // Adaugă puncte
            playerInput.OnRightClick += CancelWallSegment; // Anulare
        }
        else if (ID == 2) // Ușă - WALL SNAP MODE
        {
            isWallMode = false;
            buildingState = new DoorPlacementState(
                ID, grid, previewSystem, database,
                objectPlacer, gameManager, wallData, segmentData, doorData); // +doorData

            playerInput.OnClick += PlaceStructure;
        }
        else // Mobilă
        {
            isWallMode = false;
            buildingState = new PlacementState(
                ID, grid, previewSystem, database,
                floorData, furnitureData, objectPlacer, gameManager);

            playerInput.OnClick += PlaceStructure;
        }

        playerInput.OnExit += StopPlacement;
        playerInput.OnRotate += RotateStructure;
    }

    public void StartRemoving()
    {
        StopPlacement();
        ToggleGridVisuals(true);
        isWallMode = false;

        playerInput.canInteract = false;

        // ACTUALIZAT: Trimitem și segmentData, și doorData
        buildingState = new RemovingState(
            grid,
            previewSystem,
            floorData,
            furnitureData,
            objectPlacer,
            database,
            wallData,      // Legacy walls
            segmentData,   // New segments
            doorData       // New doors
        );

        playerInput.OnClick += PlaceStructure;
        playerInput.OnExit += StopPlacement;
    }

    // Pentru obiecte normale și pereți multi-segment (click)
    private void PlaceStructure()
    {
        if (playerInput.IsPointerOverUI())
            return;

        //Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        //Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        buildingState.OnAction(lastDetectedPosition);
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

        ToggleGridVisuals(false);
        buildingState.EndState();

        playerInput.canInteract = true;

        // Unsubscribe de la toate evenimentele
        playerInput.OnClick -= PlaceStructure;
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