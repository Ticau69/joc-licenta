using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

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
        previewSystem.StartShowingRemovePreview();
    }

    public void EndState()
    {
        previewSystem.StopShowingPreview();
    }

    public void OnAction(Vector3Int gridPosition)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // LayerMask: Ignorăm "Placement" și "Ignore Raycast"
        int layerMask = ~LayerMask.GetMask("Placement", "Ignore Raycast");

        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, layerMask);

        // Sortăm după distanță
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;

            // --- FIX 1: SIGURANȚĂ PENTRU GRID ---
            // Dacă layer-ul nu e setat corect pe copii, verificăm și numele
            if (hitObject.name.Contains("gridVisualization") ||
                hitObject.transform.root.name.Contains("gridVisualization") ||
                hit.collider.isTrigger)
            {
                continue; // Ignorăm și trecem la următorul obiect (din spate)
            }

            Debug.Log($"Raycast a lovit valid: {hitObject.name}");

            // --- 1. VERIFICARE UȘI ---
            if (doorData != null)
            {
                // Căutăm ușa folosind poziția obiectului lovit (Centrul ușii)
                DoorInfo door = doorData.FindDoorNearPoint(hitObject.transform.position, 0.2f);

                // Fallback la punctul de impact
                if (door == null) door = doorData.FindDoorNearPoint(hit.point, 1.0f);

                if (door != null)
                {
                    RestoreResources(door.DoorID);
                    doorData.RemoveDoor(door.Position);
                    Debug.Log("Ușă ștearsă!");
                    return;
                }
            }

            // --- 2. VERIFICARE PEREȚI ---
            if (segmentData != null)
            {
                ProceduralWall wallScript = hitObject.GetComponentInParent<ProceduralWall>();

                if (wallScript != null)
                {
                    // --- FIX 2: PRECIZIE ȘTERGERE ---
                    // În loc să folosim hit.point (care e la margine), folosim transform.position
                    // al obiectului lovit. ProceduralWall își setează pivotul exact în centru.
                    // Astfel distanța va fi 0, și ștergerea e garantată.

                    Vector3 segmentCenter = wallScript.transform.position;

                    int removedCount;
                    segmentData.RemoveSegmentsInRange(segmentCenter, 0.5f, out removedCount);

                    if (removedCount > 0)
                    {
                        Debug.Log("Perete șters!");
                        return;
                    }
                }
            }

            // --- 3. VERIFICARE MOBILĂ ---
            Vector3Int objectGridPos = grid.WorldToCell(hit.point);
            int furnitureID = furnitureData.GetObjectIDAt(objectGridPos);

            if (furnitureID != -1)
            {
                RemoveObjectAt(objectGridPos, furnitureData);
                Debug.Log("Mobilă ștearsă!");
                return;
            }

            // --- 4. VERIFICARE PODEA ---
            // Dacă lovim ceva ce pare a fi podeaua (Ground, Default layer)
            if (hitObject.name.Contains("Ground") || hitObject.layer == 0)
            {
                int floorID = floorData.GetObjectIDAt(objectGridPos);
                if (floorID != -1)
                {
                    RemoveObjectAt(objectGridPos, floorData);
                    Debug.Log("Podea ștearsă!");
                    return;
                }

                // Dacă am lovit pământul și nu era podea, ne oprim.
                break;
            }
        }
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

    public void UpdateState(Vector3Int gridPosition)
    {
        previewSystem.UpdatePosition(grid.CellToWorld(gridPosition), false);
    }

    public bool CheckIfSelectionIsValid(Vector3Int gridPosition) => true;
}