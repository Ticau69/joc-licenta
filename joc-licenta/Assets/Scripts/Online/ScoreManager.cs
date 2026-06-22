using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    // Aceasta este variabila pe care o citește meniul de pauză
    public int TotalScore { get; private set; }

    private IEventBus _eventBus;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Ne conectăm automat la EventBus fără să mai depindem de GameManager!
        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out _eventBus))
        {
            _eventBus.Subscribe<ScoreGainedEvent>(OnScoreGained);
            Debug.Log("[ScoreManager] Conectat cu succes la EventBus!");
        }
        else
        {
            Debug.LogError("[ScoreManager] CRITIC: Nu am găsit EventBus în ServiceLocator!");
        }
    }

    private void OnScoreGained(ScoreGainedEvent e)
    {
        TotalScore += e.Amount;
        Debug.Log($"[ScoreManager] +{e.Amount} pct ({e.Source}). Scor Total: {TotalScore}");
    }

    void OnDestroy()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<ScoreGainedEvent>(OnScoreGained);
        }
    }
}