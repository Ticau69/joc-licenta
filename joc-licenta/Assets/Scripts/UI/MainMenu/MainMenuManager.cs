using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [Header("Referințe UI Toolkit")]
    [SerializeField] private UIDocument mainMenuDoc;
    [SerializeField] private UIDocument gameUIDoc;

    private Button playButton;
    private Button quitButton;

    void Start()
    {
        Time.timeScale = 0f; // Oprim timpul la start

        // 1. Pregătim Meniul Principal
        if (mainMenuDoc != null)
        {
            if (mainMenuDoc.enabled == false)
                mainMenuDoc.enabled = true;

            var root = mainMenuDoc.rootVisualElement;

            // Facem meniul vizibil
            root.style.display = DisplayStyle.Flex;

            // Conectăm butoanele
            playButton = root.Q<Button>("PlayButton");
            quitButton = root.Q<Button>("QuitButton");

            if (playButton != null) playButton.clicked += OnPlayClicked;
            if (quitButton != null) quitButton.clicked += OnQuitClicked;
        }

        // 2. Ascundem UI-ul Jocului (Fără să-l dezactivăm!)
        // Astfel, ClockManager și GameManager pot găsi label-urile în spate și le pot actualiza.
        if (gameUIDoc != null)
        {
            gameUIDoc.rootVisualElement.style.display = DisplayStyle.None;
        }
    }

    private void OnPlayClicked()
    {
        // 1. Ascundem Meniul
        if (mainMenuDoc != null)
            mainMenuDoc.rootVisualElement.style.display = DisplayStyle.None;

        // 2. Arătăm Jocul
        if (gameUIDoc != null)
            gameUIDoc.rootVisualElement.style.display = DisplayStyle.Flex;

        // 3. Pornim timpul
        Time.timeScale = 1f;
        Debug.Log("Jocul a început!");
    }

    private void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}