using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistem de tracking pentru uși (similar cu WallGridData)
/// Permite ștergerea și gestionarea ușilor
/// </summary>
public class DoorData
{
    // Dicționar: Cheie = Poziție rotunjită, Valoare = Date ușă
    private Dictionary<string, DoorInfo> placedDoors = new();

    /// <summary>
    /// Adaugă o ușă în tracking
    /// </summary>
    public void AddDoor(Vector3 position, Quaternion rotation, int doorID, GameObject doorObject)
    {
        string key = GenerateKey(position);

        if (placedDoors.ContainsKey(key))
        {
            Debug.LogWarning($"O ușă există deja la poziția {key}");
            return;
        }

        DoorInfo info = new DoorInfo(position, rotation, doorID, doorObject);
        placedDoors[key] = info;

        Debug.Log($"Ușă adăugată în tracking: {key}");
    }

    /// <summary>
    /// Găsește o ușă aproape de un punct
    /// </summary>
    public DoorInfo FindDoorNearPoint(Vector3 point, float tolerance = 0.5f)
    {
        foreach (var door in placedDoors.Values)
        {
            float distance = Vector3.Distance(point, door.Position);
            if (distance < tolerance)
            {
                return door;
            }
        }
        return null;
    }

    /// <summary>
    /// Șterge o ușă
    /// </summary>
    public bool RemoveDoor(Vector3 position, float tolerance = 0.5f)
    {
        DoorInfo doorToRemove = FindDoorNearPoint(position, tolerance);

        if (doorToRemove != null)
        {
            string key = GenerateKey(doorToRemove.Position);

            // Distrugem GameObject-ul
            if (doorToRemove.DoorObject != null)
            {
                GameObject.Destroy(doorToRemove.DoorObject);
            }

            placedDoors.Remove(key);
            Debug.Log($"Ușă ștearsă: {key}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returnează toate ușile
    /// </summary>
    public List<DoorInfo> GetAllDoors()
    {
        return new List<DoorInfo>(placedDoors.Values);
    }

    /// <summary>
    /// Verifică dacă există o ușă la poziție
    /// </summary>
    public bool HasDoorAt(Vector3 position, float tolerance = 0.5f)
    {
        return FindDoorNearPoint(position, tolerance) != null;
    }

    /// <summary>
    /// Curăță toate ușile
    /// </summary>
    public void ClearAll()
    {
        foreach (var door in placedDoors.Values)
        {
            if (door.DoorObject != null)
            {
                GameObject.Destroy(door.DoorObject);
            }
        }
        placedDoors.Clear();
    }

    // ========== HELPER METHODS ==========

    private string GenerateKey(Vector3 position)
    {
        Vector3 rounded = RoundVector(position);
        return $"door_{rounded.x:F2}_{rounded.z:F2}";
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

/// <summary>
/// Informații despre o ușă plasată
/// </summary>
[System.Serializable]
public class DoorInfo
{
    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; }
    public int DoorID { get; private set; }
    public GameObject DoorObject { get; private set; }

    public DoorInfo(Vector3 position, Quaternion rotation, int doorID, GameObject doorObject)
    {
        Position = position;
        Rotation = rotation;
        DoorID = doorID;
        DoorObject = doorObject;
    }

    public override string ToString()
    {
        return $"Door at {Position}, rotation: {Rotation.eulerAngles.y}°";
    }
}