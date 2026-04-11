using System;
using System.Collections.Generic;
using UnityEngine;

public class WallSegmentData
{
    // Segmente logice (pentru detectie usa/fereastra)
    private Dictionary<string, WallSegment> segments = new();

    // Combined wall GameObjects (un singur GO per perete plasat)
    private Dictionary<string, GameObject> segmentObjects = new();

    // Mapping bidirecțional: seg_key → wall_key și wall_key → lista seg_keys
    private Dictionary<string, string> segmentToWallKey = new();
    private Dictionary<string, List<string>> wallToSegmentKeys = new();

    // Copie a datelor segmentelor pentru rebuild după ștergere
    private Dictionary<string, WallSegment> allSegmentsData = new();

    // Materialul per wall (necesar pentru rebuild)
    private Dictionary<string, Material> wallMaterials = new();
    private Dictionary<string, float> wallHeights = new Dictionary<string, float>();

    private float segmentLength = 1f;

    public WallSegmentData(float segmentSize = 1f)
    {
        this.segmentLength = segmentSize;
    }

    public void AddWall(Vector3 startPos, Vector3 endPos, int wallID, GameObject wallPrefab, Material wallMaterial)
    {
        float wallHeight = 1.5f; // fallback
        if (wallPrefab != null)
        {
            ProceduralWall prefabWall = wallPrefab.GetComponent<ProceduralWall>();
            if (prefabWall != null)
                wallHeight = prefabWall.height; // trebuie sa fie public sau internal
        }
        float totalLength = Vector3.Distance(startPos, endPos);
        Vector3 direction = (endPos - startPos).normalized;

        int segmentCount = Mathf.CeilToInt(totalLength / 1f);
        float actualSegmentLength = totalLength / segmentCount;

        string wallKey = $"wall_{wallID}_{startPos.x:F2}_{startPos.z:F2}";
        wallHeights[wallKey] = wallHeight;

        List<string> segmentKeysForThisWall = new List<string>();
        List<MeshFilter> tempMeshFilters = new List<MeshFilter>();
        List<GameObject> tempObjects = new List<GameObject>();

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 segStart = startPos + direction * (i * actualSegmentLength);
            Vector3 segEnd = startPos + direction * ((i + 1) * actualSegmentLength);

            string segmentKey = GenerateSegmentKey(segStart, segEnd);
            if (segments.ContainsKey(segmentKey)) continue;

            WallSegment segment = new WallSegment(segStart, segEnd, wallID, i, segmentCount);
            segments[segmentKey] = segment;
            allSegmentsData[segmentKey] = segment;

            segmentToWallKey[segmentKey] = wallKey;
            segmentKeysForThisWall.Add(segmentKey);

            GameObject tempObj = CreateTempSegmentObject(segStart, segEnd, wallMaterial, wallHeight);
            tempObjects.Add(tempObj);
            tempMeshFilters.Add(tempObj.GetComponent<MeshFilter>());
        }

        wallToSegmentKeys[wallKey] = segmentKeysForThisWall;
        wallMaterials[wallKey] = wallMaterial;

        if (tempMeshFilters.Count == 0) return;

        GameObject combinedWall = CombineSegments(tempMeshFilters, wallMaterial);
        combinedWall.name = wallKey;

        MeshCollider col = combinedWall.AddComponent<MeshCollider>();
        col.sharedMesh = combinedWall.GetComponent<MeshFilter>().sharedMesh;

        var obstacle = combinedWall.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.carving = true;

        segmentObjects[wallKey] = combinedWall;

