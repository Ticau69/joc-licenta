using UnityEngine;
using System.Collections.Generic;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Grid grid;
    [SerializeField] private ObjectDataBase database;
    [Header("Grid Visuals")]
    [Tooltip("Trage aici toate obiectele gridVisualization din scenă")]
    [SerializeField] private List<MeshRenderer> gridVisualizations = new List<MeshRenderer>();
    [SerializeField] private PreviewSystem previewSystem;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private GameManager gameManager;

    [Header("Preview Materials")]
    [SerializeField] private Material wallPreviewMaterial;
    [SerializeField] private Material doorPreviewMaterial;

    [SerializeField] private ToolTipController toolTipController;

    private CameraController cameraController;
    private GridData floorData, furnitureData;
    private WallGridData wallData;
    private WallSegmentData segmentData;
    private DoorData doorData; // NOU: Tracking pentru uși
    private Vector3Int lastDetectedPosition = Vector3Int.zero;
    private IBuldingState buildingState;

    private bool isWallMode = false;
    private bool _isGridVisible = false;

    private void Start()
    {
        ToggleGridVisuals(false);
        floorData = new();
        furnitureData = new();
        wallData = new WallGridData();
        segmentData = new WallSegmentData(0.5f);
        doorData = new DoorData();

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Subscribe<ToggleExpansionModeEvent>(OnExpansionModeToggled);
            Debug.Log("[GRID] Subscribed la ToggleExpansionModeEvent cu succes!");
        }
        else
        {
            Debug.LogError("[GRID] ServiceLocator/EventBus NULL în Start!");
        }
    }


    private void OnDisable()
    {
        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            eventBus.Unsubscribe<ToggleExpansionModeEvent>(OnExpansionModeToggled);
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

    private void OnExpansionModeToggled(ToggleExpansionModeEvent e)
    {
        Debug.Log($"[GRID] OnExpansionModeToggled primit! IsActive={e.IsActive}");
        if (e.IsActive)
        {
            StopPlacement();
            playerInput.canInteract = true;
            ToggleGridVisuals(true);
        }
        else
        {
            ToggleGridVisuals(false);
        }
    }

    private void ToggleGridVisuals(bool isActive)
    {
        _isGridVisible = isActive;
        Debug.Log($"[GRID] ToggleGridVisuals({isActive}) — {gridVisualizations.Count} renderere în listă");

        foreach (MeshRenderer meshRenderer in gridVisualizations)
        {
            if (meshRenderer == null)
            {
                Debug.LogError("[GRID] MeshRenderer NULL în listă!");
                continue;
            }
            Debug.Log($"[GRID] Setez {meshRenderer.gameObject.name}.enabled = {isActive}");
            meshRenderer.enabled = isActive;
        }
    }

    public void AddGridVisual(GameObject newVisualParent)
    {
        if (newVisualParent == null) return;

        Transform visualChild = newVisualParent.transform.Find("gridVisualization");
        if (visualChild == null)
        {
            Debug.LogError($"[GRID] Nu am găsit 'gridVisualization' în {newVisualParent.name}!");
            return;
        }

        MeshRenderer meshRenderer = visualChild.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError($"[GRID] Nu am găsit MeshRenderer pe gridVisualization din {newVisualParent.name}!");
            return;
        }

        if (gridVisualizations.Contains(meshRenderer))
        {
            Debug.LogWarning($"[GRID] {newVisualParent.name} era deja în listă!");
            return;
        }

        gridVisualizations.Add(meshRenderer);
        meshRenderer.enabled = _isGridVisible;
        Debug.Log($"[GRID] AddGridVisual: {newVisualParent.name} adăugat | _isGridVisible={_isGridVisible} | enabled={meshRenderer.enabled}");
    }

    public void StartPlacement(int ID)
    {
        StopPlacement();
        ToggleGridVisuals(true);

        playerInput.canInteract = false;

        if (ID == 0 || ID == 5) // Podea
        {
            isWallMode = false;
            buildingState = new BoxPlacementState(
                ID, grid, previewSystem, database,
                floorData, objectPlacer, gameManager, toolTipController);

            playerInput.OnClick += PlaceStructure;
        }
        else if (ID == 1) // Perete
        {
            isWallMode = true;
            buildingState = new WallPlacementState(
                ID, grid, previewSystem, database,
                objectPlacer, gameManager, playerInput,
                wallData, segmentData,
                wallPreviewMaterial, toolTipController, floorData, cameraController);

            playerInput.OnClick += PlaceStructure;
            playerInput.OnRightClick += UndoWallSegment;
            playerInput.OnConfirm += FinalizeWall;
        }
        else if (database.objectsData.FindIndex(data => data.ID == ID) is int doorIdx
         && doorIdx >= 0
         && database.objectsData[doorIdx].IsDoor) // Ușă
        {
            isWallMode = false;
            buildingState = new DoorPlacementState(
                ID, grid, previewSystem, database,
                objectPlacer, gameManager,
                wallData, segmentData, doorData,
                doorPreviewMaterial, playerInput);

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

    private void UndoWallSegment()
    {
        if (buildingState is WallPlacementState wallState)
            wallState.UndoLastSegment();
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

    private void FinalizeWall()
    {
        if (buildingState is WallPlacementState wallState)
            wallState.ForceFinalize();
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

        playerInput.OnClick -= PlaceStructure;
        playerInput.OnRightClick -= UndoWallSegment;  // ← schimbat
        playerInput.OnExit -= StopPlacement;
        playerInput.OnRotate -= RotateStructure;
        playerInput.OnConfirm -= FinalizeWall;

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