using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument pauseMenuDoc;
    [SerializeField] private VisualTreeAsset leaderboardRowTemplate;

    [Header("Input Settings")]
    [SerializeField] private InputActionReference openPauseMenuAction;

    private VisualElement root;
    private Button resumeButton;
    private Button saveButton;
    private Button exitButton;

    // Referințe noi pentru Leaderboard
    private Button leaderboardButton;
    private Button buttonCloseLeaderboard;
    private VisualElement leaderboardSection;

    private bool isPaused = false;

    private void OnEnable()
    {
        if (openPauseMenuAction != null)
        {
            openPauseMenuAction.action.Enable();
            openPauseMenuAction.action.performed += OnPauseActionPerformed;
        }
    }

    private void OnDisable()
    {
        if (openPauseMenuAction != null)
        {
            openPauseMenuAction.action.performed -= OnPauseActionPerformed;
            openPauseMenuAction.action.Disable();
        }
    }

    void Start()
    {
        if (pauseMenuDoc == null)
        {
            Debug.LogError("[PauseMenu] UIDocument nu este asignat!");
            return;
        }

        root = pauseMenuDoc.rootVisualElement;
        root.style.display = DisplayStyle.None;

        resumeButton = root.Q<Button>("ResumeButton");
        saveButton = root.Q<Button>("SaveButton");
        exitButton = root.Q<Button>("ExitButton");

        // Extragem noile elemente
        leaderboardButton = root.Q<Button>("LeaderboardButton");
        leaderboardSection = root.Q<VisualElement>("LeaderboardSection");
        buttonCloseLeaderboard = root.Q<Button>("ExitButtonLeaderBoard");

        if (resumeButton != null) resumeButton.clicked += TogglePause;
        if (saveButton != null) saveButton.clicked += OnSaveClicked;
        if (exitButton != null) exitButton.clicked += OnExitClicked;

        // Legăm acțiunea pe noul buton
        if (leaderboardButton != null) leaderboardButton.clicked += ToggleLeaderboard;
        if (buttonCloseLeaderboard != null) buttonCloseLeaderboard.clicked += ToggleLeaderboard;
    }

    private void OnPauseActionPerformed(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    private void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            root.style.display = DisplayStyle.Flex;
            Time.timeScale = 0f;

            // Ascundem clasamentul de fiecare dată când deschidem pauza
            if (leaderboardSection != null)
                leaderboardSection.style.display = DisplayStyle.None;
        }
        else
        {
            root.style.display = DisplayStyle.None;
            Time.timeScale = 1f;
        }
    }

    // Funcția care ascunde/arată clasamentul și descarcă datele
    private void ToggleLeaderboard()
    {
        if (leaderboardSection == null) return;

        // Dacă e ascuns, îl arătăm și populăm lista
        if (leaderboardSection.style.display == DisplayStyle.None)
        {
            leaderboardSection.style.display = DisplayStyle.Flex;
            PopulateLeaderboard();
        }
        else
        {
            // Dacă e deja vizibil, îl ascundem la loc (ca un Toggle)
            leaderboardSection.style.display = DisplayStyle.None;
        }
    }

    private async void PopulateLeaderboard()
    {
        var listContainer = root.Q<ScrollView>("LeaderboardList");
        if (listContainer == null) return;

        listContainer.Clear();
        listContainer.Add(new Label("Se descarcă clasamentul...") { style = { color = Color.gray, fontSize = 12 } });

        if (LeaderboardManager.Instance == null)
        {
            listContainer.Clear();
            listContainer.Add(new Label("Eroare: LeaderboardManager indisponibil") { style = { color = Color.red } });
            return;
        }

        if (leaderboardRowTemplate == null)
        {
            listContainer.Clear();
            listContainer.Add(new Label("Eroare: Șablonul UXML lipsește în Inspector") { style = { color = Color.red } });
            return;
        }

        var topScores = await LeaderboardManager.Instance.GetGlobalTopScoresAsync();

        listContainer.Clear();
        int rank = 1;

        // NOU: Preluăm numele utilizatorului curent conectat la Firebase
        var currentUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        string activePlayerName = (currentUser != null && !string.IsNullOrEmpty(currentUser.DisplayName))
            ? currentUser.DisplayName
            : "";

        foreach (var entry in topScores)
        {
            VisualElement rowInstance = leaderboardRowTemplate.Instantiate();
            LeaderboardRowController rowController = new LeaderboardRowController(rowInstance);

            // Verificăm dacă numele din clasament coincide cu numele tău salvat
            string displayName = entry.Name;
            if (!string.IsNullOrEmpty(activePlayerName) && entry.Name == activePlayerName)
            {
                displayName += " (TU)";
            }

            // Trimitem numele (modificat sau nu) către șablon
            rowController.SetData(rank, displayName, entry.Score);

            listContainer.Add(rowInstance);
            rank++;
        }
    }

    private void OnSaveClicked()
    {
        saveButton.text = "SALVAT!";

        // 1. Salvarea normală a jocului (bani, inventar, angajați etc.)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameSave();
        }

        // 2. NOU: Trimitem și scorul pe Leaderboard-ul global!
        if (LeaderboardManager.Instance != null && ScoreManager.Instance != null)
        {
            var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
            // Preluăm numele real (dacă există) sau punem un default
            string playerName = (user != null && !string.IsNullOrEmpty(user.DisplayName)) ? user.DisplayName : "Manager Anonim";

            // ATENȚIE: Aici folosești variabila corectă din ScoreManager-ul tău (ex: TotalScore, CurrentScore etc.)
            LeaderboardManager.Instance.UploadPlayerScore(playerName, ScoreManager.Instance.TotalScore);
        }

        Invoke(nameof(ResetSaveButtonText), 2f);
    }

    private void ResetSaveButtonText()
    {
        if (saveButton != null) saveButton.text = "SALVEAZĂ JOCUL";
    }

    private void OnExitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}