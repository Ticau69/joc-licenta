using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class TimeControlManager : MonoBehaviour
{
    [SerializeField] private InputActionReference pauseAction;
    private Button timeSpeedButton;
    private VisualElement pauseBorder;

    // 0 = Pauză, 1 = 1x (Normal), 2 = 1.5x (Rapid), 3 = 2x (Foarte rapid)
    private int currentState = 1;
    private int previousState = 1;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // Găsim butonul în UI
        timeSpeedButton = root.Q<Button>("TimeSpeedButton");
        pauseBorder = root.Q<VisualElement>("PauseBorder");

        if (timeSpeedButton != null)
        {
            timeSpeedButton.clicked += CycleTimeSpeed;
            UpdateTimeUI(); // Setăm textul și culorile la start
        }

        if (pauseAction != null)
        {
            pauseAction.action.Enable();
            pauseAction.action.performed += OnPauseInputPerformed;
        }
    }

    private void OnPauseInputPerformed(InputAction.CallbackContext context)
    {
        TogglePauseLogic();
    }

    private void TogglePauseLogic()
    {
        if (currentState == 0)
        {
            currentState = previousState;
            if (currentState == 0) currentState = 1;
        }
        else
        {
            previousState = currentState;
            currentState = 0;
        }

        UpdateTimeUI();
    }
    private void CycleTimeSpeed()
    {
        currentState++;
        if (currentState > 3) currentState = 0;
        UpdateTimeUI();
    }

    private void UpdateTimeUI()
    {
        if (timeSpeedButton == null) return;

        switch (currentState)
        {
            case 0: // PAUZĂ
                Time.timeScale = 0f;
                timeSpeedButton.text = "⏸";
                // Îi dăm o nuanță roșiatică să atragă atenția că e pe pauză
                timeSpeedButton.style.backgroundColor = new StyleColor(new Color(0.7f, 0.2f, 0.2f));

                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.Flex; // Afișăm bordura de pauză
                break;

            case 1: // NORMAL
                Time.timeScale = 1f;
                timeSpeedButton.text = "▶";
                timeSpeedButton.style.backgroundColor = new StyleColor(new Color(0.23f, 0.23f, 0.23f)); // Gri

                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.None; // Ascundem bordura de pauză
                break;

            case 2: // RAPID
                Time.timeScale = 1.5f;
                timeSpeedButton.text = "⏩";
                timeSpeedButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.2f)); // Verde

                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.None; // Ascundem bordura de pauză
                break;

            case 3: // SUPER RAPID
                Time.timeScale = 2f;
                timeSpeedButton.text = "⏭";
                timeSpeedButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.8f)); // Albastru

                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.None; // Ascundem bordura de pauză
                break;
        }
    }

    private void OnDisable()
    {
        // Sistem de siguranță: Când se închide HUD-ul sau jucătorul iese în Main Menu,
        // ne asigurăm că timpul revine la normal, altfel restul jocului va rămâne blocat!
        Time.timeScale = 1f;
    }
}
