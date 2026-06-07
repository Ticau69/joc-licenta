using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistem simplu de plasare pereți: Click 1 = punct start, Click 2 = plasează.
/// Fără segmente multiple, fără undo, fără loop-uri.
/// </summary>
public class WallPlacementState : IBuldingState
{
    // ── Referințe ─────────────────────────────────────────────────────────────
    private readonly int _id;
    private readonly int _selectedIndex;
    private readonly Grid _grid;
    private readonly PreviewSystem _previewSystem;
    private readonly ObjectDataBase _database;
    private readonly ObjectPlacer _objectPlacer;
    private readonly GameManager _gameManager;
    private readonly PlayerInput _playerInput;
    private readonly WallGridData _wallData;
    private readonly WallSegmentData _segmentData;
    private readonly ToolTipController _toolTip;
    private readonly GridData _floorData;
    private readonly GameObject _wallPrefab;
    private readonly Material _previewMat;

    // ── State ─────────────────────────────────────────────────────────────────
    private Vector3? _startPoint = null;        // null = așteptăm primul click
    private GameObject _previewWall = null;     // peretele preview curent
    private CameraController _cameraController;

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    public WallPlacementState(
        int id,
        Grid grid,
        PreviewSystem previewSystem,
        ObjectDataBase database,
        ObjectPlacer objectPlacer,
        GameManager gameManager,
        PlayerInput playerInput,
        WallGridData wallData,
        WallSegmentData segmentData,
        Material previewMaterial,
        ToolTipController toolTip,
        GridData floorData,
        CameraController cameraController)
    {
        _id = id;
        _grid = grid;
        _previewSystem = previewSystem;
        _database = database;
        _objectPlacer = objectPlacer;
        _gameManager = gameManager;
        _playerInput = playerInput;
        _wallData = wallData;
        _segmentData = segmentData;
        _toolTip = toolTip;
        _floorData = floorData;
        _cameraController = cameraController;

        _selectedIndex = database.objectsData.FindIndex(d => d.ID == id);
        if (_selectedIndex < 0)
            throw new System.Exception($"[WallPlacement] ID {id} negăsit în database!");

        _wallPrefab = database.objectsData[_selectedIndex].Prefab;
        _previewMat = new Material(previewMaterial);

        _previewSystem.ToggleCursorVisibility(false);
    }

    // =========================================================================
    // IBuildingState
    // =========================================================================

    public void EndState()
    {
        _toolTip?.HidePlacementInfo();
        DestroyPreview();
        if (_previewMat != null) GameObject.Destroy(_previewMat);
        _previewSystem.ToggleCursorVisibility(false);
        _startPoint = null;
    }

    public void OnAction(Vector3Int gridPosition)
    {
        Vector3 snapped = GetSnappedMousePosition();

        if (_startPoint == null)
        {
            // ── Primul click — setăm start-ul ──────────────────────────────
            _startPoint = snapped;
            Debug.Log($"[Wall] Start: {snapped}");
        }
        else
        {
            // ── Al doilea click — plasăm peretele ──────────────────────────
            Vector3 start = _startPoint.Value;
            Vector3 end = SnapAxis(start, snapped);

            if (Vector3.Distance(start, end) < 0.1f)
            {
                // Click în același punct — resetăm
                _startPoint = null;
                DestroyPreview();
                return;
            }

            PlaceWall(start, end);

            // Reset — gata pentru un perete nou
            _startPoint = null;
            DestroyPreview();
        }
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector3 mouse = GetSnappedMousePosition();

        if (_startPoint == null)
        {
            // --- SOLUȚIA: Arătăm un perete de mărimea unei celule pe axa X (Hover) ---
            float cellSize = _grid != null ? _grid.cellSize.x : 0.1f;
            UpdatePreview(mouse, mouse + new Vector3(cellSize, 0, 0), true);
            ShowTooltip(null, mouse, false);
        }
        else
        {
            // Avem start — preview complet start → cursor
            Vector3 end = SnapAxis(_startPoint.Value, mouse);
            bool valid = ValidateWall(_startPoint.Value, end);
            UpdatePreview(_startPoint.Value, end, valid);
            ShowTooltip(_startPoint.Value, end, valid);
        }
    }

    // =========================================================================
    // PLASARE
    // =========================================================================

    private void PlaceWall(Vector3 start, Vector3 end)
    {
        if (!_wallData.CanPlaceWall(start, end))
        {
            Debug.LogWarning("[Wall] Perete deja existent pe acest traseu!");
            return;
        }

        float cellSize = _grid.cellSize.x;
        int cellCount = Mathf.CeilToInt(Vector3.Distance(start, end) / cellSize);
        int cost = cellCount * _database.objectsData[_selectedIndex].Cost;

        if (!_gameManager.TrySpendMoney(cost))
        {
            Debug.LogWarning($"[Wall] Fonduri insuficiente! Cost: {cost} RON");
            return;
        }

        FinanceManager.Instance?.RegisterTransaction(TransactionCategory.Constructii_Teren, cost);

        // Plasăm cu sistemul de segmente
        Material wallMat = GetWallMaterial();
        _segmentData.AddWall(start, end, _id, _wallPrefab, wallMat);
        _wallData.AddWall(start, end, _id, null);

        int power = _database.objectsData[_selectedIndex].PowerConsumption;
        if (power > 0) PowerManager.Instance?.RegisterConsumer(power);

        Debug.Log($"[Wall] Plasat: {start} → {end} | Cost: {cost} RON");
    }

