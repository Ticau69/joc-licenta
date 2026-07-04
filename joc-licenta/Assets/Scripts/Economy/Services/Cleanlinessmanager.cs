using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manager central pentru sistemul de curățenie.
/// Tracked global — murdăria e reprezentată prin obiecte 3D pe podea.
/// </summary>
public class CleanlinessManager : MonoBehaviour
{
    public static CleanlinessManager Instance { get; private set; }

    [Header("Dirt Settings")]
    [Tooltip("Prefab-urile de gunoi/pete (ales random la spawn).")]
    public List<GameObject> dirtPrefabs = new List<GameObject>();

    [Tooltip("Câte obiecte de gunoi pot exista simultan în magazin.")]
    public int maxDirtObjects = 50;

    [Tooltip("Pragul de murdărie (0-1) peste care inspecția dă amendă.")]
    [Range(0f, 1f)] public float dirtyThreshold = 0.3f;

    [Header("Spawn Settings")]
    [Tooltip("Șansa ca un client să lase gunoi la fiecare pas (0-1).")]
    [Range(0f, 1f)] public float dirtSpawnChancePerStep = 0.02f;

    [Tooltip("Distanța minimă între două obiecte de gunoi.")]
    public float minDistanceBetweenDirt = 0.5f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private List<DirtObject> _dirtObjects = new List<DirtObject>();

    // ── Properties ────────────────────────────────────────────────────────────

    public int DirtCount => _dirtObjects.Count;
    public float DirtPercent => maxDirtObjects > 0
        ? (float)_dirtObjects.Count / maxDirtObjects
        : 0f;
    public bool IsOverThreshold => DirtPercent >= dirtyThreshold;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Încearcă să spaweze un obiect de gunoi la poziția dată.
    /// Returnează true dacă a reușit.
    /// </summary>
    public bool TrySpawnDirt(Vector3 position)
    {
        if (dirtPrefabs.Count == 0) return false;
        if (_dirtObjects.Count >= maxDirtObjects) return false;

        // Verificăm că nu e alt gunoi prea aproape
        foreach (DirtObject existing in _dirtObjects)
        {
            if (existing == null) continue;
            if (Vector3.Distance(existing.transform.position, position) < minDistanceBetweenDirt)
                return false;
        }

        // Spawnam
        GameObject prefab = dirtPrefabs[Random.Range(0, dirtPrefabs.Count)];
        GameObject dirtObj = Instantiate(prefab, position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        DirtObject dirt = dirtObj.GetComponent<DirtObject>();
        if (dirt == null) dirt = dirtObj.AddComponent<DirtObject>();

        _dirtObjects.Add(dirt);
        return true;
    }

    /// <summary>
    /// Returnează cel mai aproape obiect de gunoi față de poziția dată.
    /// </summary>
    public DirtObject GetClosestDirt(Vector3 Position)
    {

        DirtObject closest = null;
        float minDistSqr = float.MaxValue;

        for (int i = _dirtObjects.Count - 1; i >= 0; i++)
        {
            if (_dirtObjects[i] == null)
            {
                _dirtObjects.RemoveAt(i);
                continue;
            }
            float distSqr = (Position - _dirtObjects[i].transform.position).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closest = _dirtObjects[i];
            }
        }
        return closest;
    }

    /// <summary>Apelat de DirtObject când e curățat.</summary>
    public void OnDirtCleaned(DirtObject dirt)
    {
        _dirtObjects.Remove(dirt);
    }

    /// <summary>Curăță referințele null din listă.</summary>
    private void CleanupNullDirt()
    {
        _dirtObjects.RemoveAll(d => d == null);
    }

    /// <summary>Returnează procentul de murdărie ca string formatat.</summary>
    public string GetCleanlinessStatus()
    {
        float cleanPercent = (1f - DirtPercent) * 100f;
        return $"{cleanPercent:F0}%";
    }
}