        foreach (var temp in tempObjects)
            GameObject.Destroy(temp);
    }

    public bool HasAnyPartFor(string wallKey)
    {
        // Verificăm dacă mai există _partX pentru acest wallKey
        foreach (var key in segmentObjects.Keys)
        {
            if (key.StartsWith(wallKey))
                return true;
        }
        return false;
    }

    private GameObject CreateTempSegmentObject(Vector3 start, Vector3 end,
                                                Material material, float height)
    {
        GameObject obj = new GameObject("TempSegment");
        obj.AddComponent<MeshFilter>();
        obj.AddComponent<MeshRenderer>();

        ProceduralWall pWall = obj.AddComponent<ProceduralWall>();
        pWall.SetHeight(height); // ── NOU
        pWall.GenerateWall(start, end);

        if (material != null)
            pWall.SetMaterial(material);

        return obj;
    }

    private GameObject CombineSegments(List<MeshFilter> meshFilters, Material material)
    {
        CombineInstance[] combine = new CombineInstance[meshFilters.Count];

        for (int i = 0; i < meshFilters.Count; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
        }

        GameObject combined = new GameObject("CombinedWall");

        MeshFilter mf = combined.AddComponent<MeshFilter>();
        mf.mesh = new Mesh();
        mf.mesh.CombineMeshes(combine, true, true);
        mf.mesh.RecalculateBounds(); // ── NOU: bounds corecte inainte de obstacle

        MeshRenderer mr = combined.AddComponent<MeshRenderer>();
        if (material != null)
            mr.material = material;

        return combined;
    }



    public List<WallSegment> RemoveSegmentsInRange(Vector3 centerPos, float range, out int removedCount)
    {
        List<WallSegment> removedSegments = new List<WallSegment>();
        List<string> segmentKeysToRemove = new List<string>();
        HashSet<string> affectedWallKeys = new HashSet<string>();

        // 1. Găsim segmentele afectate
        foreach (var kvp in segments)
        {
            Vector3 segmentCenter = kvp.Value.GetCenter();
            float distance = Vector3.Distance(segmentCenter, centerPos);

            if (distance < range)
            {
                segmentKeysToRemove.Add(kvp.Key);
                removedSegments.Add(kvp.Value);

                if (segmentToWallKey.ContainsKey(kvp.Key))
                    affectedWallKeys.Add(segmentToWallKey[kvp.Key]);
            }
        }

        // 2. Ștergem segmentele din date
        foreach (string key in segmentKeysToRemove)
        {
            segments.Remove(key);
            segmentToWallKey.Remove(key);
        }

        // 3. Reconstruim fiecare combined wall afectat
        foreach (string wallKey in affectedWallKeys)
        {
            RebuildCombinedWall(wallKey, segmentKeysToRemove);
        }

        removedCount = segmentKeysToRemove.Count;
        return removedSegments;
    }

    public bool RemoveWallByKey(string wallKey, WallGridData wallGridData)
    {
        if (!wallToSegmentKeys.ContainsKey(wallKey)) return false;

        foreach (string segKey in wallToSegmentKeys[wallKey])
        {
            segments.Remove(segKey);
            segmentToWallKey.Remove(segKey);
            allSegmentsData.Remove(segKey); // lipsea asta
        }

        DestroyWallObjects(wallKey);

        wallToSegmentKeys.Remove(wallKey);
        wallMaterials.Remove(wallKey);

        return true;
    }

    public bool RemoveWallPartByKey(string fullHitName, WallGridData wallGridData)
    {
        if (fullHitName.Contains("_part"))
        {
            if (!segmentObjects.ContainsKey(fullHitName)) return false;

            // Distrugem GameObject-ul
            GameObject.Destroy(segmentObjects[fullHitName]);
            segmentObjects.Remove(fullHitName);

            // Găsim wallKey original
            int partIndex = fullHitName.LastIndexOf("_part");
            string wallKey = fullHitName.Substring(0, partIndex);

            // Curățăm și datele logice ale segmentelor din acest part
            if (wallToSegmentKeys.ContainsKey(wallKey))
            {
                // Identificăm ce segmente aparțin acestui part specific
                // Reconstruim grupurile ca să știm care segmente sunt în acest part
                List<List<string>> groups = FindContiguousGroups(wallToSegmentKeys[wallKey]);

                int partNumber = int.Parse(fullHitName.Substring(partIndex + 5));
                if (partNumber < groups.Count)
                {
                    foreach (string segKey in groups[partNumber])
                    {
                        segments.Remove(segKey);
                        segmentToWallKey.Remove(segKey);
                        allSegmentsData.Remove(segKey);
                        wallToSegmentKeys[wallKey].Remove(segKey);
                    }
                }

                // Dacă nu mai sunt segmente, curățăm wallKey complet
                if (wallToSegmentKeys[wallKey].Count == 0)
                {
                    wallToSegmentKeys.Remove(wallKey);
                    wallMaterials.Remove(wallKey);
                }
            }

            return true;
        }

        return RemoveWallByKey(fullHitName, wallGridData);
    }

    public void RemoveAllPartsFor(string wallKey)
    {
        List<string> keysToRemove = new List<string>();

        foreach (var key in segmentObjects.Keys)
        {
            if (key == wallKey || key.StartsWith(wallKey + "_part"))
                keysToRemove.Add(key);
        }

        foreach (string key in keysToRemove)
        {
            if (segmentObjects.ContainsKey(key))
            {
                GameObject.Destroy(segmentObjects[key]);
                segmentObjects.Remove(key);
            }
        }

        // Curățăm și segmentele logice
        if (wallToSegmentKeys.ContainsKey(wallKey))
        {
            foreach (string segKey in wallToSegmentKeys[wallKey])
            {
                segments.Remove(segKey);
                segmentToWallKey.Remove(segKey);
                allSegmentsData.Remove(segKey);
            }
            wallToSegmentKeys.Remove(wallKey);
            wallMaterials.Remove(wallKey);
        }
    }

    private void RebuildCombinedWall(string wallKey, List<string> removedKeys)
    {
        if (!wallToSegmentKeys.ContainsKey(wallKey)) return;

        wallToSegmentKeys[wallKey].RemoveAll(k => removedKeys.Contains(k));

        // Distrugem toate obiectele vechi asociate acestui wallKey
        // (pot fi mai multe dacă a mai fost rebuildat)
        DestroyWallObjects(wallKey);

        List<string> remainingKeys = wallToSegmentKeys[wallKey];

        if (remainingKeys.Count == 0)
        {
            wallToSegmentKeys.Remove(wallKey);
            wallMaterials.Remove(wallKey);
            return;
        }

        Material mat = wallMaterials.ContainsKey(wallKey) ? wallMaterials[wallKey] : null;

        // Grupăm segmentele continue
        List<List<string>> contiguousGroups = FindContiguousGroups(remainingKeys);

        // Creăm un combined wall per grup continuu
        for (int g = 0; g < contiguousGroups.Count; g++)
        {
            List<string> group = contiguousGroups[g];
            string groupKey = $"{wallKey}_part{g}";

            List<MeshFilter> tempMeshFilters = new List<MeshFilter>();
            List<GameObject> tempObjects = new List<GameObject>();

            foreach (string segKey in group)
            {
                if (!allSegmentsData.ContainsKey(segKey)) continue;

                WallSegment seg = allSegmentsData[segKey];
                float h = wallHeights.ContainsKey(wallKey) ? wallHeights[wallKey] : 1.5f;
                GameObject tempObj = CreateTempSegmentObject(seg.StartPosition, seg.EndPosition, mat, h);
                tempObjects.Add(tempObj);
                tempMeshFilters.Add(tempObj.GetComponent<MeshFilter>());
            }

            if (tempMeshFilters.Count == 0)
            {
                foreach (var temp in tempObjects) GameObject.Destroy(temp);
                continue;
            }

            GameObject newCombined = CombineSegments(tempMeshFilters, mat);
            newCombined.name = groupKey;

            // Collider
            MeshCollider col = newCombined.AddComponent<MeshCollider>();
            col.sharedMesh = newCombined.GetComponent<MeshFilter>().sharedMesh;

            // Obstacle bazat pe bounds-ul REAL al acestui grup
            AddNavMeshObstacleFromMesh(newCombined);

            segmentObjects[groupKey] = newCombined;

            foreach (var temp in tempObjects) GameObject.Destroy(temp);

            UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame =
                UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame;
        }
    }

    /// <summary>
    /// Grupează cheile de segmente în grupuri continue (fără goluri între ele)
    /// </summary>
    private List<List<string>> FindContiguousGroups(List<string> segmentKeys)
    {
        List<List<string>> groups = new List<List<string>>();
        if (segmentKeys.Count == 0) return groups;

        // Sortăm segmentele după poziție de-a lungul peretelui
        List<WallSegment> segs = new List<WallSegment>();
        List<string> validKeys = new List<string>();

        foreach (string key in segmentKeys)
        {
            if (allSegmentsData.ContainsKey(key))
            {
                segs.Add(allSegmentsData[key]);
                validKeys.Add(key);
            }
        }

        if (segs.Count == 0) return groups;

        // Sortăm după SegmentIndex
        List<(string key, WallSegment seg)> sorted = new List<(string, WallSegment)>();
        for (int i = 0; i < segs.Count; i++)
            sorted.Add((validKeys[i], segs[i]));
        sorted.Sort((a, b) => a.seg.SegmentIndex.CompareTo(b.seg.SegmentIndex));

        // Grupăm segmentele cu indecși consecutivi
        List<string> currentGroup = new List<string> { sorted[0].key };

        for (int i = 1; i < sorted.Count; i++)
        {
            int prevIndex = sorted[i - 1].seg.SegmentIndex;
            int currIndex = sorted[i].seg.SegmentIndex;

            if (currIndex == prevIndex + 1)
            {
                // Segment consecutiv — același grup
                currentGroup.Add(sorted[i].key);
            }
            else
            {
                // Gap detectat (ușă/fereastră) — grup nou
                groups.Add(currentGroup);
                currentGroup = new List<string> { sorted[i].key };
            }
        }

        groups.Add(currentGroup);
        return groups;
    }

    /// <summary>
    /// Distruge toate obiectele asociate unui wallKey (inclusiv _part0, _part1 etc.)
    /// </summary>
    private void DestroyWallObjects(string wallKey)
    {
        // Distrugem obiectul principal
        if (segmentObjects.ContainsKey(wallKey))
        {
            GameObject.Destroy(segmentObjects[wallKey]);
            segmentObjects.Remove(wallKey);
        }

        // Distrugem și part-urile din rebuild-uri anterioare
        List<string> keysToRemove = new List<string>();
        foreach (var key in segmentObjects.Keys)
        {
            if (key.StartsWith(wallKey + "_part"))
                keysToRemove.Add(key);
        }

        foreach (string key in keysToRemove)
        {
            if (segmentObjects[key] != null)
                GameObject.Destroy(segmentObjects[key]);
            segmentObjects.Remove(key);
        }
    }

    private void AddNavMeshObstacleFromMesh(GameObject wallObject)
    {
        MeshFilter mf = wallObject.GetComponent<MeshFilter>();
        if (mf == null || mf.mesh == null) return;

        var obstacle = wallObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.carving = false; // off intai
        obstacle.carvingMoveThreshold = 0f;
        obstacle.carvingTimeToStationary = 0f;
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;

        Bounds meshBounds = mf.mesh.bounds;
        obstacle.center = meshBounds.center;
        obstacle.size = meshBounds.size;

        // Forteaza carving imediat indiferent de timeScale
        obstacle.carving = true;
    }

    public List<WallSegment> FindSegmentsNearPoint(Vector3 point, float tolerance = 0.2f)
    {
        List<WallSegment> nearSegments = new List<WallSegment>();

        foreach (var segment in segments.Values)
        {
            float distToStart = Vector3.Distance(point, segment.StartPosition);
            float distToEnd = Vector3.Distance(point, segment.EndPosition);

            if (distToStart < tolerance || distToEnd < tolerance)
            {
                nearSegments.Add(segment);
            }
            else
            {
                Vector3 projected = ProjectPointOnLineSegment(point, segment.StartPosition, segment.EndPosition);
                if (Vector3.Distance(point, projected) < tolerance)
                    nearSegments.Add(segment);
            }
        }

        return nearSegments;
    }

    public List<WallSegment> GetAllSegments() => new List<WallSegment>(segments.Values);

    public WallSegment GetSegment(string key) => segments.ContainsKey(key) ? segments[key] : null;

    public void ClearAll()
    {
        foreach (var obj in segmentObjects.Values)
        {
            if (obj != null)
                GameObject.Destroy(obj);
        }

        segments.Clear();
        segmentObjects.Clear();
        segmentToWallKey.Clear();
        wallToSegmentKeys.Clear();
        allSegmentsData.Clear();
        wallMaterials.Clear();
    }

    // ========== HELPER METHODS ==========

    private string GenerateSegmentKey(Vector3 start, Vector3 end)
    {
        Vector3 s = RoundVector(start);
        Vector3 e = RoundVector(end);

        if (s.x > e.x || (s.x == e.x && s.z > e.z))
        {
            var temp = s;
            s = e;
            e = temp;
        }

        return $"seg_{s.x:F2}_{s.z:F2}_{e.x:F2}_{e.z:F2}";
    }

    private Vector3 RoundVector(Vector3 v, int decimals = 2)
    {
        float multiplier = Mathf.Pow(10, decimals);
        return new Vector3(
            Mathf.Round(v.x * multiplier) / multiplier,
            Mathf.Round(v.y * multiplier) / multiplier,
            Mathf.Round(v.z * multiplier) / multiplier
        );
    }

    private Vector3 ProjectPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();

        float dot = Mathf.Clamp(Vector3.Dot(point - lineStart, lineDirection), 0, lineLength);
        return lineStart + lineDirection * dot;
    }
}

[System.Serializable]
public class WallSegment
{
    public Vector3 StartPosition { get; private set; }
    public Vector3 EndPosition { get; private set; }
    public int WallID { get; private set; }
    public int SegmentIndex { get; private set; }
    public int TotalSegments { get; private set; }
    public float Length { get; private set; }

    public WallSegment(Vector3 start, Vector3 end, int wallID, int index, int total)
    {
        StartPosition = start;
        EndPosition = end;
        WallID = wallID;
        SegmentIndex = index;
        TotalSegments = total;
        Length = Vector3.Distance(start, end);
    }

    public Vector3 GetCenter() => (StartPosition + EndPosition) / 2f;
    public Vector3 GetDirection() => (EndPosition - StartPosition).normalized;
    public bool IsVertical() => Mathf.Abs(EndPosition.x - StartPosition.x) < 0.01f;
    public bool IsHorizontal() => Mathf.Abs(EndPosition.z - StartPosition.z) < 0.01f;

    public override string ToString()
    {
        return $"Segment {SegmentIndex}/{TotalSegments}: {StartPosition} -> {EndPosition} (L={Length:F2}m)";
    }
}