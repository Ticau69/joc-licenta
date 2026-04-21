using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class WallPlacementState : IBuldingState
{
    private int selectedObjectIndex = -1;
    private int ID;
    private Grid grid;
    private PreviewSystem previewSystem;
    private ObjectDataBase dataBase;
    private ObjectPlacer objectPlacer;
    private GameManager gameManager;
    private PlayerInput playerInput;
    private WallGridData wallData;
    private WallSegmentData segmentData;
    private ToolTipController toolTip;
    private Material previewMaterialInstance;
    private bool lastValidState = false;

    // Sistema multi-segment
    private List<Vector3> wallPoints = new List<Vector3>();
    private List<GameObject> segmentPreviews = new List<GameObject>();
    private List<GameObject> cornerIndicators = new List<GameObject>();

    private GameObject currentSegmentPreview;
    private GameObject _snapIndicator;
    private const float CLOSE_LOOP_SNAP_RADIUS_MULTIPLIER = 3f;
    private readonly GridData floorData;
    private CameraController _cameraController;
    private bool isPlacingWall = false;
    private bool isFinalizingFromDoubleClick = false;

    // Prefabs și referințe
    private GameObject wallPrefab;
    private GameObject cornerIndicatorPrefab;

    public WallPlacementState(
        int iD,
        Grid grid,
        PreviewSystem previewSystem,
        ObjectDataBase database,
        ObjectPlacer objectPlacer,
        GameManager gameManager,
        PlayerInput input,
        WallGridData wallData,
        WallSegmentData segmentData,
        Material previewMaterial,
        ToolTipController toolTip,
        GridData floorData,
        CameraController cameraController
        ) // NOU
    {
        this.ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;
        this.playerInput = input;
        this.wallData = wallData;
        this.segmentData = segmentData;
        this.toolTip = toolTip;
        this.floorData = floorData;
        _cameraController = cameraController;

        previewMaterialInstance = new Material(previewMaterial);
        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);

        if (selectedObjectIndex > -1)
        {
            wallPrefab = database.objectsData[selectedObjectIndex].Prefab;
            previewSystem.ToggleCursorVisibility(false);
            CreateCornerIndicatorPrefab();
            CreateSnapIndicator();
        }
        else
        {
            throw new System.Exception($"Nu s-a găsit obiectul cu ID {iD}");
        }
    }

    private void CreateSnapIndicator()
    {
        _snapIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _snapIndicator.name = "SnapIndicator";
        _snapIndicator.transform.localScale = Vector3.one * 0.25f;

        // Dezactivăm collider-ul — e doar vizual
        GameObject.Destroy(_snapIndicator.GetComponent<Collider>());

        // Material emissive verde
        Renderer r = _snapIndicator.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.2f, 1f, 0.2f, 0.8f);
        r.material = mat;

        _snapIndicator.SetActive(false);
    }

    private void UpdateSnapIndicator(Vector3 mouseWorldPos)
    {
        if (_snapIndicator == null) return;
        if (!isPlacingWall || wallPoints.Count == 0)
        {
            _snapIndicator.SetActive(false);
            return;
        }

        float snapRadius = GetSnapRadius();

        // Verificăm snap la primul punct (închidere formă)
        if (wallPoints.Count >= 3)
        {
            float distToStart = Vector3.Distance(mouseWorldPos, wallPoints[0]);
            if (distToStart < snapRadius * CLOSE_LOOP_SNAP_RADIUS_MULTIPLIER)
            {
                _snapIndicator.SetActive(true);
                _snapIndicator.transform.position = wallPoints[0] + Vector3.up * 0.05f;
                SetSnapIndicatorColor(Color.cyan); // cyan = închide forma
                return;
            }
        }

        // Verificăm snap la colțuri existente
        foreach (Vector3 point in wallPoints)
        {
            if (Vector3.Distance(mouseWorldPos, point) < snapRadius)
            {
                _snapIndicator.SetActive(true);
                _snapIndicator.transform.position = point + Vector3.up * 0.05f;
                SetSnapIndicatorColor(Color.green); // verde = snap la colț
                return;
            }
        }

        _snapIndicator.SetActive(false);
    }

    private void SetSnapIndicatorColor(Color color)
    {
        if (_snapIndicator == null) return;
        _snapIndicator.GetComponent<Renderer>().material.color = color;
    }

    private float GetSnapRadius()
    {
        // Raza de snap crește proporțional cu înălțimea camerei
        Camera cam = Camera.main;
        if (cam == null) return 0.5f;

        float camHeight = cam.transform.position.y;
        float baseRadius = 0.5f;
        float maxRadius = 2.0f;

        // Normalizăm între minY și maxY din CameraController
        float minY = 6f;
        float maxY = 17f;
        float t = Mathf.InverseLerp(minY, maxY, camHeight);

        return Mathf.Lerp(baseRadius, maxRadius, t);
    }

    // ... (toate celelalte metode rămân la fel până la PlaceWallSegment) ...

    public void EndState()
    {
        if (toolTip != null)
            toolTip.HidePlacementInfo();

        if (previewMaterialInstance != null)
            GameObject.Destroy(previewMaterialInstance);
        if (_snapIndicator != null) GameObject.Destroy(_snapIndicator);

        CleanupAll();
        previewSystem.ToggleCursorVisibility(false);
    }

    public void OnAction(Vector3Int gridPosition)
    {
        Vector3 mousePos = playerInput.GetSelectedMapPostion();
        Vector3 snapPoint = SnapToGridCorner(mousePos);
        if (isFinalizingFromDoubleClick) return;
        if (!isPlacingWall)
        {
            StartNewWallChain(snapPoint);
        }
        else
        {
            if (wallPoints.Count >= 3 && Vector3.Distance(snapPoint, wallPoints[0]) < GetSnapRadius())
            {
                CloseWallLoop();
                return;
            }

            Vector3 lastPoint = wallPoints[wallPoints.Count - 1];
            if (Vector3.Distance(snapPoint, lastPoint) < 0.1f)
            {
                FinalizePlacement();
                return;
            }

            AddWallPoint(snapPoint);
        }
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector3 mousePos = GetMouseWorldPosition();
        Vector3 snapPoint = SnapToGridCorner(mousePos);

        UpdateSnapIndicator(mousePos);

        if (!isPlacingWall)
        {
            snapPoint = ClampToGrid(snapPoint);
            ShowHoverPreview(snapPoint);
            return;
        }

        // ── Guard: wallPoints nu trebuie să fie gol dacă isPlacingWall e true ──
        if (wallPoints.Count == 0)
        {
            isPlacingWall = false;
            return;
        }

        bool nearStart = wallPoints.Count >= 3 &&
                         Vector3.Distance(mousePos, wallPoints[0]) < GetSnapRadius();

        if (nearStart)
        {
            UpdateCurrentSegmentPreview(wallPoints[0]);
        }
        else
        {
            Vector3 clampedMouse = ClampToGrid(snapPoint);
            Vector3 snappedMousePos = SnapWallToGridAxis(
                wallPoints[wallPoints.Count - 1], clampedMouse);

            Bounds b = GetGridBounds();
            Debug.Log($"Grid bounds: min={b.min} max={b.max} | mousePos={snapPoint} | clamped={ClampToGrid(snapPoint)}");

            UpdateCurrentSegmentPreview(snappedMousePos);
        }
    }

    /// <summary>
    /// Clampează o poziție la bounds-ul real al podelei.
    /// </summary>
    private Vector3 ClampToGrid(Vector3 pos)
    {
        Bounds b = GetGridBounds();
        return new Vector3(
            Mathf.Clamp(pos.x, b.min.x, b.max.x),
            pos.y,
            Mathf.Clamp(pos.z, b.min.z, b.max.z)
        );
    }


    private void StartNewWallChain(Vector3 startPoint)
    {
        wallPoints.Clear();
        wallPoints.Add(startPoint);
        isPlacingWall = true;
        CreateCornerIndicator(startPoint, Color.green);
        Debug.Log($"Început plasare pereți la: {startPoint}");
    }

    private void AddWallPoint(Vector3 newPoint)
    {
        Vector3 lastPoint = wallPoints[wallPoints.Count - 1];
        newPoint = SnapWallToGridAxis(lastPoint, newPoint);

        if (!ValidateSegment(lastPoint, newPoint))
        {
            Debug.Log("Segment invalid - există deja perete sau nu ai destui bani!");
            return;
        }

        wallPoints.Add(newPoint);
        CreateCornerIndicator(newPoint, Color.yellow);
        CreateSegmentPreview(lastPoint, newPoint, true);
        Debug.Log($"Adăugat punct: {newPoint} (Total: {wallPoints.Count} puncte)");
    }

    private void CloseWallLoop()
    {
        Vector3 lastPoint = wallPoints[wallPoints.Count - 1];
        Vector3 firstPoint = wallPoints[0];

        if (ValidateSegment(lastPoint, firstPoint))
        {
            wallPoints.Add(firstPoint);
            FinalizePlacement();
        }
        else
        {
            Debug.Log("Nu se poate închide forma - segment invalid!");
        }
    }

    private void FinalizePlacement()
    {
        if (wallPoints.Count < 2)
        {
            Debug.Log("Ai nevoie de cel puțin 2 puncte!");
            CancelPlacement();
            return;
        }

        float cellSize = grid.cellSize.x;
        int totalCost = 0;

        for (int i = 0; i < wallPoints.Count - 1; i++)
        {
            float length = Vector3.Distance(wallPoints[i], wallPoints[i + 1]);
            int cellCount = Mathf.CeilToInt(length / cellSize);
            totalCost += cellCount * dataBase.objectsData[selectedObjectIndex].Cost;
        }

        if (gameManager.CurrentMoney < totalCost)
        {
            Debug.Log($"Nu ai destui bani! Cost: {totalCost}RON");
            CancelPlacement();
            return;
        }

        int placedCount = 0;
        for (int i = 0; i < wallPoints.Count - 1; i++)
        {
            Vector3 start = wallPoints[i];
            Vector3 end = wallPoints[i + 1];

            if (PlaceWallSegment(start, end))
            {
                placedCount++;
            }
        }

        Debug.Log($"Formă plasată! Segmente: {placedCount}/{wallPoints.Count - 1}");
        ResetToNewPlacement();
    }

    public void UndoLastSegment()
    {
        // Nu putem undo primul punct — anulăm tot
        if (wallPoints.Count <= 1)
        {
            CancelPlacement();
            return;
        }

        // Scoatem ultimul punct
        wallPoints.RemoveAt(wallPoints.Count - 1);

        // Distrugem ultimul segment preview confirmat
        if (segmentPreviews.Count > 0)
        {
            int lastIdx = segmentPreviews.Count - 1;
            if (segmentPreviews[lastIdx] != null)
                GameObject.Destroy(segmentPreviews[lastIdx]);
            segmentPreviews.RemoveAt(lastIdx);
        }

        // Distrugem ultimul corner indicator
        // (primul indicator e start-ul — îl păstrăm)
        if (cornerIndicators.Count > 1)
        {
            int lastIdx = cornerIndicators.Count - 1;
            if (cornerIndicators[lastIdx] != null)
                GameObject.Destroy(cornerIndicators[lastIdx]);
            cornerIndicators.RemoveAt(lastIdx);
        }

        Debug.Log($"[Wall] Undo — puncte rămase: {wallPoints.Count}");
    }

    /// <summary>
    /// ACTUALIZAT: Plasează un segment individual folosind sistemul cu segmente
    /// </summary>
    private bool PlaceWallSegment(Vector3 start, Vector3 end)
    {
        if (!wallData.CanPlaceWall(start, end))
            return false;

        float length = Vector3.Distance(start, end);
        float cellSize = grid.cellSize.x;
        int cellCount = Mathf.CeilToInt(length / cellSize);
        int totalCost = cellCount * dataBase.objectsData[selectedObjectIndex].Cost;

        if (!gameManager.TrySpendMoney(totalCost))
            return false;
        if (FinanceManager.Instance != null)
        {
            FinanceManager.Instance.RegisterTransaction(TransactionCategory.Constructii_Teren, totalCost);
        }

        // NOU: Folosim sistemul cu segmente în loc să creăm un singur perete
        if (segmentData != null)
        {
            // Obținem materialul peretelui
            Material wallMaterial = GetWallMaterial();

            // Adăugăm peretele ca segmente
            segmentData.AddWall(start, end, ID, wallPrefab, wallMaterial);

            // Adăugăm și în wallData pentru tracking general (fără GameObject)
            wallData.AddWall(start, end, ID, null);

            Debug.Log($"Perete segmentat creat: {start} -> {end}");
        }
        else
        {
            // Fallback la sistemul vechi dacă segmentData nu există
            GameObject newWall = GameObject.Instantiate(wallPrefab);
            newWall.name = $"Wall_Segment_{wallData.GetAllWalls().Count}";

            ProceduralWall pWall = newWall.GetComponent<ProceduralWall>();
            if (pWall != null)
            {
                pWall.GenerateWall(start, end);
                wallData.AddWall(start, end, ID, newWall);
            }
        }

        // Consumăm energie
        int consumption = dataBase.objectsData[selectedObjectIndex].PowerConsumption;
        if (consumption > 0 && PowerManager.Instance != null)
        {
            PowerManager.Instance.RegisterConsumer(consumption);
        }

        return true;
    }

    /// <summary>
    /// NOU: Obține materialul peretelui din prefab
    /// </summary>
    private Material GetWallMaterial()
    {
        if (wallPrefab != null)
        {
            ProceduralWall pw = wallPrefab.GetComponent<ProceduralWall>();
            if (pw != null)
            {
                return pw.GetMaterial();
            }

            // Fallback: încearcă să iei primul material găsit
            MeshRenderer renderer = wallPrefab.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                return renderer.sharedMaterial;
            }
        }

        Debug.LogWarning("Nu s-a găsit materialul peretelui! Folosind material default.");
        return null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return playerInput.GetSelectedMapPostion();

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);

            // ── NOU: Clampăm la limitele gridului ────────────────────────
            Bounds gridBounds = GetGridBounds();
            hitPoint.x = Mathf.Clamp(hitPoint.x, gridBounds.min.x, gridBounds.max.x);
            hitPoint.z = Mathf.Clamp(hitPoint.z, gridBounds.min.z, gridBounds.max.z);
            // ─────────────────────────────────────────────────────────────

            float cellSize = grid.cellSize.x;
            Vector3 gridOrigin = grid.transform.position;

            float snappedX = Mathf.Round((hitPoint.x - gridOrigin.x) / cellSize)
                             * cellSize + gridOrigin.x;
            float snappedZ = Mathf.Round((hitPoint.z - gridOrigin.z) / cellSize)
                             * cellSize + gridOrigin.z;

            return new Vector3(snappedX, 0f, snappedZ);
        }

        return playerInput.GetSelectedMapPostion();
    }

    private Bounds GetGridBounds()
    {
        // Căutăm CameraController lazy (nu în constructor)
        if (_cameraController == null)
            _cameraController = GameObject.FindFirstObjectByType<CameraController>();

        if (_cameraController != null && _cameraController.mapRenderers.Count > 0)
        {
            Bounds combined = _cameraController.mapRenderers[0].bounds;
            for (int i = 1; i < _cameraController.mapRenderers.Count; i++)
            {
                if (_cameraController.mapRenderers[i] != null)
                    combined.Encapsulate(_cameraController.mapRenderers[i].bounds);
            }
            return combined;
        }

        return new Bounds(grid.transform.position, Vector3.one * 100f);
    }

    // ===== METODELE RĂMASE SUNT IDENTICE CU VERSIUNEA ORIGINALĂ =====

    private void CancelPlacement()
    {
        ResetToNewPlacement();
    }

    private void ResetToNewPlacement()
    {
        CleanupPreviews();
        wallPoints.Clear();
        isPlacingWall = false;
    }

    private void CleanupPreviews()
    {
        foreach (var preview in segmentPreviews)
        {
            if (preview != null)
                GameObject.Destroy(preview);
        }
        segmentPreviews.Clear();

        foreach (var indicator in cornerIndicators)
        {
            if (indicator != null)
                GameObject.Destroy(indicator);
        }
        cornerIndicators.Clear();

        if (currentSegmentPreview != null)
        {
            GameObject.Destroy(currentSegmentPreview);
            currentSegmentPreview = null;
        }
    }

    public void ForceFinalize()
    {
        isFinalizingFromDoubleClick = true;
        FinalizePlacement();
        isFinalizingFromDoubleClick = false;
    }

    private void CleanupAll()
    {
        CleanupPreviews();
        wallPoints.Clear();
        isPlacingWall = false;
    }

    private void ShowHoverPreview(Vector3 snapPoint)
    {
        if (currentSegmentPreview == null)
        {
            currentSegmentPreview = GameObject.Instantiate(wallPrefab);
            currentSegmentPreview.name = "HoverPreview";
            DisableColliders(currentSegmentPreview);
        }

        Vector3 endPoint = snapPoint + new Vector3(0.01f, 0, 0);

        ProceduralWall pWall = currentSegmentPreview.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            pWall.GenerateWall(snapPoint, endPoint);
            bool hasEnoughMoney = gameManager.CurrentMoney >= dataBase.objectsData[selectedObjectIndex].Cost;
            ApplyPreviewMaterial(currentSegmentPreview, hasEnoughMoney, 0.3f);
        }

        if (toolTip != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            int costPerCell = dataBase.objectsData[selectedObjectIndex].Cost;

            string info = "Perete\n" +
                          $"Cost / celula: {costPerCell} RON\n" +
                          "──────────────────\n" +
                          "[Click] Incepe constructia";

            toolTip.ShowPlacementInfo(info, mouseScreenPos);
        }
    }

    private void UpdateCurrentSegmentPreview(Vector3 currentMousePos)
    {
        if (wallPoints.Count == 0) return;

        Vector3 lastPoint = wallPoints[wallPoints.Count - 1];

        bool nearStart = wallPoints.Count >= 3 && Vector3.Distance(currentMousePos, wallPoints[0]) < 0.5f;
        if (nearStart)
        {
            currentMousePos = wallPoints[0];
        }

        float distance = Vector3.Distance(lastPoint, currentMousePos);
        if (distance < 0.01f)
        {
            if (currentSegmentPreview != null)
                currentSegmentPreview.SetActive(false);

            toolTip?.HidePlacementInfo();
            return;
        }

        if (currentSegmentPreview == null)
        {
            currentSegmentPreview = GameObject.Instantiate(wallPrefab);
            currentSegmentPreview.name = "CurrentSegmentPreview";
            DisableColliders(currentSegmentPreview);
        }

        currentSegmentPreview.SetActive(true);

        ProceduralWall pWall = currentSegmentPreview.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            pWall.GenerateWall(lastPoint, currentMousePos);
            bool isValid = ValidateSegment(lastPoint, currentMousePos);
            float alpha = nearStart ? 0.7f : 0.5f;
            ApplyPreviewMaterial(currentSegmentPreview, isValid, alpha);
        }

        if (nearStart && cornerIndicators.Count > 0)
        {
            UpdateCornerIndicatorColor(cornerIndicators[0], Color.cyan);
        }
        else if (cornerIndicators.Count > 0)
        {
            UpdateCornerIndicatorColor(cornerIndicators[0], Color.green);
        }
        UpdateCostTooltip(lastPoint, currentMousePos);
    }

    private void UpdateCostTooltip(Vector3 from, Vector3 to)
    {
        if (toolTip == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        float cellSize = grid.cellSize.x;
        int costPerCell = dataBase.objectsData[selectedObjectIndex].Cost;

        // ── Celule și cost pentru segmentele deja confirmate ─────────────────────
        int confirmedCells = 0;
        for (int i = 0; i < wallPoints.Count - 1; i++)
        {
            float segLen = Vector3.Distance(wallPoints[i], wallPoints[i + 1]);
            confirmedCells += Mathf.CeilToInt(segLen / cellSize);
        }
        int confirmedCost = confirmedCells * costPerCell;

        // ── Celule și cost pentru segmentul curent (până la cursor) ──────────────
        int currentCells = Mathf.CeilToInt(Vector3.Distance(from, to) / cellSize);
        int currentCost = currentCells * costPerCell;

        // ── Total ─────────────────────────────────────────────────────────────────
        int totalCells = confirmedCells + currentCells;
        int totalCost = confirmedCells * costPerCell + currentCost;
        bool canAfford = gameManager.CurrentMoney >= totalCost;

        // ── Număr puncte confirmate (colțuri) ─────────────────────────────────────
        int confirmedPoints = Mathf.Max(0, wallPoints.Count - 1); // exclude punctul curent

        // ── Build string ──────────────────────────────────────────────────────────
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("Perete in constructie");
        sb.AppendLine("──────────────────");

        // Segment curent
        sb.AppendLine($"Segment curent:  {currentCells,3} celule  •  {currentCost,6} RON");

        // Confirmate — afișăm doar dacă există
        if (confirmedCells > 0)
            sb.AppendLine($"Confirmate:      {confirmedCells,3} celule  •  {confirmedCost,6} RON");

        // Total
        sb.AppendLine("──────────────────");
        sb.AppendLine($"Total:           {totalCells,3} celule  •  {totalCost,6} RON");

        // Fonduri insuficiente
        if (!canAfford)
            sb.AppendLine("<color=red>Fonduri insuficiente!</color>");

        sb.AppendLine("──────────────────");

        // Puncte confirmate
        if (confirmedPoints > 0)
            sb.AppendLine($"Colturi plasate: {confirmedPoints}");

        // Instrucțiuni contextuale
        sb.AppendLine("[Click] Adauga punct");
        if (confirmedPoints > 0)
            sb.AppendLine("[Click Dreapta] Undo");
        if (wallPoints.Count >= 3)
            sb.AppendLine("[Q] Finalizeaza");

        toolTip.ShowPlacementInfo(sb.ToString(), mouseScreenPos);
    }

    private void CreateSegmentPreview(Vector3 start, Vector3 end, bool isConfirmed)
    {
        GameObject preview = GameObject.Instantiate(wallPrefab);
        preview.name = $"SegmentPreview_{segmentPreviews.Count}";
        DisableColliders(preview);

        ProceduralWall pWall = preview.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            pWall.GenerateWall(start, end);
            ApplyPreviewMaterial(preview, true, isConfirmed ? 0.6f : 0.4f);
        }

        segmentPreviews.Add(preview);
    }

    private void DisableColliders(GameObject obj)
    {
        MeshCollider[] colliders = obj.GetComponentsInChildren<MeshCollider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    private void CreateCornerIndicator(Vector3 position, Color color)
    {
        if (cornerIndicatorPrefab == null) return;

        GameObject indicator = GameObject.Instantiate(cornerIndicatorPrefab);
        float postHeight = 2.5f;
        indicator.transform.position = position + Vector3.up * (postHeight / 2f);
        indicator.name = $"CornerIndicator_{cornerIndicators.Count}";

        Renderer renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color semiTransparent = color;
            semiTransparent.a = 0.6f;
            renderer.material.color = semiTransparent;
        }

        cornerIndicators.Add(indicator);
    }

    private void UpdateCornerIndicatorColor(GameObject indicator, Color color)
    {
        if (indicator == null) return;

        Renderer renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    private void CreateCornerIndicatorPrefab()
    {
        cornerIndicatorPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cornerIndicatorPrefab.transform.localScale = Vector3.one * 0.2f;

        // Folosim direct materialul valid ca bază
        Renderer renderer = cornerIndicatorPrefab.GetComponent<Renderer>();
        renderer.sharedMaterial = previewMaterialInstance;

        GameObject.Destroy(cornerIndicatorPrefab.GetComponent<Collider>());
        cornerIndicatorPrefab.SetActive(false);
    }

    private Vector3 SnapToGridCorner(Vector3 worldPos)
    {
        float snapRadius = GetSnapRadius();

        // Snap la puncte existente din wallPoints (colțuri deja plasate)
        foreach (Vector3 existingPoint in wallPoints)
        {
            if (Vector3.Distance(worldPos, existingPoint) < snapRadius)
                return existingPoint; // snap exact pe punct
        }

        // Snap standard la grid
        float cellSize = grid.cellSize.x;
        Vector3 gridOrigin = grid.transform.position;

        float snappedX = Mathf.Round((worldPos.x - gridOrigin.x) / cellSize) * cellSize + gridOrigin.x;
        float snappedZ = Mathf.Round((worldPos.z - gridOrigin.z) / cellSize) * cellSize + gridOrigin.z;

        return new Vector3(snappedX, 0, snappedZ);
    }

    private Vector3 SnapWallToGridAxis(Vector3 startPoint, Vector3 endPoint)
    {
        float deltaX = Mathf.Abs(endPoint.x - startPoint.x);
        float deltaZ = Mathf.Abs(endPoint.z - startPoint.z);

        if (deltaX > deltaZ)
        {
            return new Vector3(endPoint.x, startPoint.y, startPoint.z);
        }
        else
        {
            return new Vector3(startPoint.x, startPoint.y, endPoint.z);
        }
    }

    private bool ValidateSegment(Vector3 start, Vector3 end)
    {
        float distance = Vector3.Distance(start, end);
        if (distance < 0.1f) return false;
        if (!wallData.CanPlaceWall(start, end)) return false;

        float cellSize = grid.cellSize.x;
        int cellCount = Mathf.CeilToInt(distance / cellSize);
        int cost = cellCount * dataBase.objectsData[selectedObjectIndex].Cost;
        if (gameManager.CurrentMoney < cost) return false;

        return true;
    }

    private void ApplyPreviewMaterial(GameObject obj, bool isValid, float alpha = 0.5f)
    {
        Color color = isValid ? Color.white : Color.red;
        color.a = alpha;

        // Actualizăm culoarea doar dacă s-a schimbat starea
        if (isValid != lastValidState)
        {
            lastValidState = isValid;
            previewMaterialInstance.color = color;
        }

        // Aplicăm ÎNTOTDEAUNA materialul pe obiect (poate fi un obiect nou)
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.sharedMaterial = previewMaterialInstance;
    }
}