using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [Header("Referințe UI Toolkit")]
    [SerializeField] private UIDocument mainMenuDoc;
    [SerializeField] private UIDocument gameUIDoc;

    private IEventBus _eventBus;
    private AuthManager _authManager;

    // Panouri
    private VisualElement _loginPanel;
    private VisualElement _registerPanel;

    // Input-uri Login
    private TextField _loginEmailInput;
    private TextField _loginPasswordInput;
    private Button _loginButton;

    // Input-uri Register
    private TextField _registerNameInput;
    private TextField _registerEmailInput;
    private TextField _registerPasswordInput;
    private Button _registerButton;

    // Comune
    private Button _quitButton;
    private Label _errorLabel;

    public void Initialize(IEventBus eventBus, AuthManager authManager)
    {
        _eventBus = eventBus;
        _authManager = authManager;

        _eventBus.Subscribe<UserAuthenticatedEvent>(OnAuthSuccess);
        _eventBus.Subscribe<AuthFailedEvent>(OnAuthFailed);
    }

    void Start()
    {
        Time.timeScale = 0f;

        if (mainMenuDoc != null && mainMenuDoc.rootVisualElement != null)
        {
            var root = mainMenuDoc.rootVisualElement;
            root.style.display = DisplayStyle.Flex;

            // Extragem panourile
            _loginPanel = root.Q<VisualElement>("LoginPanel");
            _registerPanel = root.Q<VisualElement>("RegisterPanel");

            // Extragem elementele pentru Login
            _loginEmailInput = root.Q<TextField>("LoginEmailInput");
            _loginPasswordInput = root.Q<TextField>("LoginPasswordInput");
            _loginButton = root.Q<Button>("LoginButton");

            // Extragem elementele pentru Register
            _registerNameInput = root.Q<TextField>("RegisterNameInput");
            _registerEmailInput = root.Q<TextField>("RegisterEmailInput");
            _registerPasswordInput = root.Q<TextField>("RegisterPasswordInput");
            _registerButton = root.Q<Button>("RegisterButton");

            // Extragem elementele comune
            _errorLabel = root.Q<Label>("ErrorLabel");
            _quitButton = root.Q<Button>("QuitButton");

            // Legăm butoanele de comutare între panouri
            root.Q<Button>("GoToRegisterBtn").clicked += ShowRegisterPanel;
            root.Q<Button>("GoToLoginBtn").clicked += ShowLoginPanel;

            // Legăm acțiunile principale
            _loginButton.clicked += OnLoginClicked;
            _registerButton.clicked += OnRegisterClicked;
            _quitButton.clicked += OnQuitClicked;
        }

        if (gameUIDoc != null)
        {
            gameUIDoc.rootVisualElement.style.display = DisplayStyle.None;
        }
    }

    private void ShowRegisterPanel()
    {
        _loginPanel.style.display = DisplayStyle.None;
        _registerPanel.style.display = DisplayStyle.Flex;
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;
    }

    private void ShowLoginPanel()
    {
        _registerPanel.style.display = DisplayStyle.None;
        _loginPanel.style.display = DisplayStyle.Flex;
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;
    }

    private async void OnRegisterClicked()
    {
        if (_authManager == null) return;

        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;

        string email = _registerEmailInput.value ?? "";
        string password = _registerPasswordInput.value ?? "";
        string username = _registerNameInput.value ?? "";

        if (string.IsNullOrEmpty(username))
        {
            ShowError("Introdu un nume pentru profil!");
            return;
        }

        if (string.IsNullOrEmpty(email) || !email.Contains("@"))
        {
            ShowError("Introdu o adresă de email validă (cu @)!");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Introdu o parolă!");
            return;
        }

        // 1. Schimbăm textul pentru a arăta progresul creării contului
        _registerButton.text = "Se creează contul...";

        // 2. Așteptăm ca Firebase să creeze utilizatorul și să îi seteze DisplayName-ul
        await _authManager.RegisterWithEmailAsync(email, password, username);

        // 3. LOGARE AUTOMATĂ: Deoarece operațiunea de sus s-a terminat fără erori,
        // chemăm imediat metoda de Login. Aceasta va declanșa descărcarea stării inițiale 
        // din baza de date și va ascunde automat meniul principal, pornind jocul!
        _registerButton.text = "Se conectează...";
        await _authManager.LoginWithEmailAsync(email, password);

        // Resetăm textul la valoarea inițială în caz de deconectare ulterioară
        _registerButton.text = "REGISTER";
    }

    private async void OnLoginClicked()
    {
        if (_authManager == null) return;

        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;

        string email = _loginEmailInput.value ?? "";
        string password = _loginPasswordInput.value ?? "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Completati ambele câmpuri!");
            return;
        }

        _loginButton.text = "Se conectează...";
        await _authManager.LoginWithEmailAsync(email, password);
        _loginButton.text = "LOGIN";
    }

    private void ShowError(string message)
    {
        if (_errorLabel != null)
        {
            _errorLabel.text = message;
            _errorLabel.style.display = DisplayStyle.Flex;
        }
    }

    private void OnAuthSuccess(UserAuthenticatedEvent eventData)
    {
        Debug.Log($"[UI] Succes: {eventData.Username}");
        if (mainMenuDoc != null) mainMenuDoc.rootVisualElement.style.display = DisplayStyle.None;
        if (gameUIDoc != null) gameUIDoc.rootVisualElement.style.display = DisplayStyle.Flex;
        Time.timeScale = 1f;
    }

    private void OnAuthFailed(AuthFailedEvent eventData)
    {
        _errorLabel.text = eventData.ErrorMessage;
        _errorLabel.style.display = DisplayStyle.Flex;
    }

    private void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDestroy()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<UserAuthenticatedEvent>(OnAuthSuccess);
            _eventBus.Unsubscribe<AuthFailedEvent>(OnAuthFailed);
        }
    }
}