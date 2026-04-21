using UnityEngine;

/// <summary>
/// Sistem de inspecție sanitară randomizată în intervalul programului.
/// Abonată la TimeManager pentru a verifica la fiecare oră.
/// </summary>
public class SanitaryInspection : MonoBehaviour
{
    public static SanitaryInspection Instance { get; private set; }

    [Header("Inspection Settings")]
    [Tooltip("Șansa de inspecție per oră de program (ex: 0.15 = 15% per oră).")]
    [Range(0f, 1f)] public float inspectionChancePerHour = 0.15f;

    [Tooltip("Amenda minimă (RON).")]
    public int minFine = 500;

    [Tooltip("Amenda maximă (RON).")]
    public int maxFine = 2000;

    [Tooltip("Cooldown minim între inspecții (ore de joc).")]
    public int minHoursBetweenInspections = 3;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private int _lastInspectionHour = -99;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourChanged += OnHourChanged;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourChanged -= OnHourChanged;
    }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private void OnHourChanged()
    {
        if (TimeManager.Instance == null) return;

        int currentHour = TimeManager.Instance.CurrentHour;

        // Verificăm dacă suntem în intervalul de program
        bool isOpen = currentHour >= TimeManager.Instance.openHour &&
                      currentHour < TimeManager.Instance.closeHour;

        if (!isOpen) return;

        // Cooldown între inspecții
        if (currentHour - _lastInspectionHour < minHoursBetweenInspections) return;

        // Roll pentru inspecție
        if (Random.value > inspectionChancePerHour) return;

        TriggerInspection();
    }

    private void TriggerInspection()
    {
        if (CleanlinessManager.Instance == null) return;

        _lastInspectionHour = TimeManager.Instance.CurrentHour;

        float dirtPercent = CleanlinessManager.Instance.DirtPercent;
        bool isDirty = CleanlinessManager.Instance.IsOverThreshold;
        string cleanStatus = CleanlinessManager.Instance.GetCleanlinessStatus();

        Debug.Log($"[Inspectie] Nivel curatenie: {cleanStatus} | Prag: {CleanlinessManager.Instance.dirtyThreshold * 100f:F0}%");

        if (isDirty)
        {
            // Calculăm amenda proporțional cu murdăria
            float severity = Mathf.InverseLerp(
                CleanlinessManager.Instance.dirtyThreshold, 1f, dirtPercent);
            int fine = Mathf.RoundToInt(Mathf.Lerp(minFine, maxFine, severity));

            ApplyFine(fine, cleanStatus);
        }
        else
        {
            NotifyInspectionPassed(cleanStatus);
        }
    }

    private void ApplyFine(int fine, string cleanStatus)
    {
        // Scădem banii
        if (ServiceLocator.Instance.TryGet(out IMoneyService money))
            money.TrySpend(fine);

        if (FinanceManager.Instance != null)
            FinanceManager.Instance.RegisterTransaction(
                TransactionCategory.Facturi_Utilitati, fine);

        // Notificăm jucătorul
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new ShowNotificationEvent(
                title: "Inspectie Sanitara - SANCTIUNE!",
                message: $"Magazinul tau are un nivel de curatenie de {cleanStatus}.\n" +
                          $"Amenda aplicata: {fine} RON.\n" +
                          $"Angajeaza un Curatator sau curata magazinul!",
                type: NotificationType.Error,
                duration: 8f
            ));
        }

        Debug.LogWarning($"[Inspectie] AMENDA {fine} RON! Curatenie: {cleanStatus}");
    }

    private void NotifyInspectionPassed(string cleanStatus)
    {
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new ShowNotificationEvent(
                title: "Inspectie Sanitara - TRECUT!",
                message: $"Felicitari! Magazinul tau are un nivel de curatenie de {cleanStatus}.\n" +
                          $"Continua sa mentii magazinul curat!",
                type: NotificationType.Success,
                duration: 5f
            ));
        }

        Debug.Log($"[Inspectie] TRECUT! Curatenie: {cleanStatus}");
    }
}