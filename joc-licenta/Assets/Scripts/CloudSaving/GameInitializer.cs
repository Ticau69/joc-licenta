using UnityEngine;

/// <summary>
/// Punctul unic de intrare al aplicației. Persistă între scene.
/// 
/// Responsabilități:
///   • Deține singurul EventBus global al aplicației
///   • Gestionează faza de autentificare (scena de Login)
///   • La încărcarea scenei de joc, injectează EventBus-ul în GameServiceInstaller
///     → elimină nevoia de CloudSaveManager.HookIntoNewEventBus
///
/// Flux:
///   Login scene  →  AuthManager  →  UserAuthenticatedEvent
///                                         ↓
///                              CloudSaveManager.SetUserIdAndLoad()
///                                         ↓
///                              SceneManager.LoadScene("Game")
///                                         ↓
///   Game scene   →  GameServiceInstaller.Install() → GameInitializer.Instance.EventBus
///                                         (același bus, fără injecție manuală)
/// </summary>
public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance { get; private set; }

    [Header("Referințe scenă Login")]
    [SerializeField] private AuthManager authManager;
    [SerializeField] private CloudSaveManager cloudSaveManager;
    [SerializeField] private MainMenuController mainMenuUI;


    /// <summary>EventBus-ul global — partajat cu GameServiceInstaller.</summary>
    public IEventBus EventBus { get; private set; }

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (!InitializeSingleton()) return;

        DontDestroyOnLoad(gameObject);
        EventBus = new EventBus();

    }

    void Start()
    {
        if (authManager == null)
        {
            Debug.LogError("[GameInitializer] AuthManager neluat din Inspector!");
            return;
        }

        authManager.Initialize(EventBus);

        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.Initialize(EventBus);
        else
            Debug.LogWarning("[GameInitializer] CloudSaveManager.Instance este null — nu se poate inițializa.");

        EventBus.Subscribe<UserAuthenticatedEvent>(OnUserAuthenticated);
        EventBus.Subscribe<AuthFailedEvent>(OnAuthFailed);

        mainMenuUI?.Initialize(EventBus, authManager);

        Debug.Log("[GameInitializer] Sisteme de bază pornite. Așteptăm autentificarea...");
    }

    void OnDestroy()
    {
        if (EventBus == null) return;
        EventBus.Unsubscribe<UserAuthenticatedEvent>(OnUserAuthenticated);
        EventBus.Unsubscribe<AuthFailedEvent>(OnAuthFailed);
    }

    // ─── Autentificare ────────────────────────────────────────────────────────

    private void OnUserAuthenticated(UserAuthenticatedEvent e)
    {
        Debug.Log($"[GameInitializer] Autentificat: {e.Username} (ID: {e.UserId})");

        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.SetUserIdAndLoad(e.UserId);
        else
            Debug.LogError("[GameInitializer] CloudSaveManager.Instance este null.");
    }

    private void OnAuthFailed(AuthFailedEvent e)
    {
        Debug.LogError($"[GameInitializer] Autentificare eșuată: {e.ErrorMessage}");
    }

    // ─── Singleton ────────────────────────────────────────────────────────────

    private bool InitializeSingleton()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return false;
        }
        Instance = this;
        return true;
    }
}