    // =========================================================================
    // PREVIEW
    // =========================================================================

    private void UpdatePreview(Vector3 start, Vector3 end, bool valid = true)
    {
        if (Vector3.Distance(start, end) < 0.001f)
        {
            if (_previewWall != null) _previewWall.SetActive(false);
            return;
        }

        if (_previewWall == null)
        {
            _previewWall = GameObject.Instantiate(_wallPrefab);
            _previewWall.name = "WallPreview";
            DisableColliders(_previewWall);
        }

        _previewWall.SetActive(true);

        ProceduralWall pw = _previewWall.GetComponent<ProceduralWall>();
        if (pw != null)
        {
            pw.GenerateWall(start, end);
            Color c = valid ? Color.white : Color.red;
            c.a = 0.5f;
            _previewMat.color = c;
            foreach (var r in _previewWall.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = _previewMat;
        }
    }

    private void DestroyPreview()
    {
        if (_previewWall != null)
        {
            GameObject.Destroy(_previewWall);
            _previewWall = null;
        }
    }

    // =========================================================================
    // TOOLTIP
    // =========================================================================

    private void ShowTooltip(Vector3? start, Vector3 end, bool valid)
    {
        if (_toolTip == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        if (start == null)
        {
            _toolTip.ShowPlacementInfo(
                $"Perete\nCost / celulă: {_database.objectsData[_selectedIndex].Cost} RON\n" +
                "[Click] Setează punctul de start",
                mouseScreen);
            return;
        }

        float cellSize = _grid.cellSize.x;
        int cells = Mathf.CeilToInt(Vector3.Distance(start.Value, end) / cellSize);
        int cost = cells * _database.objectsData[_selectedIndex].Cost;
        bool canAfford = _gameManager.CurrentMoney >= cost;

        string info = $"Perete în construcție\n" +
                      $"──────────────────\n" +
                      $"Lungime: {cells} celule\n" +
                      $"Cost total: {cost} RON\n" +
                      (canAfford ? "" : "<color=red>Fonduri insuficiente!</color>\n") +
                      $"──────────────────\n" +
                      "[Click] Plasează peretele\n" +
                      "[Esc] Anulează";

        _toolTip.ShowPlacementInfo(info, mouseScreen);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private bool ValidateWall(Vector3 start, Vector3 end)
    {
        if (Vector3.Distance(start, end) < 0.1f) return false;
        if (!_wallData.CanPlaceWall(start, end)) return false;

        float cellSize = _grid.cellSize.x;
        int cells = Mathf.CeilToInt(Vector3.Distance(start, end) / cellSize);
        int cost = cells * _database.objectsData[_selectedIndex].Cost;
        return _gameManager.CurrentMoney >= cost;
    }

    /// <summary>Forțează peretele să fie orizontal sau vertical (axa dominantă).</summary>
    private Vector3 SnapAxis(Vector3 start, Vector3 end)
    {
        float dx = Mathf.Abs(end.x - start.x);
        float dz = Mathf.Abs(end.z - start.z);
        return dx > dz
            ? new Vector3(end.x, start.y, start.z)
            : new Vector3(start.x, start.y, end.z);
    }

    private Vector3 GetSnappedMousePosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return _playerInput.GetSelectedMapPostion();

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float dist))
        {
            Vector3 hit = ray.GetPoint(dist);
            float cellSize = _grid.cellSize.x;
            Vector3 origin = _grid.transform.position;

            float sx = Mathf.Round((hit.x - origin.x) / cellSize) * cellSize + origin.x;
            float sz = Mathf.Round((hit.z - origin.z) / cellSize) * cellSize + origin.z;
            return new Vector3(sx, 0f, sz);
        }

        return _playerInput.GetSelectedMapPostion();
    }

    private Material GetWallMaterial()
    {
        if (_wallPrefab != null)
        {
            var pw = _wallPrefab.GetComponent<ProceduralWall>();
            if (pw != null) return pw.GetMaterial();

            var mr = _wallPrefab.GetComponent<MeshRenderer>();
            if (mr != null) return mr.sharedMaterial;
        }
        return null;
    }

    private void DisableColliders(GameObject obj)
    {
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    // ForceFinalize și UndoLastSegment — păstrate pentru compatibilitate cu PlacementSystem
    // dar nu fac nimic în modul simplu
    public void ForceFinalize() { }
    public void UndoLastSegment() { }
}