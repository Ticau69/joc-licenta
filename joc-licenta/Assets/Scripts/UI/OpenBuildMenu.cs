using UnityEngine;
using UnityEngine.UIElements;

public class OpenBuildMenu : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private Button openBuildMenuButton;
    private VisualElement buildPanel;

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        openBuildMenuButton = root.Q<Button>("BuildButton");
        buildPanel = root.Q<VisualElement>("BuildView");

        if (openBuildMenuButton != null)
        {
            openBuildMenuButton.clicked += OnBuildClicked;
        }

        // --- FIXUL ESTE AICI ---
        // Forțăm panoul să fie ascuns la pornirea scriptului.
        // Astfel, variabila 'style.display' devine 'None' în mod explicit.
        if (buildPanel != null)
        {
            buildPanel.style.display = DisplayStyle.None;
        }
    }

    private void OnDisable()
    {
        if (openBuildMenuButton != null)
        {
            openBuildMenuButton.clicked -= OnBuildClicked;
        }
    }

    private void OnBuildClicked()
    {
        if (buildPanel == null) return;

        // Verificăm dacă ESTE VIZIBIL (Flex).
        // Dacă e Null sau None, considerăm că e ascuns.
        bool isVisible = buildPanel.style.display == DisplayStyle.Flex;

        if (isVisible)
        {
            // Dacă e vizibil, îl ascundem
            buildPanel.style.display = DisplayStyle.None;
        }
        else
        {
            // Dacă e ascuns (sau Null), îl arătăm
            buildPanel.style.display = DisplayStyle.Flex;
        }
    }
}