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

    [Header("Inspection Settings (NOU)")]
    [Tooltip("Câte minute reale durează până la PRIMA inspecție.")]
    public float firstInspectionMinutes = 10f;

    [Tooltip("Numărul minim de zile in-game între inspecții (după prima).")]
    public int daysBetweenInspections = 4;

    [Tooltip("Valoarea amenzii dacă magazinul este murdar.")]
    public int fineAmount = 500;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private List<DirtObject> _dirtObjects = new List<DirtObject>();

    // Variabile pentru tracking-ul timpului
    private float _realPlaytimeSeconds = 0f;
    private bool _isFirstInspectionDone = false;
    private int _daysSinceLastInspection = 0;

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

    void Update()
    {
        // 1. Cronometrăm doar până la prima inspecție!
        if (!_isFirstInspectionDone)
        {
            _realPlaytimeSeconds += Time.deltaTime;

            // Dacă au trecut cele 10 minute (convertite în secunde)
            if (_realPlaytimeSeconds >= firstInspectionMinutes * 60f)
            {
                TriggerHealthInspection();
                _isFirstInspectionDone = true;
                _daysSinceLastInspection = 0; // Resetăm contorul de zile pentru următoarele
            }
        }
    }

    // ── Inspecția Sanitară ────────────────────────────────────────────────────

    /// <summary>
    /// Această metodă trebuie apelată de scriptul tău de Timp (DayNightCycle / GameManager)
    /// la finalul sau începutul fiecărei zile in-game!
    /// </summary>
    public void RegisterNewInGameDay()
    {
        // Nu începem să numărăm zilele dacă nu a trecut măcar inspecția de tutorial de 10 minute
        if (!_isFirstInspectionDone) return;

        _daysSinceLastInspection++;

        if (_daysSinceLastInspection >= daysBetweenInspections)
        {
            TriggerHealthInspection();
            _daysSinceLastInspection = 0; // Resetăm pentru a aștepta din nou 4 zile
        }
    }

    private void TriggerHealthInspection()
    {
        Debug.Log("[SANEPID] Inspecția sanitară a sosit în magazin!");

        // Verificăm dacă suntem peste pragul admis folosind proprietatea ta existentă
        if (IsOverThreshold)
        {
            Debug.LogWarning($"[SANEPID] AMENDĂ! Magazinul este {GetCleanlinessStatus()} murdar!");

            // Aici scazi banii jucătorului (integrat cu sistemul tău financiar)
            if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IMoneyService money))
            {
                money.TrySpend(fineAmount);

                if (FinanceManager.Instance != null)
                {
                    FinanceManager.Instance.RegisterTransaction(TransactionCategory.Amenzi, fineAmount);
                }
            }

            // Opțional: Trimitem o notificare pe ecran
            if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            {
                eventBus.Publish(new ShowNotificationEvent(
                    "AMENDĂ SANEPID!",
                    $"Murdărie peste limita admisă ({dirtyThreshold * 100}%). Amendă: {fineAmount} RON.",
                    NotificationType.Error,
                    8f));
            }
        }
        else
        {
            Debug.Log($"[SANEPID] Inspecție trecută cu succes. Murdărie la nivel acceptabil: {GetCleanlinessStatus()}.");

            if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            {
                eventBus.Publish(new ShowNotificationEvent(
                    "Inspecție Sanitară",
                    "Magazinul este curat. Continuați tot așa!",
                    NotificationType.Info,
                    5f));
            }
        }
    }

    // ── Public API (Original) ─────────────────────────────────────────────────

    public bool TrySpawnDirt(Vector3 position)
    {
        if (dirtPrefabs.Count == 0) return false;
        if (_dirtObjects.Count >= maxDirtObjects) return false;

        foreach (DirtObject existing in _dirtObjects)
        {
            if (existing == null) continue;
            if (Vector3.Distance(existing.transform.position, position) < minDistanceBetweenDirt)
                return false;
        }

        GameObject prefab = dirtPrefabs[Random.Range(0, dirtPrefabs.Count)];
        GameObject dirtObj = Instantiate(prefab, position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        DirtObject dirt = dirtObj.GetComponent<DirtObject>();
        if (dirt == null) dirt = dirtObj.AddComponent<DirtObject>();

        _dirtObjects.Add(dirt);
        return true;
    }

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

    public void OnDirtCleaned(DirtObject dirt)
    {
        _dirtObjects.Remove(dirt);
    }

    private void CleanupNullDirt()
    {
        _dirtObjects.RemoveAll(d => d == null);
    }

    public string GetCleanlinessStatus()
    {
        float cleanPercent = DirtPercent * 100f; // Am corectat asta ca să afișeze procentul de murdărie!
        return $"{cleanPercent:F0}%";
    }
}