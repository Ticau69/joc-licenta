using UnityEngine;
using System.Collections.Generic;

public class LandExpansionManager : MonoBehaviour
{
    public static LandExpansionManager Instance { get; private set; }

    // Lista cu toate zonele fizice blocate din scenă
    private List<ExpansionZone> _lockedZones = new List<ExpansionZone>();

    // Lista cu ID-urile pe care le vom salva în JSON
    public List<string> unlockedPlotIDs = new List<string>();

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

    // NOU: Metodă pentru a înregistra o cumpărătură
    public void MarkAsUnlocked(string plotID)
    {
        if (!unlockedPlotIDs.Contains(plotID))
        {
            unlockedPlotIDs.Add(plotID);
        }
    }

    // NOU: Metodă apelată la încărcarea jocului (Load)
    public void RestoreUnlockedPlots(List<string> savedPlots)
    {
        unlockedPlotIDs = savedPlots ?? new List<string>();

        // Parcurgem invers lista de zone blocate, deoarece LoadUnlock() le va distruge scriptul
        for (int i = _lockedZones.Count - 1; i >= 0; i--)
        {
            ExpansionZone zone = _lockedZones[i];

            // Dacă parcela se află în salvarea din JSON, o deblocăm instant
            if (unlockedPlotIDs.Contains(zone.plotID))
            {
                zone.LoadUnlock();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(12f, 4f, 12f);
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawCube(center, size);
    }
}