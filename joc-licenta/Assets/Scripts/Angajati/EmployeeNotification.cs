using UnityEngine;
using UnityEngine.UIElements;

public class EmployeeNotification : MonoBehaviour
{
    [Header("Configurare")]
    [Tooltip("Asigură-te că acest UIDocument are un PanelSettings setat pe 'World Space'")]
    public UIDocument notificationDocument;
    [Header("Configurare Audio")] // --- AUDIO ---
    [SerializeField] private AudioClip clickSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private VisualElement root;
    private Button clickButton;
    private string currentProblemDescription;
    private Employee employee;
    private IEventBus eventBus;

    void Awake()
    {
        employee = GetComponent<Employee>();

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeNotification] Awake - Employee: {(employee != null ? employee.employeeName : "NULL")}");
        }
    }

    private void OnEnable()
    {
        // Încercăm să obținem EventBus
        try
        {
            eventBus = ServiceLocator.Instance.Get<IEventBus>();

            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeNotification] EventBus obtained: {(eventBus != null ? "SUCCESS" : "FAILED")}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EmployeeNotification] Failed to get EventBus: {e.Message}");
        }

        // Setup UI
        if (notificationDocument != null)
        {
            root = notificationDocument.rootVisualElement;

            if (root == null)
            {
                Debug.LogError($"[EmployeeNotification] Root element is NULL for {gameObject.name}");
                return;
            }

            // IMPORTANT: Ascundem inițial
            root.style.display = DisplayStyle.None;

            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeNotification] Root found, initially hidden");
            }

            // Setup buton
            clickButton = root.Q<Button>("NotificationButton");

            if (clickButton != null)
            {
                clickButton.RegisterCallback<ClickEvent>(OnNotificationClicked);

                if (showDebugLogs)
                {
                    Debug.Log($"[EmployeeNotification] Button '{clickButton.name}' found and registered");
                }
            }
            else
            {
                Debug.LogError($"[EmployeeNotification] Nu am găsit butonul 'NotificationButton' în UXML pentru {gameObject.name}");

                // Listează toate elementele pentru debug
                ListAllUIElements(root);
            }
        }
        else
        {
            Debug.LogError($"[EmployeeNotification] notificationDocument is NULL for {gameObject.name}");
        }
    }

    private void ListAllUIElements(VisualElement parent, int depth = 0)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}Element: {parent.name} (type: {parent.GetType().Name})");

        foreach (var child in parent.Children())
        {
            ListAllUIElements(child, depth + 1);
        }
    }

    public void ShowNotification(string description)
    {
        if (root == null)
        {
            Debug.LogError($"[EmployeeNotification] Cannot show notification - root is NULL");
            return;
        }

        currentProblemDescription = description;
        root.style.display = DisplayStyle.Flex;

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeNotification] Showing notification: '{description}' for {employee?.employeeName}");
        }
    }

    public void HideNotification()
    {
        if (root != null)
        {
            root.style.display = DisplayStyle.None;

            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeNotification] Notification hidden for {employee?.employeeName}");
            }
        }
    }

    private void OnNotificationClicked(ClickEvent evt)
    {
        SoundFXManager.Instance.PlaySound(SoundType.UiClick); // Redăm sunetul de click

        Debug.Log($"[EmployeeNotification] ★★★ CLICK DETECTED ★★★ on {employee?.employeeName}");

        if (eventBus == null)
        {
            Debug.LogError($"[EmployeeNotification] Cannot send notification - EventBus is NULL!");

            // Încearcă să obții EventBus din nou
            try
            {
                eventBus = ServiceLocator.Instance.Get<IEventBus>();
                Debug.Log($"[EmployeeNotification] Retry EventBus: {(eventBus != null ? "SUCCESS" : "FAILED")}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EmployeeNotification] EventBus retry failed: {e.Message}");
                return;
            }
        }

        if (employee == null)
        {
            Debug.LogError($"[EmployeeNotification] Cannot send notification - Employee is NULL!");
            return;
        }

        // Construiește evenimentul
        string titlu = $"Problemă: {employee.employeeName}";
        string mesaj = currentProblemDescription ?? "Problemă necunoscută";

        Debug.Log($"[EmployeeNotification] Publishing event - Title: '{titlu}', Message: '{mesaj}'");

        try
        {
            // TRIMITE EVENTUL
            eventBus.Publish(new ShowNotificationEvent
            {
                Title = titlu,
                Message = mesaj,
                Type = NotificationType.Warning,
                Duration = 5f
            });

            Debug.Log($"[EmployeeNotification] ✓ Event published successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EmployeeNotification] Failed to publish event: {e.Message}");
        }

        // Ascunde notificarea
        HideNotification();
    }

    void OnDisable()
    {
        // 1. Verificăm dacă referința la buton există.
        // (Este posibil ca UI-ul să fi fost distrus deja de Unity, caz în care nu trebuie să facem nimic)
        if (clickButton != null)
        {
            // 2. DEZABONARE CORECTĂ:
            // Folosim exact același NUME de funcție pe care l-am folosit la RegisterCallback.
            // FOARTE IMPORTANT: Nu punem paranteze () la finalul numelui funcției.
            clickButton.UnregisterCallback<ClickEvent>(OnNotificationClicked);
        }
    }
}