using UnityEngine;
using System;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // Singleton - Accesibil de oriunde
    public static GameManager Instance { get; private set; }

    [Header("Economie")]
    [SerializeField] private int startingMoney = 5000;
    public int CurrentMoney { get; private set; }

    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;
    private Label moneyText;

    // Referințe pentru panoul de info
    private VisualElement objectSelectedInfo;
    private Label objectNameLabel;
    private VisualElement contentContainer;
    private DropdownField productDropdown; // Folosim DropdownField pentru filtrare
    private Button addProductButton;
    private Button closeButton;

    private WorkStation currentSelectedShelf;

    public event Action OnMoneyChanged;

    void Awake()
    {
        // 1. REPARATIE CRITICĂ SINGLETON
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
    }

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        moneyText = root.Q<Label>("Money");


        // Căutăm panoul de Info
        // Asigură-te că în UXML elementul se numește EXACT "ObjectInfo"
        objectSelectedInfo = root.Q<VisualElement>("ObjectInfo");

        if (objectSelectedInfo != null)
        {
            objectNameLabel = objectSelectedInfo.Q<Label>("ObjectName");
            contentContainer = objectSelectedInfo.Q<VisualElement>("Content");
            addProductButton = objectSelectedInfo.Q<Button>("AdaugareProdus");
            closeButton = objectSelectedInfo.Q<Button>("CloseButton");

            // Ascundem panoul la început, ca să nu stea pe ecran degeaba
            objectSelectedInfo.style.display = DisplayStyle.None;

            if (addProductButton != null)
                addProductButton.clicked += OnActionButtonClicked;
            if (closeButton != null)
                closeButton.clicked += () =>
                {
                    objectSelectedInfo.style.display = DisplayStyle.None;
                    currentSelectedShelf = null;
                };
        }
        else
        {
            Debug.LogError("Nu am găsit elementul 'ObjectInfo' în UI Builder! Verifică numele.");
        }
    }

    void Start()
    {
        UpdateMoneyUI();

        // Ne abonăm la click-uri
        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.OnObjectClicked += SelectObject;
        }
    }

    void SelectObject(GameObject obj)
    {
        // Căutăm WorkStation (codul tău standard)
        WorkStation shelf = obj.GetComponentInParent<WorkStation>(true);
        if (shelf == null) shelf = obj.transform.root.GetComponent<WorkStation>();

        if (shelf != null && shelf.stationType == StationType.Shelf)
        {
            currentSelectedShelf = shelf;
            RefreshUI(); // Funcția care decide ce arătăm
            objectSelectedInfo.style.display = DisplayStyle.Flex;
        }
        else
        {
            objectSelectedInfo.style.display = DisplayStyle.None;
            currentSelectedShelf = null;
        }
    }

    private void RefreshUI()
    {
        if (currentSelectedShelf == null) return;

        contentContainer.Clear();
        objectNameLabel.text = currentSelectedShelf.stationType == StationType.Storage
            ? "Depozit Central"
            : currentSelectedShelf.shelfVariant.ToString();

        // CAZ 1: DEPOZIT
        if (currentSelectedShelf.stationType == StationType.Storage)
        {
            SetupStorageUI();
        }
        // CAZ 2: RAFT NORMAL
        else
        {
            // Dacă raftul e gol (Nu are tip setat)
            if (currentSelectedShelf.slot1Product == ProductType.None)
            {
                addProductButton.text = "Setează Produs";
                addProductButton.SetEnabled(true);
            }
            // Dacă raftul ARE deja un produs
            else
            {
                // Afișăm informațiile
                Label prod = new Label($"Produs: {currentSelectedShelf.slot1Product}");
                prod.style.color = Color.white;
                contentContainer.Add(prod);

                Label stoc = new Label($"Raft: {currentSelectedShelf.slot1Stock}/{currentSelectedShelf.maxProductsPerSlot}");
                stoc.style.color = currentSelectedShelf.slot1Stock == 0 ? Color.red : Color.green;
                contentContainer.Add(stoc);

                // --- MODIFICARE AICI ---
                // Înainte îl dezactivam. Acum îl lăsăm activ pentru a putea schimba produsul.
                addProductButton.text = "Schimbă Produsul";
                addProductButton.SetEnabled(true);
                // -----------------------
            }
        }
    }

    private void OnActionButtonClicked()
    {
        if (currentSelectedShelf == null) return;

        // A. Logica pentru DEPOZIT (Comandă)
        if (currentSelectedShelf.stationType == StationType.Storage)
        {
            CreateSupplyDropdown();
        }
        // B. Logica pentru RAFT (Setare SAU Schimbare Produs)
        else
        {
            // --- MODIFICARE AICI ---
            // Nu mai verificăm dacă e gol. Deschidem meniu oricum.
            // Funcția CreateDynamicDropdown va șterge automat conținutul vechi.
            CreateDynamicDropdown();
            // -----------------------
        }
    }

    // --- FUNCȚIE NOUĂ: UI PENTRU DEPOZIT ---
    private void SetupStorageUI()
    {
        // 1. Afișăm Inventarul Curent din Depozit
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

        // 2. Setăm Butonul de Acțiune pentru "Comandă Marfă"
        addProductButton.text = "Comandă Marfă (-100$)";
        addProductButton.style.display = DisplayStyle.Flex;
        addProductButton.SetEnabled(true);
    }

    // --- FUNCȚIE NOUĂ: Meniu Comandă ---
    private void CreateSupplyDropdown()
    {
        contentContainer.Clear();
        addProductButton.style.display = DisplayStyle.None;

        Label title = new Label("Ce dorești să comanzi?");
        title.style.color = Color.white;
        contentContainer.Add(title);

        // Dropdown cu TOATE produsele posibile
        DropdownField supplyDrop = new DropdownField("Produs:");
        // Excludem 'None' din listă
        List<string> options = Enum.GetNames(typeof(ProductType))
            .Where(x => x != "None").ToList();

        supplyDrop.choices = options;
        supplyDrop.value = "Selectează...";
        contentContainer.Add(supplyDrop);

        supplyDrop.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue != "Selectează...")
            {
                BuyProduct(evt.newValue);
            }
        });

        // Buton Anulare
        Button cancel = new Button(() => { RefreshUI(); addProductButton.style.display = DisplayStyle.Flex; });
        cancel.text = "Anulează";
        contentContainer.Add(cancel);
    }

    // --- FUNCȚIE NOUĂ: Cumpărarea Efectivă ---
    private void BuyProduct(string productName)
    {
        if (Enum.TryParse(productName, out ProductType type))
        {
            int cost = 100; // Preț fix momentan
            int amount = 50; // Câte bucăți primim

            if (TrySpendMoney(cost))
            {
                currentSelectedShelf.AddToStorage(type, amount);
                Debug.Log($"[SUPPLY] Am cumpărat {amount} x {type}");
                RefreshUI(); // Actualizăm lista
                addProductButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                Debug.Log("Fonduri insuficiente!");
                // Aici poți adăuga un efect vizual de eroare
                RefreshUI();
                addProductButton.style.display = DisplayStyle.Flex;
            }
        }
    }

    // --- 4. GENERARE DINAMICĂ DROPDOWN ---
    private void CreateDynamicDropdown()
    {
        // Curățăm containerul
        contentContainer.Clear();

        // Ascundem butonul
        addProductButton.style.display = DisplayStyle.None;

        // Creăm Dropdown-ul
        DropdownField dropdown = new DropdownField("Alege Produsul:");

        List<string> options = currentSelectedShelf.GetAllowedProducts()
                                .Select(x => x.ToString()).ToList();

        dropdown.choices = options;

        // --- FIXUL ESTE AICI ---
        // Nu mai selectăm automat prima opțiune (options[0]).
        // Punem un text "fantomă" care forțează utilizatorul să facă o schimbare.
        dropdown.value = "Selectează...";
        // -----------------------

        dropdown.style.marginBottom = 10;
        dropdown.style.width = Length.Percent(100);

        dropdown.RegisterValueChangedCallback(evt =>
        {
            // Verificăm să nu fie textul placeholder (deși nu e în listă, e bine să fim siguri)
            if (evt.newValue != "Selectează...")
            {
                OnProductSelected(evt.newValue);
            }
        });

        contentContainer.Add(dropdown);

        // Buton Anulare
        Button cancelButton = new Button(() =>
        {
            addProductButton.style.display = DisplayStyle.Flex;
            RefreshUI();
        });
        cancelButton.text = "Anulează";
        contentContainer.Add(cancelButton);
    }

    // --- 5. FINALIZARE SELECȚIE ---
    private void OnProductSelected(string productName)
    {
        if (Enum.TryParse(productName, out ProductType selectedType))
        {
            // 1. Relocare (codul existent)
            if (currentSelectedShelf.slot1Product != ProductType.None && currentSelectedShelf.slot1Stock > 0)
            {
                if (currentSelectedShelf.slot1Product != selectedType)
                {
                    RelocateExistingStock(currentSelectedShelf.slot1Product, currentSelectedShelf.slot1Stock);
                }
            }

            // 2. Setare Produs Nou
            currentSelectedShelf.slot1Product = selectedType;
            currentSelectedShelf.slot1Stock = 0;

            Debug.Log($"Produs nou setat: {selectedType}. Notificăm angajații...");

            // 3. --- COD NOU: NOTIFICĂM ANGAJAȚII ---
            NotifyAllRestockers();
            // ---------------------------------------

            addProductButton.style.display = DisplayStyle.Flex;
            RefreshUI();
        }
    }

    // --- FUNCȚIE NOUĂ ÎN GameManager ---
    private void NotifyAllRestockers()
    {
        // Găsim toți angajații din scenă
        Employee[] allEmployees = FindObjectsByType<Employee>(FindObjectsSortMode.None);

        foreach (var emp in allEmployees)
        {
            // Îi trezim pe toți
            emp.WakeUpAndWork();
        }
    }

    // --- ALGORITMUL DE RELOCARE ---
    private void RelocateExistingStock(ProductType typeToMove, int amountToMove)
    {
        int amountLeft = amountToMove;

        // Găsim toate stațiile active
        var allStations = FindObjectsByType<WorkStation>(FindObjectsSortMode.None);

        Debug.Log($"[RELOCARE] Încerc să mut {amountToMove} x {typeToMove}...");

        // PASUL 1: Căutăm alte rafturi (Shelf) compatibile
        foreach (var station in allStations)
        {
            if (amountLeft <= 0) break;

            if (station.stationType == StationType.Shelf &&
                station != currentSelectedShelf &&
                station.slot1Product == typeToMove)
            {
                int spaceAvailable = station.maxProductsPerSlot - station.slot1Stock;

                if (spaceAvailable > 0)
                {
                    int amountTransferring = Mathf.Min(amountLeft, spaceAvailable);
                    station.slot1Stock += amountTransferring; // Adăugăm direct la stoc
                    amountLeft -= amountTransferring;

                    Debug.Log($" -> Mutat {amountTransferring} buc. în raftul '{station.name}'");
                }
            }
        }

        // PASUL 2: Restul la DEPOZIT (Storage)
        if (amountLeft > 0)
        {
            // Căutare mai sigură a depozitului
            WorkStation storage = allStations.FirstOrDefault(x => x.stationType == StationType.Storage);

            if (storage != null)
            {
                storage.AddToStorage(typeToMove, amountLeft);
                Debug.Log($" -> Salvat {amountLeft} buc. în DEPOZIT ({storage.name}).");
            }
            else
            {
                Debug.LogError(" -> [EROARE CRITICĂ] Nu am găsit niciun obiect de tip STORAGE în scenă! Produsele s-au pierdut.");
            }
        }
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
        if (moneyText != null)
        {
            moneyText.text = $"{CurrentMoney} RON";
        }
    }

    void OnDestroy()
    {
        OnMoneyChanged -= UpdateMoneyUI;

        // Dezabonare pentru a evita erori la schimbarea scenei
        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.OnObjectClicked -= SelectObject;
        }
    }
}