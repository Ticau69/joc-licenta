using UnityEngine;
using UnityEngine.UIElements;

public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private UIDocument mainDocument;
    [SerializeField] private VisualTreeAsset notificationCardTemplate;
    [SerializeField] private string containerName = "NotificationContainer"; // Numele containerului din dreapta sus

    private VisualElement _notificationContainer;
    private IEventBus _eventBus;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NotificationManager] Multiple instances detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Optional: Don't destroy on load dacă vrei să persiste între scene
        // DontDestroyOnLoad(gameObject);

        Debug.Log("[NotificationManager] Instance created");
    }

    void Start()
    {
        Debug.Log("[NotificationManager] ═══════════════════════════════");
        Debug.Log("[NotificationManager] Starting NotificationManager...");
        Debug.Log($"[NotificationManager] GameObject active? {gameObject.activeInHierarchy}");
        Debug.Log($"[NotificationManager] Component enabled? {enabled}");

        if (mainDocument == null)
        {
            Debug.LogError("[NotificationManager] mainDocument is NULL!");
            return;
        }

        var root = mainDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("[NotificationManager] rootVisualElement is NULL!");
            return;
        }

        Debug.Log($"[NotificationManager] Root element found: {root.name}");

        _notificationContainer = root.Q<VisualElement>(containerName);

        if (_notificationContainer == null)
        {
            Debug.LogError($"[NotificationManager] Container '{containerName}' NOT FOUND!");
            Debug.Log("[NotificationManager] Available elements in root:");
            ListAllElements(root, 0);
            return;
        }

        Debug.Log($"[NotificationManager] ✓ Container '{containerName}' found!");
        Debug.Log($"[NotificationManager] Container children count: {_notificationContainer.childCount}");

        // EventBus subscription
        try
        {
            _eventBus = ServiceLocator.Instance.Get<IEventBus>();

            if (_eventBus == null)
            {
                Debug.LogError("[NotificationManager] EventBus is NULL!");
                return;
            }

            Debug.Log("[NotificationManager] ✓ EventBus obtained");

            _eventBus.Subscribe<ShowNotificationEvent>(OnNotificationReceived);

            Debug.Log("[NotificationManager] ✓ Subscribed to ShowNotificationEvent");
            Debug.Log("[NotificationManager] ═══════════════════════════════");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NotificationManager] Exception during setup: {e.Message}\n{e.StackTrace}");
        }
    }

    private void ListAllElements(VisualElement parent, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}- {parent.name} ({parent.GetType().Name})");

        foreach (var child in parent.Children())
        {
            ListAllElements(child, depth + 1);
        }
    }

    private void OnNotificationReceived(ShowNotificationEvent evt)
    {
        if (notificationCardTemplate == null) return;

        // 1. Instanțiem cardul din Template
        VisualElement cardInstance = notificationCardTemplate.Instantiate();

        // 2. Îl adăugăm în containerul din dreapta-sus
        _notificationContainer.Add(cardInstance);

        // 3. Predăm controlul Controller-ului cardului
        // (Nu trebuie să salvăm controller-ul nicăieri, el trăiește cât trăiește UI-ul)
        new NotificationCardController(cardInstance, evt);
    }

    void OnDestroy()
    {
        _eventBus?.Unsubscribe<ShowNotificationEvent>(OnNotificationReceived);
    }
}