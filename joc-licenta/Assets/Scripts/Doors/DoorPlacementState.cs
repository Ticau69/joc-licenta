using UnityEngine;
using System.Collections.Generic;

public class DoorPlacementState : IBuldingState
{
    private readonly int ID;
    private readonly Grid grid;
    private readonly PreviewSystem previewSystem;
    private readonly ObjectDataBase dataBase;
    private readonly ObjectPlacer objectPlacer;
    private readonly GameManager gameManager;
    private readonly WallGridData wallData;
    private readonly WallSegmentData segmentData;
    private readonly DoorData doorData;
    private readonly PlayerInput playerInput;
    private readonly GameObject doorPrefab;
    private readonly int selectedObjectIndex;
    private readonly int doorSizeInSegments; // Size.x din database

    private GameObject _previewObj;
    private GameObject _boundingBox;
    private Material _previewMat;
    private Material _boxMat;
    private List<WallSegment> _hoveredGroup; // grupul de segmente selectat
    private bool _lastValid = false;

    private const float HOVER_SNAP_RADIUS = 1.0f;

    public DoorPlacementState(
        int iD, Grid grid, PreviewSystem previewSystem,
        ObjectDataBase database, ObjectPlacer objectPlacer,
        GameManager gameManager, WallGridData wallData,
        WallSegmentData segmentData, DoorData doorData,
        Material previewMaterial, Material boxMaterial, PlayerInput playerInput)
    {
        ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        dataBase = database;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;
        this.wallData = wallData;
        this.segmentData = segmentData;
        this.doorData = doorData;
        this.playerInput = playerInput;

        selectedObjectIndex = database.objectsData.FindIndex(d => d.ID == ID);
        if (selectedObjectIndex < 0)
            throw new System.Exception($"[DoorPlacement] ID {iD} negasit!");

        doorPrefab = database.objectsData[selectedObjectIndex].Prefab;
        doorSizeInSegments = database.objectsData[selectedObjectIndex].Size.x;

        _previewMat = new Material(previewMaterial);
        _boxMat = new Material(boxMaterial);
        previewSystem.ToggleCursorVisibility(false);
        CreatePreview();
    }

    // ── IBuildingState ────────────────────────────────────────────────────────

    public void EndState()
    {
        DestroyPreview();
        previewSystem.ToggleCursorVisibility(false);
        if (_previewMat != null) GameObject.Destroy(_previewMat);
        if (_boxMat != null) GameObject.Destroy(_boxMat);
    }

    public void OnAction(Vector3Int gridPosition)
    {
        if (_hoveredGroup == null || _hoveredGroup.Count < doorSizeInSegments)
        {
            Debug.Log("[DoorPlacement] Grup de segmente insuficient.");
            return;
        }
        if (!IsPlacementValid()) return;
        PlaceDoor(_hoveredGroup);

        if (SoundFXManager.Instance != null)
        {
            SoundFXManager.Instance.PlaySound(SoundType.PlaceStructure);
        }
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector3 mouseWorld = playerInput.GetSelectedMapPostion();

        // Găsim segmentul cel mai aproape de mouse
        WallSegment closest = FindClosestSegment(mouseWorld);

        if (closest == null)
        {
            _hoveredGroup = null;
            if (_previewObj != null) _previewObj.SetActive(false);
            return;
        }

        // Găsim grupul de N segmente consecutive care include segmentul closest
        _hoveredGroup = FindConsecutiveGroup(closest, doorSizeInSegments);

        if (_hoveredGroup == null || _hoveredGroup.Count < doorSizeInSegments)
        {
            if (_previewObj != null) _previewObj.SetActive(false);
            return;
        }

        // Centrul grupului = media pozițiilor tuturor segmentelor
        Vector3 groupCenter = GetGroupCenter(_hoveredGroup);
        groupCenter.y = 0f;

        Quaternion rot = GetDoorRotation(closest);

        MovePreview(groupCenter, rot);
        ApplyPreviewColor(IsPlacementValid());
    }

    // ── Segment Finding ───────────────────────────────────────────────────────

    private WallSegment FindClosestSegment(Vector3 mouseWorld)
    {
        List<WallSegment> all = segmentData.GetAllSegments();

        WallSegment closest = null;
        float closestDist = HOVER_SNAP_RADIUS;

        foreach (WallSegment seg in all)
        {
            float dist = Vector3.Distance(
                new Vector3(mouseWorld.x, 0, mouseWorld.z),
                new Vector3(seg.GetCenter().x, 0, seg.GetCenter().z)
            );

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = seg;
            }
        }

