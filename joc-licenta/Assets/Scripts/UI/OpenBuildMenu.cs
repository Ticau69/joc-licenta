using UnityEngine;
using UnityEngine.UIElements;

public class OpenBuildMenu : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private Button openBuildMenuButton;
    private VisualElement buildPanel;

    private Button openFurnitureMenuButton;
    private VisualElement furniturePanel;

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        openBuildMenuButton = root.Q<Button>("BuildButton");
        buildPanel = root.Q<VisualElement>("BuildView");

        if (openBuildMenuButton != null)
        {
            openBuildMenuButton.clicked += OnBuildClicked;
        }

        if (buildPanel != null)
        {
            buildPanel.style.display = DisplayStyle.None;
        }

        openFurnitureMenuButton = root.Q<Button>("FurnitureButton");
        furniturePanel = root.Q<VisualElement>("FurnitureView");

        if (openFurnitureMenuButton != null)
        {
            openFurnitureMenuButton.clicked += OnFurnitureClicked;
        }

        if (furniturePanel != null)
        {
            furniturePanel.style.display = DisplayStyle.None;
        }

    }

    private void OnDisable()
    {
        if (openBuildMenuButton != null)
        {
            openBuildMenuButton.clicked -= OnBuildClicked;
        }

        if (openFurnitureMenuButton != null)
        {
            openFurnitureMenuButton.clicked -= OnFurnitureClicked;
        }
    }

    private void OnFurnitureClicked()
    {
        if (furniturePanel == null) return;

        // Verificăm dacă ESTE VIZIBIL (Flex).
        // Dacă e Null sau None, considerăm că e ascuns.
        bool isVisible = furniturePanel.style.display == DisplayStyle.Flex;

        if (isVisible)
        {
            // Dacă e vizibil, îl ascundem
            furniturePanel.style.display = DisplayStyle.None;
        }
        else
        {
            // Dacă e ascuns (sau Null), îl arătăm
            furniturePanel.style.display = DisplayStyle.Flex;
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