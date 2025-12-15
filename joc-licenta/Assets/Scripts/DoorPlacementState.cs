using UnityEngine;
using System.Collections.Generic;

public class DoorPlacementState : IBuldingState
{
    private int selectedObjectIndex = -1;
    private int ID;
    private Grid grid;
    private PreviewSystem previewSystem;
    private ObjectDataBase dataBase;
    private ObjectPlacer objectPlacer;
    private GameManager gameManager;
    private WallGridData wallData;
    private WallSegmentData segmentData; // NOU: Pentru ștergerea segmentelor

    private GameObject doorPreview;
    private GameObject doorPrefab;

    // Date despre plasamentul curent
    private WallData currentWall = null;
    private Vector3 currentDoorPosition;
    private Quaternion currentDoorRotation;
    private bool hasValidPlacement = false;

    // Setări pentru uși
    private float doorWidth = 2f; // Lățimea usei
    private float snapDistance = 1.5f; // Distanța maximă pentru snap la perete

    public DoorPlacementState(
        int iD,
        Grid grid,
        PreviewSystem previewSystem,
        ObjectDataBase database,
        ObjectPlacer objectPlacer,
        GameManager gameManager,
        WallGridData wallData,
        WallSegmentData segmentData) // NOU
    {
        this.ID = iD;
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.dataBase = database;
        this.objectPlacer = objectPlacer;
        this.gameManager = gameManager;
        this.wallData = wallData;
        this.segmentData = segmentData; // NOU

        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);

