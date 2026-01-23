using UnityEngine;
using System;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // Singleton - Accesibil de oriunde
    public static GameManager Instance { get; private set; }

    [Header("Database Configuration")]
    // MODIFICARE: Nu mai e List<ProductDataSO>, ci un singur fișier master
    public ProductDataSO productDB;

    // Memoria jocului (Runtime)
    public Dictionary<ProductType, ProductEconomics> marketData = new Dictionary<ProductType, ProductEconomics>();
    [Header("Economie")]
    [SerializeField] private int startingMoney = 5000;
    public int CurrentMoney { get; private set; }

    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Inventory UI")]
    private VisualElement inventoryTab;
    private ScrollView inventoryList;
    private WorkStation mainStorage;
    private float inventoryUpdateTimer = 0;

    [Header("Inventory Details UI")]
    private Label detailNameLabel;
    private Label detailStockLabel;
    private Label detailStatusLabel;
    private VisualElement detailsPanel;

    [Header("Inventory Pricing UI")]
    private Slider priceSlider;
    private Label priceDisplayLabel;
    private Label profitDisplayLabel;

    // Variabilă ca să știm la ce produs umblăm acum
    private ProductType currentViewingProduct = ProductType.None;

    private VisualElement root;
    private Label moneyText;

    // Referințe pentru panoul de info
    private VisualElement objectSelectedInfo;
    private Label objectNameLabel;
    private VisualElement contentContainer;
    private Button addProductButton;
    private Button closeButton;
    private WorkStation currentSelectedShelf;

    public event Action OnMoneyChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Inițializăm banii
        CurrentMoney = startingMoney;
        OnMoneyChanged += UpdateMoneyUI;

        // Inițializăm economia din ScriptableObjects
        InitializeEconomy();
    }

    private void InitializeEconomy()
    {
        marketData.Clear();

        if (productDB == null)
        {
            Debug.LogError("Nu ai atașat fișierul 'ProductDatabase' în GameManager!");
            return;
        }

        foreach (var productInfo in productDB.allProducts)
        {
            // Creăm intrarea în memoria jocului
            ProductEconomics newEntry = new ProductEconomics(productInfo);

            if (!marketData.ContainsKey(productInfo.type))
            {
                marketData.Add(productInfo.type, newEntry);
            }
        }
        Debug.Log($"Economie inițializată. {marketData.Count} produse încărcate din ProductDatabase.");
    }

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        moneyText = root.Q<Label>("Money");

        objectSelectedInfo = root.Q<VisualElement>("ObjectInfo");

        if (objectSelectedInfo != null)
        {
            objectNameLabel = objectSelectedInfo.Q<Label>("ObjectName");
            contentContainer = objectSelectedInfo.Q<VisualElement>("Content");
            addProductButton = objectSelectedInfo.Q<Button>("AdaugareProdus");
            closeButton = objectSelectedInfo.Q<Button>("CloseButton");

            inventoryList = root.Q<ScrollView>("InventoryList");
            inventoryTab = root.Q<VisualElement>("Inventory");

            detailsPanel = root.Q<VisualElement>("DetailsPanel");
            detailNameLabel = root.Q<Label>("DetailName");
            detailStockLabel = root.Q<Label>("DetailStock");
            detailStatusLabel = root.Q<Label>("DetailStatus");

            priceSlider = root.Q<Slider>("PriceSlider");
            priceDisplayLabel = root.Q<Label>("PriceDisplay");
            profitDisplayLabel = root.Q<Label>("ProfitDisplay");

            objectSelectedInfo.style.display = DisplayStyle.None;

            if (addProductButton != null)
                addProductButton.clicked += OnActionButtonClicked;

            if (closeButton != null)
                closeButton.clicked += () =>
                {
                    objectSelectedInfo.style.display = DisplayStyle.None;
                    currentSelectedShelf = null;
                };

            if (priceSlider != null)
            {
                priceSlider.RegisterValueChangedCallback(evt =>
                {
                    OnPriceSliderChanged(evt.newValue);
                });
            }
        }
        else
        {
            Debug.LogError("Nu am găsit elementul 'ObjectInfo' în UI Builder! Verifică numele.");
        }
    }

    void Start()
    {
        UpdateMoneyUI();

        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.OnObjectClicked += SelectObject;
        }
    }

    void Update()
    {
        if (inventoryTab != null && inventoryTab.style.display != DisplayStyle.None)
        {
            inventoryUpdateTimer += Time.deltaTime;
            if (inventoryUpdateTimer > 0.5f)
            {
                UpdateInventoryUI();
                inventoryUpdateTimer = 0;
            }
        }
    }

    void SelectObject(GameObject obj)
    {
        WorkStation shelf = obj.GetComponentInParent<WorkStation>(true);
        if (shelf == null) shelf = obj.transform.root.GetComponent<WorkStation>();

        if (shelf != null && shelf.stationType == StationType.Shelf)
        {
            currentSelectedShelf = shelf;
            RefreshUI();
            objectSelectedInfo.style.display = DisplayStyle.Flex;
        }
        else
        {
            objectSelectedInfo.style.display = DisplayStyle.None;
            currentSelectedShelf = null;
        }
    }

    private void UpdateInventoryUI()
    {
        if (mainStorage == null)
        {
            var allStations = FindObjectsByType<WorkStation>(FindObjectsSortMode.None);
            mainStorage = allStations.FirstOrDefault(x => x.stationType == StationType.Storage);
            if (mainStorage == null) return;
        }

        inventoryList.Clear();

        foreach (ProductType type in Enum.GetValues(typeof(ProductType)))
        {
            if (type == ProductType.None) continue;

            int amount = 0;
            if (mainStorage.storageInventory.ContainsKey(type)) amount = mainStorage.storageInventory[type];

            Color rowColor;
            string statusText;
            if (amount == 0) { rowColor = new Color(1f, 0.3f, 0.3f); statusText = "CRITIC"; }
            else if (amount < 20) { rowColor = Color.yellow; statusText = "SCĂZUT"; }
            else { rowColor = Color.green; statusText = "BUN"; }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 5;
            row.style.borderBottomColor = new Color(1, 1, 1, 0.1f);
            row.style.borderBottomWidth = 1;
            row.style.height = 40;

            Label infoLabel = new Label($"{type} ({amount})");
            infoLabel.style.color = rowColor;
            infoLabel.style.fontSize = 14;
            row.Add(infoLabel);

            Button viewBtn = new Button();
            viewBtn.text = "VIEW";
            viewBtn.style.width = 60;
            viewBtn.style.height = 25;
            viewBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            viewBtn.style.color = Color.white;

            ProductType capturedType = type;
            int capturedAmount = amount;
            string capturedStatus = statusText;
            Color capturedColor = rowColor;

            viewBtn.clicked += () =>
            {
                ShowProductDetails(capturedType, capturedAmount, capturedStatus, capturedColor);
            };

            row.Add(viewBtn);
            inventoryList.Add(row);
        }
    }

    // --- LOGICA DE VIZUALIZARE CORECTATĂ ---
    private void ShowProductDetails(ProductType type, int amount, string status, Color color)
    {
        currentViewingProduct = type;

        // 1. Verificăm dacă avem date în Baza de Date
        if (marketData.ContainsKey(type))
        {
            ProductEconomics ecoData = marketData[type];
            ProductData staticData = ecoData.data; // Luăm datele statice (ScriptableObject)

            // Acum putem seta numele corect din fișier (ex: "Paine Rustica" în loc de "Bread")
            if (detailNameLabel != null)
                detailNameLabel.text = staticData.productName;

            // Configurare Slider
            if (priceSlider != null)
            {
                priceSlider.lowValue = ecoData.CurrentBaseCost;     // Minim: cât am dat pe ea
                priceSlider.highValue = ecoData.CurrentBaseCost * 5.0f; // Maxim: de 5 ori prețul

                // Setăm valoarea curentă a slider-ului fără a declanșa event-ul (opțional)
                priceSlider.SetValueWithoutNotify(ecoData.sellingPrice);
            }

            UpdatePriceLabels(ecoData);
        }
        else
        {
            // Fallback dacă nu avem date (ex: nu ai creat încă ScriptableObject pt acest tip)
            if (detailNameLabel != null) detailNameLabel.text = type.ToString();
        }

        // 2. Actualizăm stocul și statusul (independent de DB)
        if (detailStockLabel != null)
        {
            detailStockLabel.text = $"Stoc Actual: {amount} bucăți";
            detailStockLabel.style.color = color;
        }

        if (detailStatusLabel != null)
        {
            detailStatusLabel.text = $"Status Depozit: {status}";
            detailStatusLabel.style.color = color;
        }

        Debug.Log($"Vizualizare detalii pentru: {type}");
    }

    private void OnPriceSliderChanged(float newValue)
    {
        if (currentViewingProduct != ProductType.None && marketData.ContainsKey(currentViewingProduct))
        {
            ProductEconomics data = marketData[currentViewingProduct];
            data.sellingPrice = (float)System.Math.Round(newValue, 2);
            UpdatePriceLabels(data);
        }
    }

    private void UpdatePriceLabels(ProductEconomics data)
    {
        if (priceDisplayLabel != null)
            priceDisplayLabel.text = $"{data.sellingPrice:F2} RON";

        if (profitDisplayLabel != null)
        {
            float profit = data.Profit;
            profitDisplayLabel.text = $"Profit: {(profit >= 0 ? "+" : "")}{profit:F2} RON";
            profitDisplayLabel.style.color = profit >= 0 ? Color.green : Color.red;
        }
    }

    private void RefreshUI()
    {
        if (currentSelectedShelf == null) return;

        addProductButton.style.display = DisplayStyle.Flex;
        addProductButton.SetEnabled(true);

        contentContainer.Clear();
        objectNameLabel.text = currentSelectedShelf.stationType == StationType.Storage
            ? "Depozit Central"
            : currentSelectedShelf.shelfVariant.ToString();

        if (currentSelectedShelf.stationType == StationType.Storage)
        {
            SetupStorageUI();
        }
        else
        {
            if (currentSelectedShelf.slot1Product == ProductType.None)
            {
                addProductButton.text = "Setează Produs";
                addProductButton.SetEnabled(true);
            }
            else
            {
                Label prod = new Label($"Produs: {currentSelectedShelf.slot1Product}");
                prod.style.color = Color.white;
                contentContainer.Add(prod);

                Color stockColor;
                float fillPercent = (float)currentSelectedShelf.slot1Stock / currentSelectedShelf.maxProductsPerSlot;

                if (currentSelectedShelf.slot1Stock == 0) stockColor = Color.red;
                else if (fillPercent <= 0.5f) stockColor = Color.yellow;
                else stockColor = Color.green;

                Label stoc = new Label($"Raft: {currentSelectedShelf.slot1Stock}/{currentSelectedShelf.maxProductsPerSlot}");
                stoc.style.color = stockColor;
                stoc.style.unityFontStyleAndWeight = FontStyle.Bold;
                contentContainer.Add(stoc);

                addProductButton.text = "Schimbă Produsul";
                addProductButton.SetEnabled(true);
            }
        }
    }

    private void OnActionButtonClicked()
    {
        if (currentSelectedShelf == null) return;

        if (currentSelectedShelf.stationType == StationType.Storage)
            CreateSupplyDropdown();
        else
            CreateDynamicDropdown();
    }

    private void SetupStorageUI()
    {
        Label inventoryTitle = new Label("--- INVENTAR ---");
        inventoryTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        inventoryTitle.style.color = Color.yellow;
        contentContainer.Add(inventoryTitle);

        if (currentSelectedShelf.storageInventory.Count == 0)
        {
            contentContainer.Add(new Label("Depozit Gol"));
        }
        else
        {
            foreach (var item in currentSelectedShelf.storageInventory)
            {
                Label itemLabel = new Label($"{item.Key}: {item.Value} buc.");
                itemLabel.style.color = Color.white;
                contentContainer.Add(itemLabel);
            }
        }

        addProductButton.text = "Comandă Marfă (-100$)";
        addProductButton.style.display = DisplayStyle.Flex;
        addProductButton.SetEnabled(true);
    }

    private void CreateSupplyDropdown()
    {
        contentContainer.Clear();
        Label title = new Label("Ce dorești să comanzi?");
        title.style.color = Color.white;
        contentContainer.Add(title);

        DropdownField supplyDrop = new DropdownField("Produs:");
        List<string> options = Enum.GetNames(typeof(ProductType)).Where(x => x != "None").ToList();

        supplyDrop.choices = options;
        supplyDrop.value = "Selectează...";
        contentContainer.Add(supplyDrop);

        supplyDrop.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue != "Selectează...") BuyProduct(evt.newValue);
        });

        Button cancel = new Button(() => { RefreshUI(); addProductButton.style.display = DisplayStyle.Flex; });
        cancel.text = "Anulează";
        contentContainer.Add(cancel);
    }

    private void BuyProduct(string productName)
    {
        if (Enum.TryParse(productName, out ProductType type))
        {
            // --- AICI PE VIITOR VEI FOLOSI COSTUL DIN ScriptableObject ---
            // Momentan lăsăm 100$, dar ideal ar fi: marketData[type].CurrentBaseCost * amount
            int cost = 100;
            int amount = 50;

            if (TrySpendMoney(cost))
            {
                currentSelectedShelf.AddToStorage(type, amount);
                Debug.Log($"[SUPPLY] Am cumpărat {amount} x {type}");
                RefreshUI();
                addProductButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                RefreshUI();
                addProductButton.style.display = DisplayStyle.Flex;
            }
        }
    }

    private void CreateDynamicDropdown()
    {
        contentContainer.Clear();
        DropdownField dropdown = new DropdownField("Alege Produsul:");
        List<string> options = currentSelectedShelf.GetAllowedProducts().Select(x => x.ToString()).ToList();

        dropdown.choices = options;
        dropdown.value = "Selectează...";
        dropdown.style.marginBottom = 10;
        dropdown.style.width = Length.Percent(100);

        dropdown.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue != "Selectează...") OnProductSelected(evt.newValue);
        });

        contentContainer.Add(dropdown);

        Button cancelButton = new Button(() =>
        {
            addProductButton.style.display = DisplayStyle.Flex;
            RefreshUI();
        });
        cancelButton.text = "Anulează";
        contentContainer.Add(cancelButton);
    }

    private void OnProductSelected(string productName)
    {
        if (Enum.TryParse(productName, out ProductType selectedType))
        {
            if (currentSelectedShelf.slot1Stock == 0 || currentSelectedShelf.slot1Product == selectedType)
            {
                currentSelectedShelf.slot1Product = selectedType;
                currentSelectedShelf.pendingProduct = ProductType.None;
            }
            else
            {
                currentSelectedShelf.pendingProduct = selectedType;
                if (addProductButton != null) addProductButton.text = "În curs de schimbare...";
            }

            NotifyAllRestockers();
            RefreshUI();
        }
    }

    private void NotifyAllRestockers()
    {
        Employee[] allEmployees = FindObjectsByType<Employee>(FindObjectsSortMode.None);
        foreach (var emp in allEmployees) emp.WakeUpAndWork();
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount <= CurrentMoney)
        {
            CurrentMoney -= amount;
            OnMoneyChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void AddMoney(int amount)
    {
        CurrentMoney += amount;
        OnMoneyChanged?.Invoke();
    }

    public void UpdateMoneyUI()
    {
        if (moneyText != null) moneyText.text = $"{CurrentMoney} RON";
    }

    void OnDestroy()
    {
        OnMoneyChanged -= UpdateMoneyUI;
        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null) playerInput.OnObjectClicked -= SelectObject;
    }
}

[System.Serializable]
public class ProductEconomics
{
    // Modificare: Acum referă clasa simplă, nu ScriptableObject-ul
    public ProductData data;

    public float sellingPrice;  // Prețul setat de jucător

    public float CurrentBaseCost => data.baseCost;

    public ProductEconomics(ProductData sourceData)
    {
        data = sourceData;
        sellingPrice = sourceData.defaultSellingPrice;
    }

    public float Profit => sellingPrice - CurrentBaseCost;
}