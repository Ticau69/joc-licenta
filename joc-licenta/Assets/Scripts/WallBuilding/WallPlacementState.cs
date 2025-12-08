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

    // Sistema multi-segment
    private List<Vector3> wallPoints = new List<Vector3>(); // Toate punctele formei
    private List<GameObject> segmentPreviews = new List<GameObject>(); // Preview pentru fiecare segment
    private List<GameObject> cornerIndicators = new List<GameObject>(); // Indicatori pentru colțuri

    private GameObject currentSegmentPreview; // Preview-ul segmentului curent (mouse -> ultimul punct)
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
        WallGridData wallData)
    {
        this.ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;
        this.playerInput = input;
        this.wallData = wallData;

        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);

        if (selectedObjectIndex > -1)
        {
            wallPrefab = database.objectsData[selectedObjectIndex].Prefab;

            // Ascundem cursor-ul și preview-ul standard
            previewSystem.ToggleCursorVisibility(false);

            // Creăm prefab pentru corner indicator
            CreateCornerIndicatorPrefab();
        }
        else
        {
            throw new System.Exception($"Nu s-a găsit obiectul cu ID {iD}");
        }
    }

    public void EndState()
    {
        CleanupAll();
        previewSystem.ToggleCursorVisibility(true);
    }

    public void OnAction(Vector3Int gridPosition)
    {
        Vector3 mousePos = playerInput.GetSelectedMapPostion();
        Vector3 snapPoint = SnapToGridCorner(mousePos);

        if (!isPlacingWall)
        {
            // PRIMUL CLICK: Începem forma
            StartNewWallChain(snapPoint);
        }
        else
        {
            // CLICK-URI ULTERIOARE: Adăugăm colțuri

            // Verificăm dacă am dat click pe primul punct (închidere formă)
            if (wallPoints.Count >= 3 && Vector3.Distance(snapPoint, wallPoints[0]) < 0.2f)
            {
                // Închidem forma (connect back to start)
                CloseWallLoop();
                return;
            }

            // Verificăm dacă punctul e prea aproape de ultimul (finalizare fără închidere)
            Vector3 lastPoint = wallPoints[wallPoints.Count - 1];
            if (Vector3.Distance(snapPoint, lastPoint) < 0.1f)
            {
                // Double-click sau click pe același loc = FINALIZARE
                FinalizePlacement();
                return;
            }

            // Adăugăm un nou punct
            AddWallPoint(snapPoint);
        }
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector3 mousePos = playerInput.GetSelectedMapPostion();
        Vector3 snapPoint = SnapToGridCorner(mousePos);

        if (!isPlacingWall)
        {
            // Modul HOVER: arătăm un mic preview de unde ar începe
            ShowHoverPreview(snapPoint);
        }
        else
        {
            // Modul DRAG: arătăm segmentul curent de la ultimul punct la mouse
            // IMPORTANT: Aplicăm snap la mouse pentru preview fluid
            Vector3 snappedMousePos = SnapWallToGridAxis(wallPoints[wallPoints.Count - 1], snapPoint);
            UpdateCurrentSegmentPreview(snappedMousePos);
        }
    }

    /// <summary>
    /// Începe o nouă secvență de pereți
    /// </summary>
    private void StartNewWallChain(Vector3 startPoint)
    {
        wallPoints.Clear();
        wallPoints.Add(startPoint);
        isPlacingWall = true;

        // Creăm indicator pentru primul punct
        CreateCornerIndicator(startPoint, Color.green);

        Debug.Log($"Început plasare pereți la: {startPoint}");
    }

    /// <summary>
    /// Adaugă un nou punct în lanț
    /// </summary>
    private void AddWallPoint(Vector3 newPoint)
    {
        Vector3 lastPoint = wallPoints[wallPoints.Count - 1];

        // SNAP: Forțăm peretele să fie pe axa grid
        newPoint = SnapWallToGridAxis(lastPoint, newPoint);

        // Validare: verificăm dacă segmentul poate fi plasat
        if (!ValidateSegment(lastPoint, newPoint))
        {
            Debug.Log("Segment invalid - există deja perete sau nu ai destui bani!");
            return;
        }

        // Adăugăm punctul
        wallPoints.Add(newPoint);

        // Creăm indicator pentru colț
        CreateCornerIndicator(newPoint, Color.yellow);

        // Creăm preview permanent pentru segmentul confirmat
        CreateSegmentPreview(lastPoint, newPoint, true);

        Debug.Log($"Adăugat punct: {newPoint} (Total: {wallPoints.Count} puncte)");
    }

    /// <summary>
    /// Închide forma conectând ultimul punct cu primul
    /// </summary>
    private void CloseWallLoop()
    {
        Vector3 lastPoint = wallPoints[wallPoints.Count - 1];
        Vector3 firstPoint = wallPoints[0];

        if (ValidateSegment(lastPoint, firstPoint))
        {
            wallPoints.Add(firstPoint); // Adăugăm primul punct la final
            FinalizePlacement();
        }
        else
        {
            Debug.Log("Nu se poate închide forma - segment invalid!");
        }
    }

    /// <summary>
    /// Finalizează plasarea și construiește toți pereții
    /// </summary>
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

        // Plasăm fiecare segment
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

        // Resetăm pentru o nouă formă
        ResetToNewPlacement();
    }

    /// <summary>
    /// Plasează un segment individual de perete
    /// </summary>
    private bool PlaceWallSegment(Vector3 start, Vector3 end)
    {
        if (!wallData.CanPlaceWall(start, end))
            return false;

        if (!gameManager.TrySpendMoney(dataBase.objectsData[selectedObjectIndex].Cost))
            return false;

        // Creăm peretele
        GameObject newWall = GameObject.Instantiate(wallPrefab);
        newWall.name = $"Wall_Segment_{wallData.GetAllWalls().Count}";

        ProceduralWall pWall = newWall.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            pWall.GenerateWall(start, end);
            wallData.AddWall(start, end, ID, newWall);

            // Consumăm energie dacă este cazul
            int consumption = dataBase.objectsData[selectedObjectIndex].PowerConsumption;
            if (consumption > 0 && PowerManager.Instance != null)
            {
                PowerManager.Instance.RegisterConsumer(consumption);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Anulează plasarea curentă
    /// </summary>
    private void CancelPlacement()
    {
        ResetToNewPlacement();
    }

    /// <summary>
    /// Resetează pentru o nouă plasare
    /// </summary>
    private void ResetToNewPlacement()
    {
        CleanupPreviews();
        wallPoints.Clear();
        isPlacingWall = false;
    }

    /// <summary>
    /// Curăță toate preview-urile
    /// </summary>
    private void CleanupPreviews()
    {
        // Curățăm preview-urile segmentelor
        foreach (var preview in segmentPreviews)
        {
            if (preview != null)
                GameObject.Destroy(preview);
        }
        segmentPreviews.Clear();

        // Curățăm indicatorii de colț
        foreach (var indicator in cornerIndicators)
        {
            if (indicator != null)
                GameObject.Destroy(indicator);
        }
        cornerIndicators.Clear();

        // Curățăm preview-ul curent
        if (currentSegmentPreview != null)
        {
            GameObject.Destroy(currentSegmentPreview);
            currentSegmentPreview = null;
        }
    }

    /// <summary>
    /// Curăță tot la închiderea state-ului
    /// </summary>
    private void CleanupAll()
    {
        CleanupPreviews();
        wallPoints.Clear();
        isPlacingWall = false;
    }

    /// <summary>
    /// Arată preview în modul hover (înainte de primul click)
    /// </summary>
    private void ShowHoverPreview(Vector3 snapPoint)
    {
        if (currentSegmentPreview == null)
        {
            currentSegmentPreview = GameObject.Instantiate(wallPrefab);
            currentSegmentPreview.name = "HoverPreview";

            // Dezactivăm collider-ul pentru preview
            DisableColliders(currentSegmentPreview);
        }

        // Arătăm un segment mic pe axa X
        Vector3 endPoint = snapPoint + new Vector3(0.2f, 0, 0);

        ProceduralWall pWall = currentSegmentPreview.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            pWall.GenerateWall(snapPoint, endPoint);

            bool hasEnoughMoney = gameManager.CurrentMoney >= dataBase.objectsData[selectedObjectIndex].Cost;
            ApplyPreviewMaterial(currentSegmentPreview, hasEnoughMoney, 0.3f);
        }
    }

    /// <summary>
    /// Actualizează preview-ul segmentului curent (de la ultimul punct la mouse)
    /// </summary>
    private void UpdateCurrentSegmentPreview(Vector3 currentMousePos)
    {
        if (wallPoints.Count == 0) return;

        Vector3 lastPoint = wallPoints[wallPoints.Count - 1];

        // Verificăm dacă suntem aproape de primul punct (pentru închidere)
        bool nearStart = wallPoints.Count >= 3 && Vector3.Distance(currentMousePos, wallPoints[0]) < 0.5f;
        if (nearStart)
        {
            currentMousePos = wallPoints[0]; // Snap la primul punct
        }

        // Verificăm lungimea minimă pentru a evita eroarea de mesh
        float distance = Vector3.Distance(lastPoint, currentMousePos);
        if (distance < 0.01f)
        {
            // Prea aproape, ascundem preview-ul
            if (currentSegmentPreview != null)
            {
                currentSegmentPreview.SetActive(false);
            }
            return;
        }

        // Creăm/actualizăm preview-ul
        if (currentSegmentPreview == null)
        {
            currentSegmentPreview = GameObject.Instantiate(wallPrefab);
            currentSegmentPreview.name = "CurrentSegmentPreview";

            // Dezactivăm collider-ul pentru preview
            DisableColliders(currentSegmentPreview);
        }

        currentSegmentPreview.SetActive(true);

        ProceduralWall pWall = currentSegmentPreview.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            pWall.GenerateWall(lastPoint, currentMousePos);

            bool isValid = ValidateSegment(lastPoint, currentMousePos);
            float alpha = nearStart ? 0.7f : 0.5f; // Mai opac când suntem aproape de start
            ApplyPreviewMaterial(currentSegmentPreview, isValid, alpha);
        }

        // Actualizăm culoarea primului indicator dacă suntem aproape
        if (nearStart && cornerIndicators.Count > 0)
        {
            UpdateCornerIndicatorColor(cornerIndicators[0], Color.cyan);
        }
        else if (cornerIndicators.Count > 0)
        {
            UpdateCornerIndicatorColor(cornerIndicators[0], Color.green);
        }
    }

    /// <summary>
    /// Creează un preview permanent pentru un segment confirmat
    /// </summary>
    private void CreateSegmentPreview(Vector3 start, Vector3 end, bool isConfirmed)
    {
        GameObject preview = GameObject.Instantiate(wallPrefab);
        preview.name = $"SegmentPreview_{segmentPreviews.Count}";

        // Dezactivăm collider-ul pentru preview
        DisableColliders(preview);

        ProceduralWall pWall = preview.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            pWall.GenerateWall(start, end);
            ApplyPreviewMaterial(preview, true, isConfirmed ? 0.6f : 0.4f);
        }

        segmentPreviews.Add(preview);
    }

    /// <summary>
    /// Dezactivează collider-ele pentru preview-uri
    /// </summary>
    private void DisableColliders(GameObject obj)
    {
        MeshCollider[] colliders = obj.GetComponentsInChildren<MeshCollider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    /// <summary>
    /// Creează un indicator vizual pentru un colț
    /// </summary>
    private void CreateCornerIndicator(Vector3 position, Color color)
    {
        if (cornerIndicatorPrefab == null) return;

        GameObject indicator = GameObject.Instantiate(cornerIndicatorPrefab);
        indicator.transform.position = position + Vector3.up * 0.05f;
        indicator.name = $"CornerIndicator_{cornerIndicators.Count}";

        Renderer renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        cornerIndicators.Add(indicator);
    }

    /// <summary>
    /// Actualizează culoarea unui indicator de colț
    /// </summary>
    private void UpdateCornerIndicatorColor(GameObject indicator, Color color)
    {
        if (indicator == null) return;

        Renderer renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    /// <summary>
    /// Creează prefab-ul pentru corner indicator
    /// </summary>
    private void CreateCornerIndicatorPrefab()
    {
        cornerIndicatorPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cornerIndicatorPrefab.transform.localScale = Vector3.one * 0.2f;

        // Material transparent
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

        // Ștergem collider
        GameObject.Destroy(cornerIndicatorPrefab.GetComponent<Collider>());
        cornerIndicatorPrefab.SetActive(false);
    }

    /// <summary>
    /// Snap la colțurile grid-ului (intersecțiile celulelor)
    /// </summary>
    private Vector3 SnapToGridCorner(Vector3 worldPos)
    {
        float cellSize = grid.cellSize.x;

        // Rotunjim la cel mai apropiat multiplu de cellSize
        float snappedX = Mathf.Round(worldPos.x / cellSize) * cellSize;
        float snappedZ = Mathf.Round(worldPos.z / cellSize) * cellSize;

        return new Vector3(snappedX, 0, snappedZ);
    }

    /// <summary>
    /// SNAP MAGIC: Forțează peretele să fie perfect aliniat pe axele grid-ului (orizontal sau vertical)
    /// </summary>
    private Vector3 SnapWallToGridAxis(Vector3 startPoint, Vector3 endPoint)
    {
        // Calculăm diferențele pe fiecare axă
        float deltaX = Mathf.Abs(endPoint.x - startPoint.x);
        float deltaZ = Mathf.Abs(endPoint.z - startPoint.z);

        // Determinăm care axă e predominantă
        if (deltaX > deltaZ)
        {
            // Perete ORIZONTAL (pe axa X) - Fixăm Z
            return new Vector3(endPoint.x, startPoint.y, startPoint.z);
        }
        else
        {
            // Perete VERTICAL (pe axa Z) - Fixăm X
            return new Vector3(startPoint.x, startPoint.y, endPoint.z);
        }
    }

    /// <summary>
    /// Validează dacă un segment poate fi plasat
    /// </summary>
    private bool ValidateSegment(Vector3 start, Vector3 end)
    {
        float distance = Vector3.Distance(start, end);

        // Lungime minimă
        if (distance < 0.1f)
            return false;

        // Verificăm dacă există deja perete
        if (!wallData.CanPlaceWall(start, end))
            return false;

        // Verificăm banii
        if (gameManager.CurrentMoney < dataBase.objectsData[selectedObjectIndex].Cost)
            return false;

        return true;
    }

    /// <summary>
    /// Aplică material de preview (roșu/alb cu transparență)
    /// </summary>
    private void ApplyPreviewMaterial(GameObject wallObj, bool isValid, float alpha = 0.5f)
    {
        ProceduralWall pWall = wallObj.GetComponent<ProceduralWall>();
        if (pWall != null)
        {
            Material currentMat = pWall.GetMaterial();
            if (currentMat != null)
            {
                // Creăm o instanță nouă pentru a nu afecta materialul original
                Material previewMat = new Material(currentMat);

                Color color = isValid ? Color.white : Color.red;
                color.a = alpha;

                if (previewMat.HasProperty("_Color"))
                {
                    previewMat.color = color;
                }

                // Setăm render mode la transparent
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