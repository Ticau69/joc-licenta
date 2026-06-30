using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class TimeControlManager : MonoBehaviour
{
    [SerializeField] private InputActionReference pauseAction;

    [Header("Sprite-uri Stare Timp")]
    [SerializeField] private Sprite pauseSprite;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite fastSprite;
    [SerializeField] private Sprite superFastSprite;

    [Header("Mărimi Buton (Lățime x Înălțime)")]
    [Tooltip("Ajustează aici dimensiunile dorite pentru fiecare pictogramă.")]
    [SerializeField] private Vector2 pauseSize = new Vector2(40, 40);
    [SerializeField] private Vector2 normalSize = new Vector2(40, 40);
    [SerializeField] private Vector2 fastSize = new Vector2(50, 40);
    [SerializeField] private Vector2 superFastSize = new Vector2(60, 40);

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
            // Ștergem textul ca să lăsăm loc doar pentru Sprite
            timeSpeedButton.text = "";

            timeSpeedButton.clicked += CycleTimeSpeed;
            UpdateTimeUI(); // Setăm imaginea și culorile la start
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
                SetButtonVisuals(pauseSprite, pauseSize, new Color(0.7f, 0.2f, 0.2f)); // Roșiatic
                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.Flex;
                break;

            case 1: // NORMAL
                Time.timeScale = 1f;
                SetButtonVisuals(normalSprite, normalSize, new Color(0.23f, 0.23f, 0.23f)); // Gri
                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.None;
                break;

            case 2: // RAPID
                Time.timeScale = 1.5f;
                SetButtonVisuals(fastSprite, fastSize, new Color(0.2f, 0.6f, 0.2f)); // Verde
                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.None;
                break;

            case 3: // SUPER RAPID
                Time.timeScale = 2f;
                SetButtonVisuals(superFastSprite, superFastSize, new Color(0.2f, 0.5f, 0.8f)); // Albastru
                if (pauseBorder != null) pauseBorder.style.display = DisplayStyle.None;
                break;
        }
    }

    // Funcție nouă care face exact ce ai cerut: modifică DOAR imaginea de fundal!
    private void SetButtonVisuals(Sprite bgSprite, Vector2 bgSize, Color bgColor)
    {
        // 1. Schimbăm imaginea
        timeSpeedButton.style.backgroundImage = new StyleBackground(bgSprite);

        // 2. Modificăm proprietatea 'Size' a background-ului (folosind % exact ca în UI Builder)
        timeSpeedButton.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(
            new Length(bgSize.x, LengthUnit.Percent),
            new Length(bgSize.y, LengthUnit.Percent)
        ));

        // 3. Schimbăm culoarea de fundal a butonului
        timeSpeedButton.style.backgroundColor = new StyleColor(bgColor);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }
}