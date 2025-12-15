using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class BuildingUI : MonoBehaviour
{
    [Header("Referințe")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private ObjectDataBase database;

    [Header("UI Containers")]
    // Aici adaugi în Inspector toate panourile: "BuildView", "FurnitureView", etc.
    [SerializeField] private List<string> containerNames = new List<string>();

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // Iterăm prin FIECARE container definit în listă
        foreach (string panelName in containerNames)
        {
            var container = root.Q<VisualElement>(panelName);

            if (container != null)
            {
                // Găsim toate butoanele din ACEST container
                List<Button> buttons = container.Query<Button>().ToList();
                RegisterButtons(buttons);
            }
            else
            {
                Debug.LogWarning($"BuildingUI: Nu am găsit containerul '{panelName}'");
            }
        }
    }

    private void RegisterButtons(List<Button> buttons)
    {
        foreach (var btn in buttons)
        {
            // Curățăm numele (ex: "TableButton" -> "Table")
            string objectName = btn.name.Replace("Button", "");

            var dbObject = database.objectsData.Find(obj => obj.Name == objectName);

            if (dbObject != null)
            {
                int idToPass = dbObject.ID;
                btn.clicked += () => SelectBuildingItem(idToPass);
            }
        }
    }

    private void SelectBuildingItem(int id)
    {
        if (placementSystem != null)
            placementSystem.StartPlacement(id);

        HideAllPanels();
    }

    private void HideAllPanels()
    {
        var root = uiDocument.rootVisualElement;

        // Ascundem toate panourile din listă
        foreach (string panelName in containerNames)
        {
            var panel = root.Q<VisualElement>(panelName);
            if (panel != null)
                panel.style.display = DisplayStyle.None;
        }
    }
}