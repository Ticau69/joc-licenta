using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class RemovingState : IBuldingState
{
    private Grid grid;
    private PreviewSystem previewSystem;
    private GridData floorData;
    private GridData furnitureData;
    private WallGridData wallData;
    private WallSegmentData segmentData;
    private DoorData doorData;
    private ObjectPlacer objectPlacer;
    private ObjectDataBase dataBase;

    private Camera mainCamera;

    // --- NOU: Sistemul profesional de Highlight ---
    private GameObject _currentHighlightedObject;
    private Dictionary<Renderer, bool> _highlightedRenderers = new Dictionary<Renderer, bool>();
    private MaterialPropertyBlock _propBlock;

    public RemovingState(
        Grid grid,
        PreviewSystem previewSystem,
        GridData floorData,
        GridData furnitureData,
        ObjectPlacer objectPlacer,
        ObjectDataBase dataBase,
        WallGridData wallData = null,
        WallSegmentData segmentData = null,
        DoorData doorData = null)
    {
        this.grid = grid;
        this.previewSystem = previewSystem;
        this.floorData = floorData;
        this.furnitureData = furnitureData;
        this.objectPlacer = objectPlacer;
        this.dataBase = dataBase;
        this.wallData = wallData;
        this.segmentData = segmentData;
        this.doorData = doorData;

        this.mainCamera = Camera.main;
        _propBlock = new MaterialPropertyBlock(); // Inițializăm filtrul de culoare

        previewSystem.StartShowingRemovePreview();
    }

    public void EndState()
    {
        ClearHighlight();
        previewSystem.StopShowingPreview();
    }

    public void OnAction(Vector3Int gridPosition)
    {
        ClearHighlight();

        if (mainCamera == null) mainCamera = Camera.main;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int layerMask = ~LayerMask.GetMask("Placement", "Ignore Raycast");
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, layerMask);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        if (doorData != null)
        {
            foreach (RaycastHit hit in hits)
            {
                GameObject hitObject = hit.collider.gameObject;
                if (hitObject.name.Contains("gridVisualization") || hit.collider.isTrigger) continue;

                DoorInfo door = doorData.FindDoorNearPoint(hitObject.transform.position, 0.5f)
                             ?? doorData.FindDoorNearPoint(hitObject.transform.root.position, 0.5f)
                             ?? doorData.FindDoorNearPoint(hit.point, 1.0f);

                if (door != null)
                {
                    RestoreResources(door.DoorID);
                    doorData.RemoveDoor(door.Position);

                    // ── NOU: curatam si wallData in zona usii ─────────────────
                    // Gasim peretii care trec prin pozitia usii si ii scoatem
                    // din wallData ca sa permita plasarea unui perete nou
                    if (wallData != null)
                    {
                        var allWalls = wallData.GetAllWalls();
                        float tolerance = 1.5f; // cat de aproape trebuie sa fie peretele de usa

                        foreach (var wall in allWalls)
                        {
                            // Verificam daca usa e pe acest perete
                            Vector3 projected = ProjectPointOnSegment(
                                door.Position, wall.StartPosition, wall.EndPosition);

                            if (Vector3.Distance(projected, door.Position) < tolerance)
                            {
                                // Stergem si segmentele ramase ale acestui perete
                                if (segmentData != null)
                                {
                                    string wallKey = $"wall_{wall.ID}_{wall.StartPosition.x:F2}_{wall.StartPosition.z:F2}";
                                    segmentData.RemoveAllPartsFor(wallKey);
                                }

                                wallData.RemoveWall(wall.StartPosition, wall.EndPosition);
                                break;
                            }
                        }
                    }
                    // ─────────────────────────────────────────────────────────

                    return;
                }
            }
        }



        // PASUL 2: Dacă nu e ușă, verificăm pereți și restul
        int interactionLayer = LayerMask.NameToLayer("ObjectInteraction");

        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.name.Contains("gridVisualization") ||
                hitObject.transform.root.name.Contains("gridVisualization") ||
                hit.collider.isTrigger) continue;

            if (segmentData != null)
            {
                string hitName = hitObject.name;
                string wallKey = hitName;
                int partIndex = hitName.LastIndexOf("_part");
                if (partIndex != -1)
                    wallKey = hitName.Substring(0, partIndex);

                if (wallKey.StartsWith("wall_"))
                {
                    if (segmentData.RemoveWallPartByKey(hitName, wallData))
                    {
                        // Ștergem din wallData ÎNTOTDEAUNA, nu doar la ultimul part
                        // Altfel CanPlaceWall va bloca plasarea de pereți noi
                        var allWalls = wallData.GetAllWalls();
                        foreach (var wall in allWalls)
                        {
                            string expectedKey = $"wall_{wall.ID}_{wall.StartPosition.x:F2}_{wall.StartPosition.z:F2}";
                            if (expectedKey == wallKey)
                            {
                                wallData.RemoveWall(wall.StartPosition, wall.EndPosition);
                                break;
                            }
                        }

                        return;
                    }
                }
            }

            if (hitObject.layer == interactionLayer)
            {
                Vector3Int posFromRoot = grid.WorldToCell(hitObject.transform.root.position);
                if (furnitureData.GetObjectIDAt(posFromRoot) != -1)
                {
                    RemoveObjectAt(posFromRoot, furnitureData);
                    return;
                }
            }

            if (hitObject.name.Contains("Ground") || hitObject.layer == 0 || hitObject.name.Contains("Floor"))
            {
                if (floorData.GetObjectIDAt(gridPosition) != -1)
                {
                    RemoveObjectAt(gridPosition, floorData);
                    return;
                }
            }
        }

        if (furnitureData.GetObjectIDAt(gridPosition) != -1)
        {
            RemoveObjectAt(gridPosition, furnitureData);
            return;
        }

        if (floorData.GetObjectIDAt(gridPosition) != -1)
        {
            RemoveObjectAt(gridPosition, floorData);
            return;
        }
    }

    private Vector3 ProjectPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 dir = end - start;
        float len = dir.magnitude;
        dir.Normalize();
        float dot = Mathf.Clamp(Vector3.Dot(point - start, dir), 0, len);
        return start + dir * dot;
    }

    public void UpdateState(Vector3Int gridPosition)
    {
        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), false);

        GameObject target = GetTargetForHighlight(gridPosition);
        HighlightObject(target);
    }

    private GameObject GetTargetForHighlight(Vector3Int gridPosition)
    {
        // Dacă mouse-ul e pe UI (Butonul de Delete, meniu etc.), nu colorăm nimic
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        if (mainCamera == null) mainCamera = Camera.main;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int layerMask = ~LayerMask.GetMask("Placement", "Ignore Raycast");
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, layerMask);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        int interactionLayer = LayerMask.NameToLayer("ObjectInteraction");

        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.name.Contains("gridVisualization") ||
                hitObject.transform.root.name.Contains("gridVisualization") ||
                hit.collider.isTrigger) continue;

            if (doorData != null)
            {
                DoorInfo door = doorData.FindDoorNearPoint(hitObject.transform.position, 0.5f)
             ?? doorData.FindDoorNearPoint(hitObject.transform.root.position, 0.5f)
             ?? doorData.FindDoorNearPoint(hit.point, 1.0f);
                if (door != null) return hitObject.transform.root.gameObject;
            }

            if (segmentData != null)
            {
                string hitName = hitObject.name;
                string wallKey = hitName;
                int partIndex = hitName.LastIndexOf("_part");
                if (partIndex != -1)
                    wallKey = hitName.Substring(0, partIndex);

                if (wallKey.StartsWith("wall_"))
                    return hitObject;
            }

            if (hitObject.layer == interactionLayer)
            {
                Vector3Int posFromRoot = grid.WorldToCell(hitObject.transform.root.position);
                if (furnitureData.GetObjectIDAt(posFromRoot) != -1)
                {
                    int index = furnitureData.GetRepresentationIndex(posFromRoot);
                    if (index != -1) return objectPlacer.GetPlacedObject(index);
                }
            }

            if (hitObject.name.Contains("Ground") || hitObject.layer == 0 || hitObject.name.Contains("Floor"))
            {
                if (furnitureData.GetObjectIDAt(gridPosition) != -1)
                {
                    int index = furnitureData.GetRepresentationIndex(gridPosition);
                    if (index != -1) return objectPlacer.GetPlacedObject(index);
                }

                if (floorData.GetObjectIDAt(gridPosition) != -1)
                {
                    int index = floorData.GetRepresentationIndex(gridPosition);
                    if (index != -1) return objectPlacer.GetPlacedObject(index);
                }
            }
        }

        if (furnitureData.GetObjectIDAt(gridPosition) != -1)
        {
            int index = furnitureData.GetRepresentationIndex(gridPosition);
            if (index != -1) return objectPlacer.GetPlacedObject(index);
        }

        if (floorData.GetObjectIDAt(gridPosition) != -1)
        {
            int index = floorData.GetRepresentationIndex(gridPosition);
            if (index != -1) return objectPlacer.GetPlacedObject(index);
        }

        return null;
    }

    private void HighlightObject(GameObject obj)
    {
        if (_currentHighlightedObject == obj) return;

        ClearHighlight();

        if (obj == null) return;

        _currentHighlightedObject = obj;
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            _highlightedRenderers[r] = true;

            // Luăm setările curente
            r.GetPropertyBlock(_propBlock);

            // Suprascriem cu Roșu
            _propBlock.SetColor("_Color", new Color(1f, 0.2f, 0.2f, 1f));
            _propBlock.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f, 1f));

            // Aplicăm filtrul pe obiect
            r.SetPropertyBlock(_propBlock);
        }
    }

    private void ClearHighlight()
    {
        if (_currentHighlightedObject != null)
        {
            foreach (Renderer r in _highlightedRenderers.Keys)
            {
                if (r != null)
                {
                    r.GetPropertyBlock(_propBlock);
                    _propBlock.Clear(); // Eliminăm suprascrierea culorii
                    r.SetPropertyBlock(_propBlock); // Obiectul revine automat la normal
                }
            }
        }
        _highlightedRenderers.Clear();
        _currentHighlightedObject = null;
    }

    private void RemoveObjectAt(Vector3Int pos, GridData data)
    {
        int id = data.GetObjectIDAt(pos);
        RestoreResources(id);

        int index = data.GetRepresentationIndex(pos);
        if (index != -1)
        {
            data.RemoveObjectAt(pos);
            objectPlacer.RemoveObjectAt(index);
        }
    }

    private void RestoreResources(int objectID)
    {
        if (objectID == -1) return;

        var objectSettings = dataBase.objectsData.Find(x => x.ID == objectID);
        if (objectSettings != null)
        {
            if (objectSettings.PowerConsumption > 0 && PowerManager.Instance != null)
            {
                PowerManager.Instance.UnregisterConsumer(objectSettings.PowerConsumption);
            }
        }
    }
}