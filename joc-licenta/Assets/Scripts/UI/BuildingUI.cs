using UnityEngine;
using UnityEngine.UIElements;
using System.Linq; // NECESAR pentru a căuta în liste (LINQ)

public class BuildingUI : MonoBehaviour
{
    [Header("Referințe Principale")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private ObjectDataBase database; // Referință la baza de date

    [Header("Configurare Nume Obiecte")]
    // Scriem aici numele exact cum apare în Baza de Date (ex: "Floor", "Wall")
    [SerializeField] private string floorObjectName = "Floor";
    [SerializeField] private string wallObjectName = "Wall";

    private Button floorButton;
    private Button wallButton;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // 1. Găsim butoanele în UI
        floorButton = root.Q<Button>("FloorButton");
        wallButton = root.Q<Button>("WallButton");

        // 2. Căutăm ID-urile reale în baza de date
        int actualFloorID = GetIDByName(floorObjectName);
        int actualWallID = GetIDByName(wallObjectName);

        // 3. Conectăm butoanele
        if (floorButton != null)
        {
            if (actualFloorID != -1)
                floorButton.clicked += () => SelectBuildingItem(actualFloorID);
            else
                Debug.LogError($"Nu am găsit obiectul '{floorObjectName}' în baza de date!");
        }

        if (wallButton != null)
        {
            if (actualWallID != -1)
                wallButton.clicked += () => SelectBuildingItem(actualWallID);
            else
                Debug.LogError($"Nu am găsit obiectul '{wallObjectName}' în baza de date!");
        }
    }

    private void OnDisable()
    {
        // De obicei la UI Toolkit e suficient, dar pentru rigurozitate
        // ar trebui să stocăm acțiunile lambda în variabile ca să le putem dezabona.
        // Pentru simplitate aici, ne bazăm pe faptul că obiectul se distruge/dezactivează.
    }

    private void SelectBuildingItem(int id)
    {
        // Pornim logica de plasare
        if (placementSystem != null)
        {
            placementSystem.StartPlacement(id);
        }

        HideBuildPanel();
    }

    private void HideBuildPanel()
    {
        var root = uiDocument.rootVisualElement;
        var panel = root.Q<VisualElement>("BuildView"); // Sau "BuildPannel" (verifică numele în UXML)
        if (panel != null)
        {
            panel.style.display = DisplayStyle.None;
        }
    }

    // --- METODA MAGICĂ ---
    // Caută în lista bazei de date un obiect care are numele specificat
    private int GetIDByName(string name)
    {
        // Verificăm dacă baza de date e asignată
        if (database == null)
        {
            Debug.LogError("BuildingUI: Nu ai asignat ObjectDataBase în Inspector!");
            return -1;
        }

        // Căutăm obiectul (presupunem că clasa ta ObjectData are un câmp 'Name')
        var item = database.objectsData.Find(obj => obj.Name == name);

        if (item != null)
        {
            return item.ID;
        }

        return -1; // Returnăm -1 dacă nu l-am găsit
    }
}