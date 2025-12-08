using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistem care detectează și vizualizează colțurile între pereți
/// </summary>
public class WallConnector : MonoBehaviour
{
    [SerializeField] private GameObject cornerPrefab; // Prefab pentru colț (opțional)
    [SerializeField] private float detectionRadius = 0.5f;

    private WallGridData wallData;
    private List<GameObject> cornerObjects = new List<GameObject>();

    public void Initialize(WallGridData data)
    {
        wallData = data;
    }

    /// <summary>
    /// Verifică toate pereții și creează conexiuni la colțuri
    /// </summary>
    public void UpdateConnections()
    {
        ClearCorners();

        var walls = wallData.GetAllWalls();

        // Găsim toate punctele de intersecție
        Dictionary<Vector3, List<WallData>> intersections = new Dictionary<Vector3, List<WallData>>();

        foreach (var wall in walls)
        {
            // Adăugăm punctul de start
            AddIntersectionPoint(intersections, wall.StartPosition, wall);
            // Adăugăm punctul de end
            AddIntersectionPoint(intersections, wall.EndPosition, wall);
        }

        // Creăm colțuri acolo unde se întâlnesc 2+ pereți
        foreach (var kvp in intersections)
        {
            if (kvp.Value.Count >= 2)
            {
                CreateCorner(kvp.Key, kvp.Value);
            }
        }
    }

    private void AddIntersectionPoint(Dictionary<Vector3, List<WallData>> dict, Vector3 point, WallData wall)
    {
        // Căutăm dacă există deja un punct similar
        Vector3 existingPoint = Vector3.zero;
        bool found = false;

        foreach (var key in dict.Keys)
        {
            if (Vector3.Distance(key, point) < detectionRadius)
            {
                existingPoint = key;
                found = true;
                break;
            }
        }

        if (found)
        {
            dict[existingPoint].Add(wall);
        }
        else
        {
            dict[point] = new List<WallData> { wall };
        }
    }

    private void CreateCorner(Vector3 position, List<WallData> connectedWalls)
    {
        if (cornerPrefab != null)
        {
            GameObject corner = Instantiate(cornerPrefab, position, Quaternion.identity);
            corner.transform.parent = transform;
            cornerObjects.Add(corner);

            Debug.Log($"Colț creat la {position} cu {connectedWalls.Count} pereți conectați");
        }
    }

    private void ClearCorners()
    {
        foreach (var corner in cornerObjects)
        {
            if (corner != null)
                Destroy(corner);
        }
        cornerObjects.Clear();
    }

    /// <summary>
    /// Detectează dacă două pereți formează un colț perfect (90°)
    /// </summary>
    public bool IsPerfectCorner(WallData wall1, WallData wall2, out Vector3 cornerPoint)
    {
        cornerPoint = Vector3.zero;

        // Verificăm toate combinațiile de puncte
        Vector3[] points1 = { wall1.StartPosition, wall1.EndPosition };
        Vector3[] points2 = { wall2.StartPosition, wall2.EndPosition };

        foreach (var p1 in points1)
        {
            foreach (var p2 in points2)
            {
                if (Vector3.Distance(p1, p2) < detectionRadius)
                {
                    // Verificăm dacă sunt perpendiculare
                    Vector3 dir1 = wall1.GetDirection();
                    Vector3 dir2 = wall2.GetDirection();

                    float dot = Mathf.Abs(Vector3.Dot(dir1, dir2));

                    if (dot < 0.1f) // Aproape perpendicular (90°)
                    {
                        cornerPoint = p1;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (wallData == null) return;

        var walls = wallData.GetAllWalls();

        // Desenăm toate conexiunile
        Gizmos.color = Color.cyan;
        foreach (var wall in walls)
        {
            Gizmos.DrawSphere(wall.StartPosition, 0.15f);
            Gizmos.DrawSphere(wall.EndPosition, 0.15f);
        }
    }
}