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

            // Ascundem panoul la început, ca să nu stea pe ecran degeaba
            objectSelectedInfo.style.display = DisplayStyle.None;

            if (addProductButton != null)
                addProductButton.clicked += OnActionButtonClicked;
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

        // Resetăm conținutul (ștergem dropdown-uri vechi dacă există)
        contentContainer.Clear();
        objectNameLabel.text = currentSelectedShelf.shelfVariant.ToString();

        // CAZ 1: Raftul nu are produs asignat
        if (currentSelectedShelf.slot1Product == ProductType.None)
        {
            addProductButton.text = "Adaugă Produs";
            addProductButton.SetEnabled(true);

            // Putem pune un text informativ în content
            Label info = new Label("Acest raft este gol.");
            info.style.color = Color.gray;
            info.style.unityTextAlign = TextAnchor.MiddleCenter;
            contentContainer.Add(info);
        }
        // CAZ 2: Raftul are produs
        else
        {
            // Afișăm info despre produs
            Label prodLabel = new Label($"Produs: {currentSelectedShelf.slot1Product}");
            prodLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            prodLabel.style.color = Color.white;
            contentContainer.Add(prodLabel);

            Color stockColor;
            float fillPercentage = (float)currentSelectedShelf.slot1Stock / currentSelectedShelf.maxProductsPerSlot;

            if (currentSelectedShelf.slot1Stock == 0)
            {
                stockColor = Color.red; // E gol complet
            }
            else if (fillPercentage <= 0.5f) // Dacă e sub sau egal cu 50% (ex: 10/20)
            {
                stockColor = Color.yellow;
            }
            else
            {
                stockColor = Color.green; // Peste 50%
            }

            Label stockLabel = new Label($"Stoc: {currentSelectedShelf.slot1Stock}/{currentSelectedShelf.maxProductsPerSlot}");
            stockLabel.style.color = stockColor;
            stockLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            contentContainer.Add(stockLabel);

            addProductButton.text = "Reumple Stoc";
            addProductButton.SetEnabled(currentSelectedShelf.slot1Stock < currentSelectedShelf.maxProductsPerSlot);
        }
    }

    private void OnActionButtonClicked()
    {
        if (currentSelectedShelf == null) return;

        // Dacă nu avem produs -> Generăm Dropdown-ul!
        if (currentSelectedShelf.slot1Product == ProductType.None)
        {
            CreateDynamicDropdown();
        }
        // Dacă avem produs -> Reumplem stocul
        else
        {
            currentSelectedShelf.RestockSlot1();
            RefreshUI(); // Actualizăm vizual
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
            // Salvăm în raft
            currentSelectedShelf.slot1Product = selectedType;
            currentSelectedShelf.slot1Stock = 0;

            Debug.Log($"Produs ales: {selectedType}");

            // Reafișăm butonul principal
            addProductButton.style.display = DisplayStyle.Flex;

            // Revenim la meniul de info (care acum va arăta stocul)
            RefreshUI();
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