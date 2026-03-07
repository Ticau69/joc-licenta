using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelUnlocksSO", menuName = "Scriptable Objects/LevelUnlocksSO")]
public class LevelUnlocksSO : ScriptableObject
{
    public List<LevelUnlock> unlocks = new();
}

[Serializable]
public class LevelUnlock
{
    public int levelRequired = 5;

    [Header("Placement Area")]
    public bool expandPlacementArea = false;
    [Tooltip("Scale target pentru plane (localScale.x/z). Ex: 2 înseamnă dublu.")]
    public Vector2 newPlaneScaleXZ = new Vector2(1f, 1f);

    [Header("Manager Limits")]
    [Tooltip("Dacă valoarea e > 0, va seta limita maximă de angajați (ex: 2 la nivelul 1)")]
    public int maxEmployees = 0;

    [Tooltip("Dacă valoarea e > 0, va seta câți clienți pot exista simultan pe hartă")]
    public int maxCustomers = 0;

    [Tooltip("Dacă valoarea e > 0, va mări spațiul maxim din depozit")]
    public int maxStorageCapacity = 0;

    [Header("Other unlocks (viitor)")]
    public string unlockId;
}