        return closest;
    }

    /// <summary>
    /// Găsește N segmente consecutive (prin SegmentIndex) care includ targetSeg.
    /// Încearcă să centreze grupul pe target — ia segmente înainte și după.
    /// </summary>
    private List<WallSegment> FindConsecutiveGroup(WallSegment targetSeg, int count)
    {
        if (count <= 1)
            return new List<WallSegment> { targetSeg };

        // Colectăm toate segmentele de pe același perete (același TotalSegments și direcție)
        List<WallSegment> wallSegs = new List<WallSegment>();
        Vector3 targetDir = targetSeg.GetDirection();

        foreach (WallSegment seg in segmentData.GetAllSegments())
        {
            // Același perete = direcție paralelă și apropiați în spațiu
            float dot = Mathf.Abs(Vector3.Dot(seg.GetDirection(), targetDir));
            float dist = Vector3.Distance(
                new Vector3(seg.GetCenter().x, 0, seg.GetCenter().z),
                new Vector3(targetSeg.GetCenter().x, 0, targetSeg.GetCenter().z)
            );

            if (dot > 0.99f && dist < targetSeg.Length * (count + 1))
                wallSegs.Add(seg);
        }

        // Sortăm după SegmentIndex
        wallSegs.Sort((a, b) => a.SegmentIndex.CompareTo(b.SegmentIndex));

        // Găsim indexul target în listă
        int targetIdx = wallSegs.IndexOf(targetSeg);
        if (targetIdx < 0) return null;

        // Centrăm grupul pe target
        int startIdx = Mathf.Max(0, targetIdx - count / 2);
        int endIdx = startIdx + count;

        if (endIdx > wallSegs.Count)
        {
            endIdx = wallSegs.Count;
            startIdx = Mathf.Max(0, endIdx - count);
        }

        if (endIdx - startIdx < count) return null; // nu sunt destule segmente

        // Verificăm că sunt consecutive (fără goluri)
        List<WallSegment> group = wallSegs.GetRange(startIdx, endIdx - startIdx);
        for (int i = 1; i < group.Count; i++)
        {
            if (group[i].SegmentIndex != group[i - 1].SegmentIndex + 1)
                return null; // gol în secvență (ex: altă ușă)
        }

        return group;
    }

    private Vector3 GetGroupCenter(List<WallSegment> group)
    {
        Vector3 sum = Vector3.zero;
        foreach (WallSegment seg in group)
            sum += seg.GetCenter();
        return sum / group.Count;
    }

    // ── Rotation ──────────────────────────────────────────────────────────────

    private Quaternion GetDoorRotation(WallSegment segment)
    {
        Vector3 wallDir = segment.GetDirection();
        Vector3 doorForward = Vector3.Cross(wallDir, Vector3.up);
        if (doorForward == Vector3.zero) return Quaternion.identity;
        return Quaternion.LookRotation(doorForward, Vector3.up);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private bool IsPlacementValid()
    {
        if (_hoveredGroup == null || _hoveredGroup.Count < doorSizeInSegments)
            return false;

        Vector3 center = GetGroupCenter(_hoveredGroup);
        if (doorData.HasDoorAt(center, 0.4f)) return false;

        int cost = dataBase.objectsData[selectedObjectIndex].Cost;
        return gameManager.CurrentMoney >= cost;
    }

    // ── Place Door ────────────────────────────────────────────────────────────

    private void PlaceDoor(List<WallSegment> group)
    {
        int cost = dataBase.objectsData[selectedObjectIndex].Cost;
        if (!gameManager.TrySpendMoney(cost)) return;

        if (FinanceManager.Instance != null)
            FinanceManager.Instance.RegisterTransaction(
                TransactionCategory.Constructii_Teren, cost);

        Vector3 pos = GetGroupCenter(group);
        Quaternion rot = GetDoorRotation(group[0]);
        pos.y = 0f;

        GameObject newDoor = GameObject.Instantiate(doorPrefab, pos, rot);
        newDoor.name = $"Door_{doorData.GetAllDoors().Count}";
        doorData.AddDoor(pos, rot, ID, newDoor);

        // Ștergem exact segmentele din grup
        foreach (WallSegment seg in group)
        {
            float radius = seg.Length * 0.4f;
            segmentData.RemoveSegmentsInRange(seg.GetCenter(), radius, out _);
        }

        Debug.Log($"[DoorPlacement] Usa plasata la {pos}, {group.Count} segmente sterse.");
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private void CreatePreview()
    {
        if (doorPrefab == null) return;

        _previewObj = GameObject.Instantiate(doorPrefab);
        _previewObj.name = "DoorPreview";

        foreach (Collider col in _previewObj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        ApplyPreviewMaterial(_previewObj);

        Vector2Int doorSize = dataBase.objectsData[selectedObjectIndex].Size;

        Vector3 cellSize = grid.cellSize;

        float worldWidth = doorSize.x * cellSize.x;
        float worldDepth = 0.5f;
        float worldHeight = 1.55f;

        _boundingBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _boundingBox.name = "DoorBoundingBox";
        GameObject.Destroy(_boundingBox.GetComponent<Collider>());

        _boundingBox.transform.SetParent(_previewObj.transform, false);
        _boundingBox.transform.localPosition = new Vector3(0f, worldHeight / 2f, 0f);
        _boundingBox.transform.rotation = Quaternion.identity;
        _boundingBox.transform.localScale = new Vector3(worldWidth, worldHeight, worldDepth);

        _boundingBox.GetComponent<Renderer>().material = _boxMat;

        _previewObj.SetActive(false);
    }

    private void MovePreview(Vector3 pos, Quaternion rot)
    {
        if (_previewObj == null) return;
        _previewObj.SetActive(true);
        _previewObj.transform.position = pos;
        _previewObj.transform.rotation = rot;
    }

    private void ApplyPreviewColor(bool valid)
    {
        if (valid == _lastValid) return;
        _lastValid = valid;

        Color c = valid ? Color.white : Color.red;
        c.a = 0.5f;
        _previewMat.color = c;

        // Color boxColor = valid ? Color.white : Color.red;
        // boxColor.a = 0.5f;
        // _boxMat.color = boxColor;

        if (_boxMat != null)
        {
            if (valid)
            {
                _boxMat.SetColor("_BoxColor", new Color(0f, 1f, 0f, 0.2f)); // verde transparent
                _boxMat.SetFloat("_WireThickness", 0.02f);
            }
            else
            {
                _boxMat.SetColor("_BoxColor", new Color(1f, 0f, 0f, 0.2f)); // roșu transparent
                _boxMat.SetFloat("_WireThickness", 0.02f);
            }
        }
    }

    private void ApplyPreviewMaterial(GameObject obj)
    {
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = new Material[r.materials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = _previewMat;
            r.materials = mats;
        }
    }

    private void DestroyPreview()
    {
        if (_previewObj != null) { GameObject.Destroy(_previewObj); _previewObj = null; }
    }
}