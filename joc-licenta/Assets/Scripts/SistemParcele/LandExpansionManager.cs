using UnityEngine;
using System.Collections.Generic;

public class LandExpansionManager : MonoBehaviour
{
    public static LandExpansionManager Instance { get; private set; }

    // Lista cu toate zonele fizice blocate din scenă
    private List<ExpansionZone> _lockedZones = new List<ExpansionZone>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterZone(ExpansionZone zone)
    {
        if (!_lockedZones.Contains(zone)) _lockedZones.Add(zone);
    }

    public void UnregisterZone(ExpansionZone zone)
    {
        if (_lockedZones.Contains(zone)) _lockedZones.Remove(zone);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(12f, 4f, 12f); // Dublul lui halfExtents (6, 2, 6)

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Un roșu transparent
        Gizmos.DrawCube(center, size);
    }


}