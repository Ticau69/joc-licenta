using UnityEngine;
using System.Collections.Generic;

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
    private WallSegmentData segmentData; // NOU: Sistemul cu segmente

    // Sistema multi-segment
    private List<Vector3> wallPoints = new List<Vector3>();
    private List<GameObject> segmentPreviews = new List<GameObject>();
    private List<GameObject> cornerIndicators = new List<GameObject>();

    private GameObject currentSegmentPreview;
    private bool isPlacingWall = false;

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
        WallSegmentData segmentData) // NOU
    {
        this.ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;
        this.playerInput = input;
        this.wallData = wallData;
        this.segmentData = segmentData; // NOU

        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);

        if (selectedObjectIndex > -1)
        {
            wallPrefab = database.objectsData[selectedObjectIndex].Prefab;
            previewSystem.ToggleCursorVisibility(false);
            CreateCornerIndicatorPrefab();
        }
        else
        {
            throw new System.Exception($"Nu s-a găsit obiectul cu ID {iD}");
        }
    }

    // ... (toate celelalte metode rămân la fel până la PlaceWallSegment) ...

    public void EndState()
    {
        CleanupAll();
        previewSystem.ToggleCursorVisibility(false);
    }

    public void OnAction(Vector3Int gridPosition)
    {
        Vector3 mousePos = playerInput.GetSelectedMapPostion();
        Vector3 snapPoint = SnapToGridCorner(mousePos);

        if (!isPlacingWall)
        {
            StartNewWallChain(snapPoint);
        }
        else
        {
            if (wallPoints.Count >= 3 && Vector3.Distance(snapPoint, wallPoints[0]) < 0.2f)
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
        Vector3 mousePos = playerInput.GetSelectedMapPostion();
        Vector3 snapPoint = SnapToGridCorner(mousePos);

        if (!isPlacingWall)
        {
            ShowHoverPreview(snapPoint);
        }
        else
        {
            Vector3 snappedMousePos = SnapWallToGridAxis(wallPoints[wallPoints.Count - 1], snapPoint);
            UpdateCurrentSegmentPreview(snappedMousePos);
        }
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

        int totalCost = (wallPoints.Count - 1) * dataBase.objectsData[selectedObjectIndex].Cost;

        if (gameManager.CurrentMoney < totalCost)
        {
            Debug.Log($"Nu ai destui bani! Cost: {totalCost}");
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

    /// <summary>
    /// ACTUALIZAT: Plasează un segment individual folosind sistemul cu segmente
    /// </summary>
    private bool PlaceWallSegment(Vector3 start, Vector3 end)
    {
        if (!wallData.CanPlaceWall(start, end))
            return false;

        if (!gameManager.TrySpendMoney(dataBase.objectsData[selectedObjectIndex].Cost))
            return false;

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
            {
                currentSegmentPreview.SetActive(false);
            }
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

        Renderer renderer = cornerIndicatorPrefab.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        renderer.material = mat;

        GameObject.Destroy(cornerIndicatorPrefab.GetComponent<Collider>());
        cornerIndicatorPrefab.SetActive(false);
    }

    private Vector3 SnapToGridCorner(Vector3 worldPos)
    {
        float cellSize = grid.cellSize.x;
        float snappedX = Mathf.Round(worldPos.x / cellSize) * cellSize;
        float snappedZ = Mathf.Round(worldPos.z / cellSize) * cellSize;
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
        if (gameManager.CurrentMoney < dataBase.objectsData[selectedObjectIndex].Cost) return false;
        return true;
    }

    private void ApplyPreviewMaterial(GameObject wallObj, bool isValid, float alpha = 0.5f)
    {
        ProceduralWall pWall = wallObj.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            Material currentMat = pWall.GetMaterial();
            if (currentMat != null)
            {
                Material previewMat = new Material(currentMat);
                Color color = isValid ? Color.white : Color.red;
                color.a = alpha;

                if (previewMat.HasProperty("_Color"))
                {
                    previewMat.color = color;
                }

                previewMat.SetFloat("_Mode", 3);
                previewMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                previewMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                previewMat.SetInt("_ZWrite", 0);
                previewMat.DisableKeyword("_ALPHATEST_ON");
                previewMat.EnableKeyword("_ALPHABLEND_ON");
                previewMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                previewMat.renderQueue = 3000;

                pWall.SetMaterial(previewMat);
            }
        }
    }
}