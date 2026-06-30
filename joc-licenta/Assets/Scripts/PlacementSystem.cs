using UnityEngine;
using System.Collections.Generic;
using System;

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
    [SerializeField] private Material boxPreviewMaterial;

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

        bool positionChanged = lastDetectedPosition != gridPosition;

        if (positionChanged || isWallMode || buildingState is RemovingState)
        {
            buildingState.UpdateState(gridPosition);
            lastDetectedPosition = gridPosition;
        }

        // --- NOU: Verificăm dacă suntem în modul de ștergere ---
        // bool isRemoving = buildingState is RemovingState;

        // // Dacă suntem pe un grid nou SAU e modul de perete SAU e modul de ștergere, dăm update!
        // if (lastDetectedPosition != gridPosition || isWallMode || isRemoving)
        // {
        //     buildingState.UpdateState(gridPosition);
        //     lastDetectedPosition = gridPosition;
        // }
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
                doorPreviewMaterial, boxPreviewMaterial, playerInput);

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

    // ==========================================
    // NOU: Sistemul de generare JSON pentru salvare
    // ==========================================
    public string GenerateShopLayoutJson()
    {
        ShopSaveState saveState = new ShopSaveState();

        if (floorData != null)
        {
            List<PlacementData> uniqueFloors = floorData.GetAllUniqueObjects();
            foreach (var floor in uniqueFloors)
            {
                saveState.Floors.Add(new GridObjectSaveData
                {
                    ID = floor.ID,
                    AnchorPosition = floor.occupiedPositions[0],
                    Rotation = floor.Rotation
                });
            }
        }

        if (furnitureData != null)
        {
            List<PlacementData> uniqueFurniture = furnitureData.GetAllUniqueObjects();
            foreach (var furn in uniqueFurniture)
            {
                saveState.Furniture.Add(new GridObjectSaveData
                {
                    ID = furn.ID,
                    AnchorPosition = furn.occupiedPositions[0],
                    Rotation = furn.Rotation
                });
            }
        }

        if (wallData != null)
        {
            List<WallData> allWalls = wallData.GetAllWalls();
            foreach (var wall in allWalls)
            {
                saveState.Walls.Add(new WallSaveData
                {
                    ID = wall.ID,
                    StartPos = wall.StartPosition,
                    EndPos = wall.EndPosition
                });
            }
        }

        return JsonUtility.ToJson(saveState);
    }

    // =========================================================================
    // VERSIONATĂ CU LOGURI: Reconstrucția fizică a magazinului din JSON
    // =========================================================================
    public void ReconstructShop(string json)
    {
        Debug.Log("--- [LOAD PASUL 1] Începe reconstrucția magazinului în PlacementSystem ---");

        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            Debug.LogWarning("[LOAD] Abort: JSON-ul primit este gol sau invalid!");
            return;
        }

        try
        {
            // Convertim textul înapoi în obiect structural C#
            ShopSaveState saveState = JsonUtility.FromJson<ShopSaveState>(json);

            Debug.Log($"--- [LOAD PASUL 2] Deserializare reușită! Obiecte găsite în salvare -> Podele: {saveState.Floors.Count}, Mobilier: {saveState.Furniture.Count}, Pereți: {saveState.Walls.Count} ---");

            // 1. RECONSTRUCȚIE PODELE
            Debug.Log("[LOAD PASUL 3] Se inițiază plasarea podelelor...");
            foreach (var floor in saveState.Floors)
            {
                try
                {
                    int dbIndex = database.objectsData.FindIndex(d => d.ID == floor.ID);
                    if (dbIndex < 0) continue;

                    Vector2Int size = database.objectsData[dbIndex].Size;
                    Vector3 worldPosition = grid.CellToWorld(floor.AnchorPosition);
                    Vector3 centeredPosition = new Vector3(
                        worldPosition.x + (size.x / 2f),
                        worldPosition.y,
                        worldPosition.z + (size.y / 2f)
                    );

                    int index = objectPlacer.PlaceObject(database.objectsData[dbIndex].Prefab, centeredPosition, floor.Rotation, false);
                    floorData.AddObjectAt(floor.AnchorPosition, size, floor.ID, index, floor.Rotation);
                }
                catch (System.Exception ex)
                {
                    // Dacă o singură podea dă eroare (ex: e deja ocupat locul), jocul nu mai dă crash, ci trece la următoarea podea!
                    Debug.LogError($"[LOAD EROARE PODEA] Nu s-a putut plasa podeaua ID {floor.ID} la poziția {floor.AnchorPosition}. Mesaj: {ex.Message}");
                }
            }

            // 2. RECONSTRUCȚIE MOBILIER / RAFTURI
            Debug.Log("[LOAD PASUL 4] Se inițiază plasarea mobilierului...");
            foreach (var furn in saveState.Furniture)
            {
                try
                {
                    int dbIndex = database.objectsData.FindIndex(d => d.ID == furn.ID);
                    if (dbIndex < 0) continue;

                    Vector2Int size = database.objectsData[dbIndex].Size;
                    Vector3 worldPosition = grid.CellToWorld(furn.AnchorPosition);
                    Vector3 centeredPosition = new Vector3(
                        worldPosition.x + (size.x / 2f),
                        worldPosition.y,
                        worldPosition.z + (size.y / 2f)
                    );

                    int index = objectPlacer.PlaceObject(database.objectsData[dbIndex].Prefab, centeredPosition, furn.Rotation, true);
                    furnitureData.AddObjectAt(furn.AnchorPosition, size, furn.ID, index, furn.Rotation);

                    GameObject placedObj = objectPlacer.GetPlacedObject(index);
                    if (placedObj != null)
                    {
                        WorkStation ws = placedObj.GetComponentInChildren<WorkStation>();
                        if (ws != null)
                        {
                            ws.shelfVariant = database.objectsData[dbIndex].ShelfVariant;
                            ws.stationType = database.objectsData[dbIndex].StationType;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LOAD EROARE MOBILIER] Nu s-a putut plasa mobilierul ID {furn.ID} la poziția {furn.AnchorPosition}. Mesaj: {ex.Message}");
                }
            }

            // 3. RECONSTRUCȚIE PEREȚI PROCEDURALI
            Debug.Log("[LOAD PASUL 5] Se inițiază plasarea pereților...");
            foreach (var wall in saveState.Walls)
            {
                try
                {
                    int dbIndex = database.objectsData.FindIndex(d => d.ID == wall.ID);
                    if (dbIndex < 0) continue;

                    GameObject wallPrefab = database.objectsData[dbIndex].Prefab;
                    Material wallMat = null;
                    var pwPrefab = wallPrefab.GetComponent<ProceduralWall>();
                    if (pwPrefab != null) wallMat = pwPrefab.GetMaterial();

                    segmentData.AddWall(wall.StartPos, wall.EndPos, wall.ID, wallPrefab, wallMat);
                    wallData.AddWall(wall.StartPos, wall.EndPos, wall.ID, null);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LOAD EROARE PERETE] Nu s-a putut genera segmentul de perete de la {wall.StartPos} la {wall.EndPos}. Mesaj: {ex.Message}");
                }
            }

            if (EmployeeManager.Instance != null)
            {
                EmployeeManager.Instance.RefreshStations();
            }

            Debug.Log("--- [LOAD FINISHED SUCCESS] Toate elementele salvate au fost procesate! ---");
        }
        catch (System.Exception bigEx)
        {
            Debug.LogError($"[LOAD EROARE CRITICĂ GENERALĂ] Structura funcției a crăpat complet: {bigEx.Message}");
        }
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

        buildingState.OnAction(lastDetectedPosition);

        if (!(buildingState is RemovingState))
        {
            if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            {
                eventBus.Publish(new ScoreGainedEvent { Amount = 10, Source = "Obiect Construit" });
                Debug.Log("[GRID] Event de 10 puncte a fost trimis!");
            }
        }
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