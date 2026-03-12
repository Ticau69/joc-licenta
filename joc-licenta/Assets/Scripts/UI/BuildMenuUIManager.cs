using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildMenuUIManager : MonoBehaviour
{
    [Header("Referințe Externe")]
    public UIDocument mainHUDDocument; // HUD-ul cu butonul de deschidere

    [Header("Bază de Date Echipamente")]
    [Tooltip("Trage aici baza de date ObjectDataBase")]
    public ObjectDataBase database;

    [Header("UI Templates")]
    public VisualTreeAsset furnitureButtonTemplate; // Butonul mic din lista din stânga

    private ObjectCategory _currentCategory = ObjectCategory.Equipment;

    // UI Elements
    private VisualElement _buildPanel;
    private VisualElement _compatibilitySection;
    private ScrollView _furnitureListScroll;
    private VisualElement _infoPanel;
    private Button _tabEquipmentButton;
    private Button _tabStructureButton;

    // Info Panel Elements
    private Label _infoTitle;
    private Label _infoDescription;
    private Label _infoProducts;
    private Label _infoCost;
    private VisualElement _infoIcon;
    private Button _buildConfirmButton;
    private Label _lockedWarningText;

    private Button _closeButton;
    private Button _hudBuildButton;
    private Button _hudFurnitureButton;

    private ObjectData _selectedFurniture;

    private void Start()
    {
        // Mutat din OnEnable pentru siguranță maximă
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Subscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _buildPanel = root.Q<VisualElement>("BuildMenuContainer");
        if (_buildPanel == null) return;

        _buildPanel.style.display = DisplayStyle.None;

        // Găsim elementele din stânga
        _furnitureListScroll = _buildPanel.Q<ScrollView>("FurnitureListScrollView");
        _closeButton = _buildPanel.Q<Button>("CloseMenuButton");

        // Găsim elementele din dreapta (Info Panel)
        _infoPanel = _buildPanel.Q<VisualElement>("InfoPanel");
        _compatibilitySection = _infoPanel.Q<VisualElement>("CompatibilitySection");
        _infoTitle = _infoPanel.Q<Label>("InfoTitle");
        _infoDescription = _infoPanel.Q<Label>("InfoDescription");
        _infoProducts = _infoPanel.Q<Label>("InfoProducts");
        _infoCost = _infoPanel.Q<Label>("InfoCost");
        _infoIcon = _infoPanel.Q<VisualElement>("InfoIcon");
        _buildConfirmButton = _infoPanel.Q<Button>("BuildConfirmButton");
        _lockedWarningText = _infoPanel.Q<Label>("LockedWarningText");

        _tabEquipmentButton = _buildPanel.Q<Button>("FurnitureButton");
        _tabStructureButton = _buildPanel.Q<Button>("BuildButton");

        if (_closeButton != null) _closeButton.clicked += ClosePanel;
        if (_buildConfirmButton != null) _buildConfirmButton.clicked += OnBuildConfirmed;

        if (_tabEquipmentButton != null)
            _tabEquipmentButton.clicked += () => SwitchCategory(ObjectCategory.Equipment);

        if (_tabStructureButton != null)
            _tabStructureButton.clicked += () => SwitchCategory(ObjectCategory.Structure);

        if (mainHUDDocument != null)
        {
            var mainRoot = mainHUDDocument.rootVisualElement;

            _hudBuildButton = mainRoot.Q<Button>("BuildButton");
            if (_hudBuildButton != null)
                _hudBuildButton.clicked += OpenStructureCategory; // Fără paranteze!

            _hudFurnitureButton = mainRoot.Q<Button>("FurnitureButton");
            if (_hudFurnitureButton != null)
                _hudFurnitureButton.clicked += OpenEquipmentCategory; // Fără paranteze!
        }
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.clicked -= ClosePanel;
        if (_buildConfirmButton != null) _buildConfirmButton.clicked -= OnBuildConfirmed;

        if (_hudBuildButton != null) _hudBuildButton.clicked -= OpenStructureCategory;
        if (_hudFurnitureButton != null) _hudFurnitureButton.clicked -= OpenEquipmentCategory;

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Unsubscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void OnCloseAllUIReceived(CloseAllUIEvent e)
    {
        ClosePanel();
    }

    private void OpenStructureCategory()
    {
        OpenPanelWithCategory(ObjectCategory.Structure);
    }

    private void OpenEquipmentCategory()
    {
        OpenPanelWithCategory(ObjectCategory.Equipment);
    }

    public void OpenPanelWithCategory(ObjectCategory startCategory)
    {
        if (_buildPanel == null) return;

        // Verificăm dacă panoul era DEJA deschis pe fix aceeași categorie
        bool wasAlreadyOpen = _buildPanel.style.display == DisplayStyle.Flex && _currentCategory == startCategory;

        // 1. STRIGĂM PRIN RADIO: "Toată lumea, inclusiv eu, închideți-vă!"
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new CloseAllUIEvent());
        }

        // 2. Dacă panoul era deja deschis, s-a închis la pasul 1 și ne oprim aici (Efect de Toggle).
        if (wasAlreadyOpen) return;

        // 3. Dacă era închis, îl deschidem noi curat pe tot ecranul!
        _buildPanel.style.display = DisplayStyle.Flex;
        if (_infoPanel != null) _infoPanel.style.display = DisplayStyle.None;

        _currentCategory = startCategory;
        PopulateFurnitureList();
    }

    private void SwitchCategory(ObjectCategory newCategory)
    {
        _currentCategory = newCategory;

        // Ascundem panoul de detalii din dreapta, deoarece am schimbat categoria
        if (_infoPanel != null) _infoPanel.style.display = DisplayStyle.None;

        // Re-populăm lista din stânga, acum cu noua categorie
        PopulateFurnitureList();
    }

    public void ClosePanel()
    {
        if (_buildPanel == null) return;
        _buildPanel.style.display = DisplayStyle.None;
        _furnitureListScroll.Clear();
    }

    private void PopulateFurnitureList()
    {
        _furnitureListScroll.Clear();
        int currentLevel = PlayerXPManager.Instance != null ? PlayerXPManager.Instance.Level : 1;

        // Plasa de siguranță: dacă ai uitat să pui baza de date în Inspector
        if (database == null || database.objectsData == null)
        {
            Debug.LogError("[BuildUI] Nu ai asignat ObjectDataBase în Inspector!");
            return;
        }

        // Citim lista DIRECT din baza de date
        foreach (var furniture in database.objectsData)
        {
            if (furniture.Category != _currentCategory)
                continue;

            VisualElement btnUI = furnitureButtonTemplate.Instantiate();
            Label nameLabel = btnUI.Q<Label>("FurnitureNameLabel");
            VisualElement iconEl = btnUI.Q<VisualElement>("FurnitureIcon");
            VisualElement lockedOverlay = btnUI.Q<VisualElement>("LockedOverlay");

            nameLabel.text = furniture.Name;
            if (furniture.Icon != null) iconEl.style.backgroundImage = new StyleBackground(furniture.Icon);

            bool isUnlocked = currentLevel >= furniture.UnlockLevel;
            if (!isUnlocked)
            {
                lockedOverlay.style.display = DisplayStyle.Flex;
                nameLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                lockedOverlay.style.display = DisplayStyle.None;
            }

            Button clickableArea = btnUI.Q<Button>("CardButton");
            clickableArea.clicked += () => ShowFurnitureInfo(furniture, isUnlocked);

            _furnitureListScroll.Add(btnUI);
        }
    }

    private void ShowFurnitureInfo(ObjectData data, bool isUnlocked)
    {
        _selectedFurniture = data;
        _infoPanel.style.display = DisplayStyle.Flex;

        _infoTitle.text = data.Name;
        _infoDescription.text = data.ObjDescription;
        _infoCost.text = $"{data.Cost} RON";

        if (data.Icon != null) _infoIcon.style.backgroundImage = new StyleBackground(data.Icon);

        // Generăm lista de produse pe care le acceptă acest raft!
        if (data.Category == ObjectCategory.Structure || data.StationType != StationType.Shelf)
        {
            // Ascundem lista de produse (pentru pereți, case de marcat, etc.)
            if (_compatibilitySection != null)
                _compatibilitySection.style.display = DisplayStyle.None;
        }
        else
        {
            // Arătăm lista de produse DOAR pentru rafturi/frigidere
            if (_compatibilitySection != null)
                _compatibilitySection.style.display = DisplayStyle.Flex;

            _infoProducts.text = GetAllowedProductsString(data.ShelfVariant);
        }

        // Controlăm butonul de Build
        if (!isUnlocked)
        {
            _buildConfirmButton.style.display = DisplayStyle.None;
            _lockedWarningText.style.display = DisplayStyle.Flex;
            _lockedWarningText.text = $"SE DEBLOCHEAZĂ LA NIVELUL {data.UnlockLevel}";
        }
        else
        {
            _buildConfirmButton.style.display = DisplayStyle.Flex;
            _lockedWarningText.style.display = DisplayStyle.None;
        }
    }

    // Funcție magică: Citește ce produse sunt permise pe acest raft
    private string GetAllowedProductsString(ShelfType type)
    {
        // Simulăm un raft fals doar pentru a accesa logica ta curată din WorkStation.cs
        WorkStation tempStation = new GameObject("Temp").AddComponent<WorkStation>();
        tempStation.shelfVariant = type;

        List<ProductType> allowed = tempStation.GetAllowedProducts();
        Destroy(tempStation.gameObject); // Curățăm imediat

        if (allowed.Count == 0) return "Niciun produs.";

        // Transformăm lista (Paine, Chipsuri) într-un string frumos cu virgulă
        return "Produse compatibile:\n• " + string.Join("\n• ", allowed);
    }

    private void OnBuildConfirmed()
    {
        if (_selectedFurniture == null) return;

        // 1. Verificăm dacă are bani DOAR CA SĂ ȘTIE. 
        // Banii efectivi îi va lua PlacementState când dă click pe podea!
        if (ServiceLocator.Instance.TryGet(out IMoneyService money))
        {
            // Verificăm dacă își permite măcar 1 bucată
            if (!money.CanAfford(_selectedFurniture.Cost))
            {
                if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
                {
                    eventBus.Publish(new ShowNotificationEvent(
                        "Fonduri Insuficiente",
                        $"Ai nevoie de {_selectedFurniture.Cost} RON pentru a construi {_selectedFurniture.Name}.",
                        NotificationType.Error
                    ));
                }
                return; // Oprim aici, nu îl lăsăm să ia obiectul în mână.
            }
        }

        Debug.Log($"[BuildMenu] Trimit {_selectedFurniture.Name} (ID: {_selectedFurniture.ID}) către PlacementSystem...");

        // 2. Închidem panoul de UI ca să vadă magazinul
        ClosePanel();

        // 3. Găsim PlacementSystem-ul din scenă și îi dăm ID-ul obiectului selectat!
        PlacementSystem placementSystem = UnityEngine.Object.FindFirstObjectByType<PlacementSystem>();

        if (placementSystem != null)
        {
            // Asta va declanșa automat BoxPlacementState, WallPlacementState sau PlacementState normal!
            placementSystem.StartPlacement(_selectedFurniture.ID);
        }
        else
        {
            Debug.LogError("[BuildMenu] Eroare: Nu s-a găsit scriptul PlacementSystem pe niciun obiect din scenă!");
        }
    }
}