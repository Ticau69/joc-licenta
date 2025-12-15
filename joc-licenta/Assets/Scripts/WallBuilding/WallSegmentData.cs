using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistem nou care stochează pereții ca segmente independente
/// Permite ștergerea selectivă pentru uși/ferestre
/// </summary>
public class WallSegmentData
{
    // Dicționar: Cheie = Segment ID, Valoare = Date segment
    private Dictionary<string, WallSegment> segments = new();

    // Referință către toate GameObject-urile create
    private Dictionary<string, GameObject> segmentObjects = new();

    // Setări
    private float segmentLength = 1f; // Lungimea unui segment (0.5m)

    public WallSegmentData(float segmentSize = 1f)
    {
        this.segmentLength = segmentSize;
    }

    /// <summary>
    /// Adaugă un perete și îl împarte automat în segmente
    /// </summary>
    public void AddWall(Vector3 startPos, Vector3 endPos, int wallID, GameObject wallPrefab, Material wallMaterial)
    {
        float totalLength = Vector3.Distance(startPos, endPos);
        Vector3 direction = (endPos - startPos).normalized;

        // Calculăm numărul de segmente necesare
        int segmentCount = Mathf.CeilToInt(totalLength / segmentLength);
        float actualSegmentLength = totalLength / segmentCount; // Ajustăm pentru distribuție uniformă

        // Creăm fiecare segment
        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 segStart = startPos + direction * (i * actualSegmentLength);
            Vector3 segEnd = startPos + direction * ((i + 1) * actualSegmentLength);

            string segmentKey = GenerateSegmentKey(segStart, segEnd);

            // Verificăm dacă există deja
            if (segments.ContainsKey(segmentKey))
            {
                Debug.LogWarning($"Segment deja existent: {segmentKey}");
                continue;
            }

            // Creăm segmentul
            WallSegment segment = new WallSegment(segStart, segEnd, wallID, i, segmentCount);
            segments[segmentKey] = segment;

            // Creăm GameObject-ul vizual
            GameObject segmentObj = CreateSegmentObject(segStart, segEnd, wallPrefab, wallMaterial);
            segmentObj.name = $"WallSegment_{segments.Count}_{i}";
            segmentObjects[segmentKey] = segmentObj;

            Debug.Log($"Segment creat: {segmentKey} ({i + 1}/{segmentCount})");
        }
    }

    /// <summary>
    /// Șterge toate segmentele care se suprapun cu o zonă (pentru uși/ferestre)
    /// </summary>
    public List<WallSegment> RemoveSegmentsInRange(Vector3 centerPos, float range, out int removedCount)
    {
        List<WallSegment> removedSegments = new List<WallSegment>();
        List<string> keysToRemove = new List<string>();

        foreach (var kvp in segments)
        {
            WallSegment segment = kvp.Value;
            Vector3 segmentCenter = (segment.StartPosition + segment.EndPosition) / 2f;

            // Verificăm dacă segmentul se află în rază
            float distance = Vector3.Distance(segmentCenter, centerPos);

            if (distance < range)
            {
                keysToRemove.Add(kvp.Key);
                removedSegments.Add(segment);

                // Distrugem GameObject-ul
                if (segmentObjects.ContainsKey(kvp.Key))
                {
                    GameObject.Destroy(segmentObjects[kvp.Key]);
                    segmentObjects.Remove(kvp.Key);
                }
            }
        }

        // Ștergem din dicționar
        foreach (string key in keysToRemove)
        {
            segments.Remove(key);
        }

        removedCount = keysToRemove.Count;
        Debug.Log($"Segmente șterse: {removedCount} în raza de {range}m de la {centerPos}");

        return removedSegments;
    }

    /// <summary>
    /// Găsește toate segmentele unui perete care conțin un punct
    /// </summary>
    public List<WallSegment> FindSegmentsNearPoint(Vector3 point, float tolerance = 0.2f)
    {
        List<WallSegment> nearSegments = new List<WallSegment>();

        foreach (var segment in segments.Values)
        {
            float distToStart = Vector3.Distance(point, segment.StartPosition);
            float distToEnd = Vector3.Distance(point, segment.EndPosition);

            // Verificăm dacă punctul este aproape de segment
            if (distToStart < tolerance || distToEnd < tolerance)
            {
                nearSegments.Add(segment);
            }
            else
            {
                // Verificăm dacă punctul este pe linia segmentului
                Vector3 projected = ProjectPointOnLineSegment(point, segment.StartPosition, segment.EndPosition);
                float distToLine = Vector3.Distance(point, projected);

                if (distToLine < tolerance)
                {
                    nearSegments.Add(segment);
                }
            }
        }

        return nearSegments;
    }

    /// <summary>
    /// Creează un GameObject vizual pentru un segment de perete
    /// </summary>
    private GameObject CreateSegmentObject(Vector3 start, Vector3 end, GameObject prefab, Material material)
    {
        GameObject segmentObj = new GameObject("WallSegment");

        // Adăugăm componentele necesare
        ProceduralWall pWall = segmentObj.AddComponent<ProceduralWall>();
        segmentObj.AddComponent<MeshFilter>();
        segmentObj.AddComponent<MeshRenderer>();
        segmentObj.AddComponent<MeshCollider>();

        // Generăm mesh-ul
        pWall.GenerateWall(start, end);

        // Aplicăm materialul
        if (material != null)
        {
            pWall.SetMaterial(material);
        }

        return segmentObj;
    }

    /// <summary>
    /// Găsește toate segmentele
    /// </summary>
    public List<WallSegment> GetAllSegments()
    {
        return new List<WallSegment>(segments.Values);
    }

    /// <summary>
    /// Găsește un segment specific
    /// </summary>
    public WallSegment GetSegment(string key)
    {
        return segments.ContainsKey(key) ? segments[key] : null;
    }

    /// <summary>
    /// Șterge toate segmentele
    /// </summary>
    public void ClearAll()
    {
        // Distrugem toate GameObject-urile
        foreach (var obj in segmentObjects.Values)
        {
            if (obj != null)
                GameObject.Destroy(obj);
        }

        segments.Clear();
        segmentObjects.Clear();
    }

    // ========== HELPER METHODS ==========

    private string GenerateSegmentKey(Vector3 start, Vector3 end)
    {
        Vector3 s = RoundVector(start);
        Vector3 e = RoundVector(end);

        // Normalizăm ordinea
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

        Vector3 pointVector = point - lineStart;
        float dot = Vector3.Dot(pointVector, lineDirection);
        dot = Mathf.Clamp(dot, 0, lineLength);

        return lineStart + lineDirection * dot;
    }
}

/// <summary>
/// Date despre un segment individual de perete
/// </summary>
[System.Serializable]
public class WallSegment
{
    public Vector3 StartPosition { get; private set; }
    public Vector3 EndPosition { get; private set; }
    public int WallID { get; private set; }
    public int SegmentIndex { get; private set; } // Index în cadrul peretelui complet
    public int TotalSegments { get; private set; } // Câte segmente are peretele complet
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

    public Vector3 GetCenter()
    {
        return (StartPosition + EndPosition) / 2f;
    }

    public Vector3 GetDirection()
    {
        return (EndPosition - StartPosition).normalized;
    }

    public bool IsVertical()
    {
        return Mathf.Abs(EndPosition.x - StartPosition.x) < 0.01f;
    }

    public bool IsHorizontal()
    {
        return Mathf.Abs(EndPosition.z - StartPosition.z) < 0.01f;
    }

    public override string ToString()
    {
        return $"Segment {SegmentIndex}/{TotalSegments}: {StartPosition} -> {EndPosition} (L={Length:F2}m)";
    }
}