        if (selectedObjectIndex > -1)
        {
            doorPrefab = database.objectsData[selectedObjectIndex].Prefab;

            // Ascundem cursor-ul standard
            previewSystem.ToggleCursorVisibility(false);

            // Creăm preview-ul usei
            CreateDoorPreview();
        }
        else
        {
            throw new System.Exception($"Nu s-a găsit obiectul cu ID {iD}");
        }
    }

    public void EndState()
    {
        CleanupPreview();
        previewSystem.ToggleCursorVisibility(false);
    }

    public void OnAction(Vector3Int gridPosition)
    {
        if (!hasValidPlacement)
        {
            Debug.Log("Plasament invalid! Ușa trebuie plasată pe un perete.");
            return;
        }

        // Verificăm banii
        int doorCost = dataBase.objectsData[selectedObjectIndex].Cost;
        if (!gameManager.TrySpendMoney(doorCost))
        {
            Debug.Log("Nu ai suficienți bani pentru a plasa ușa!");
            return;
        }

        // Plasăm ușa
        PlaceDoor(currentDoorPosition, currentDoorRotation);
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        Vector3 mousePos = grid.CellToWorld(gridPosition);

        // Găsim cel mai apropiat perete de mouse
        WallData nearestWall = FindNearestWallToMouse(mousePos);

        if (nearestWall != null)
        {
            // Calculăm poziția și rotația usei EXACT PE PERETE
            CalculateDoorPlacementOnWall(nearestWall, mousePos);
            hasValidPlacement = true;
        }
        else
        {
            hasValidPlacement = false;
        }

        UpdateDoorPreview();
    }

    /// <summary>
    /// Creează preview-ul pentru ușă
    /// </summary>
    private void CreateDoorPreview()
    {
        doorPreview = GameObject.Instantiate(doorPrefab);
        doorPreview.name = "DoorPreview";

        // Dezactivăm collider-ele
        Collider[] colliders = doorPreview.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Aplicăm material transparent
        ApplyPreviewMaterial(doorPreview, false);
    }

    /// <summary>
    /// Găsește cel mai apropiat perete de poziția mouse-ului
    /// </summary>
    private WallData FindNearestWallToMouse(Vector3 mousePos)
    {
        var allWalls = wallData.GetAllWalls();
        WallData nearestWall = null;
        float minDistance = float.MaxValue;

        foreach (var wall in allWalls)
        {
            // Calculăm distanța de la mouse la linia peretelui
            float distance = DistanceToLineSegment(mousePos, wall.StartPosition, wall.EndPosition);

            if (distance < minDistance && distance < snapDistance)
            {
                minDistance = distance;
                nearestWall = wall;
            }
        }

        return nearestWall;
    }

    /// <summary>
    /// Calculează poziția EXACTĂ a usei pe perete (fără snap la centrul celulei)
    /// </summary>
    private void CalculateDoorPlacementOnWall(WallData wall, Vector3 mousePos)
    {
        currentWall = wall;

        // 1. PROIECTĂM mouse-ul pe linia peretelui (fără snap la grid!)
        Vector3 projectedPoint = ProjectPointOnLineSegment(mousePos, wall.StartPosition, wall.EndPosition);

        // 2. Opțional: Snap la intervale regulate de-a lungul peretelui (nu la centrul celulei)
        // Comentează această linie dacă vrei plasare complet liberă
        projectedPoint = SnapAlongWall(projectedPoint, wall, 0.5f); // Snap la fiecare 0.5m

        // 3. Verificăm dacă ușa încape pe perete (nu depășește capetele)
        float distToStart = Vector3.Distance(projectedPoint, wall.StartPosition);
        float distToEnd = Vector3.Distance(projectedPoint, wall.EndPosition);
        float halfDoorWidth = doorWidth / 2f;

        // Clamping: Dacă e prea aproape de capete, mutăm ușa
        if (distToStart < halfDoorWidth)
        {
            Vector3 wallDir = wall.GetDirection();
            projectedPoint = wall.StartPosition + wallDir * halfDoorWidth;
        }
        else if (distToEnd < halfDoorWidth)
        {
            Vector3 wallDir = wall.GetDirection();
            projectedPoint = wall.EndPosition - wallDir * halfDoorWidth;
        }

        // 4. Setăm poziția finală (pe sol, y=0)
        currentDoorPosition = new Vector3(projectedPoint.x, 0, projectedPoint.z);

        // 5. Calculăm rotația (perpendiculară pe perete)
        Vector3 wallDirection = wall.GetDirection();
        float angle = Mathf.Atan2(wallDirection.x, wallDirection.z) * Mathf.Rad2Deg;
        currentDoorRotation = Quaternion.Euler(0, angle, 0);

        // Debug pentru vizualizare
        Debug.DrawLine(wall.StartPosition, wall.EndPosition, Color.cyan, 0.1f);
        Debug.DrawLine(mousePos, projectedPoint, Color.yellow, 0.1f);
        Debug.DrawRay(currentDoorPosition, Vector3.up * 2f, Color.green, 0.1f);
    }

    /// <summary>
    /// Snap de-a lungul peretelui la intervale regulate (nu la grid!)
    /// </summary>
    private Vector3 SnapAlongWall(Vector3 point, WallData wall, float snapInterval)
    {
        // Calculăm distanța de la start la punctul proiectat
        float distanceAlongWall = Vector3.Distance(wall.StartPosition, point);

        // Rotunjim la cel mai apropiat multiplu de snapInterval
        float snappedDistance = Mathf.Round(distanceAlongWall / snapInterval) * snapInterval;

        // Calculăm noua poziție
        Vector3 wallDir = wall.GetDirection();
        return wall.StartPosition + wallDir * snappedDistance;
    }

    /// <summary>
    /// Actualizează preview-ul usei
    /// </summary>
    private void UpdateDoorPreview()
    {
        if (doorPreview == null) return;

        if (hasValidPlacement)
        {
            doorPreview.SetActive(true);
            doorPreview.transform.position = currentDoorPosition;
            doorPreview.transform.rotation = currentDoorRotation;

            // Verificăm dacă avem bani
            bool hasEnoughMoney = gameManager.CurrentMoney >= dataBase.objectsData[selectedObjectIndex].Cost;
            ApplyPreviewMaterial(doorPreview, hasEnoughMoney);
        }
        else
        {
            doorPreview.SetActive(false);
        }
    }

    /// <summary>
    /// Plasează ușa în lume și șterge segmentele de perete
    /// </summary>
    private void PlaceDoor(Vector3 position, Quaternion rotation)
    {
        // 1. ȘTERGEM SEGMENTELE DE PERETE din zona usei
        int removedCount = 0;
        if (segmentData != null)
        {
            // Raza = lățimea usei + un pic extra pentru siguranță
            float clearanceRadius = doorWidth / 2f + 0.1f;
            segmentData.RemoveSegmentsInRange(position, clearanceRadius, out removedCount);

            Debug.Log($"Segmente de perete șterse pentru ușă: {removedCount}");
        }

        // 2. PLASĂM UȘA
        GameObject newDoor = GameObject.Instantiate(doorPrefab);
        newDoor.transform.position = position;
        newDoor.transform.rotation = rotation;
        newDoor.name = $"Door_{wallData.GetAllWalls().Count}";

        // 3. Consumăm energie dacă este cazul
        int consumption = dataBase.objectsData[selectedObjectIndex].PowerConsumption;
        if (consumption > 0 && PowerManager.Instance != null)
        {
            PowerManager.Instance.RegisterConsumer(consumption);
        }

        Debug.Log($"Ușă plasată la: {position}, rotație: {rotation.eulerAngles.y}°, segmente șterse: {removedCount}");
    }

    /// <summary>
    /// Curăță preview-ul
    /// </summary>
    private void CleanupPreview()
    {
        if (doorPreview != null)
        {
            GameObject.Destroy(doorPreview);
            doorPreview = null;
        }
    }

    /// <summary>
    /// Aplică material de preview (roșu/alb cu transparență)
    /// </summary>
    private void ApplyPreviewMaterial(GameObject obj, bool isValid, float alpha = 0.5f)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        foreach (var renderer in renderers)
        {
            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material previewMat = new Material(materials[i]);

                Color color = isValid ? Color.white : Color.red;
                color.a = alpha;
                previewMat.color = color;

                // Setăm render mode la transparent
                previewMat.SetFloat("_Mode", 3);
                previewMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                previewMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                previewMat.SetInt("_ZWrite", 0);
                previewMat.DisableKeyword("_ALPHATEST_ON");
                previewMat.EnableKeyword("_ALPHABLEND_ON");
                previewMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                previewMat.renderQueue = 3000;

                materials[i] = previewMat;
            }

            renderer.materials = materials;
        }
    }

    // ============ FUNCȚII MATEMATICE HELPER ============

    /// <summary>
    /// Calculează distanța de la un punct la un segment de linie
    /// </summary>
    private float DistanceToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 projectedPoint = ProjectPointOnLineSegment(point, lineStart, lineEnd);
        return Vector3.Distance(point, projectedPoint);
    }

    /// <summary>
    /// Proiectează un punct pe un segment de linie (perpendicular)
    /// Returnează cel mai apropiat punct de pe linie
    /// </summary>
    private Vector3 ProjectPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();

        Vector3 pointVector = point - lineStart;
        float dot = Vector3.Dot(pointVector, lineDirection);

        // Clamp la lungimea liniei (ca să nu depășească capetele)
        dot = Mathf.Clamp(dot, 0, lineLength);

        return lineStart + lineDirection * dot;
    }
}