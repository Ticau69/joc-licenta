using System;
using System.Collections.Generic;
using UnityEngine;

public class WallGridData
{
    // Dicționar care stochează pereții folosind o cheie unică
    private Dictionary<string, WallData> placedWalls = new();

    public void AddWall(Vector3 startPos, Vector3 endPos, int ID, GameObject wallObject)
    {
        string key = GenerateWallKey(startPos, endPos);

        if (placedWalls.ContainsKey(key))
        {
            Debug.LogWarning($"Un perete există deja la poziția {key}");
            return;
        }

        WallData data = new WallData(startPos, endPos, ID, wallObject);
        placedWalls[key] = data;
    }

    public bool CanPlaceWall(Vector3 startPos, Vector3 endPos)
    {
        string key = GenerateWallKey(startPos, endPos);
        return !placedWalls.ContainsKey(key);
    }

    public WallData GetWallAt(Vector3 startPos, Vector3 endPos)
    {
        string key = GenerateWallKey(startPos, endPos);

        if (placedWalls.ContainsKey(key))
            return placedWalls[key];

        return null;
    }

    public void RemoveWall(Vector3 startPos, Vector3 endPos)
    {
        string key = GenerateWallKey(startPos, endPos);

        if (placedWalls.ContainsKey(key))
        {
            WallData wall = placedWalls[key];
            if (wall.WallObject != null)
            {
                GameObject.Destroy(wall.WallObject);
            }
            placedWalls.Remove(key);
        }
    }

    public WallData FindWallNearPoint(Vector3 point, float tolerance = 0.1f)
    {
        foreach (var wall in placedWalls.Values)
        {
            // Verificăm dacă punctul este aproape de start sau end
            if (Vector3.Distance(point, wall.StartPosition) < tolerance ||
                Vector3.Distance(point, wall.EndPosition) < tolerance)
            {
                return wall;
            }

            // Verificăm dacă punctul este pe linia peretelui
            Vector3 wallDir = (wall.EndPosition - wall.StartPosition).normalized;
            Vector3 pointDir = (point - wall.StartPosition).normalized;

            float dot = Vector3.Dot(wallDir, pointDir);
            if (dot > 0.99f) // Aproape paralel
            {
                float distToStart = Vector3.Distance(point, wall.StartPosition);
                float wallLength = Vector3.Distance(wall.StartPosition, wall.EndPosition);

                if (distToStart <= wallLength + tolerance)
                {
                    return wall;
                }
            }
        }

        return null;
    }

    public List<WallData> GetAllWalls()
    {
        return new List<WallData>(placedWalls.Values);
    }

    private string GenerateWallKey(Vector3 start, Vector3 end)
    {
        // Rotunjim coordonatele pentru consistență
        Vector3 s = RoundVector(start);
        Vector3 e = RoundVector(end);

        // Normalizăm ordinea (cel mai mic întotdeauna primul)
        if (s.x > e.x || (s.x == e.x && s.z > e.z))
        {
            var temp = s;
            s = e;
            e = temp;
        }

        return $"{s.x:F2}_{s.z:F2}_{e.x:F2}_{e.z:F2}";
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
}

[System.Serializable]
public class WallData
{
    public Vector3 StartPosition { get; private set; }
    public Vector3 EndPosition { get; private set; }
    public int ID { get; private set; }
    public GameObject WallObject { get; private set; }
    public float Length { get; private set; }

    public WallData(Vector3 start, Vector3 end, int id, GameObject wallObj)
    {
        StartPosition = start;
        EndPosition = end;
        ID = id;
        WallObject = wallObj;
        Length = Vector3.Distance(start, end);
    }

    public Vector3 GetMidPoint()
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
}