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


}