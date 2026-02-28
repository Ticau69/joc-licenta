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

    [Header("Other unlocks (viitor)")]
    public string unlockId; // ex: "Unlock_ShelfTier2" / "Unlock_NewProductMilk"
}