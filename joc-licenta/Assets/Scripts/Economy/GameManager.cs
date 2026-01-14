using UnityEngine;
using System;
using UnityEngine.UIElements;

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
    private VisualElement objectDetailsContainer;

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
        root = uiDocument.rootVisualElement;

        moneyText = root.Q<Label>("Money");

        // Căutăm panoul de Info
        // Asigură-te că în UXML elementul se numește EXACT "ObjectInfo"
        objectSelectedInfo = root.Q<VisualElement>("ObjectInfo");

        if (objectSelectedInfo != null)
        {
            objectNameLabel = objectSelectedInfo.Q<Label>("ObjectName");
            objectDetailsContainer = objectSelectedInfo.Q<VisualElement>("ObjectDetails");

            // Ascundem panoul la început, ca să nu stea pe ecran degeaba
            objectSelectedInfo.style.display = DisplayStyle.None;
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
        Debug.Log("Obiect lovit de Raycast: " + obj.name);

        // Ascundem panoul existent pentru a-l reîmprospăta
        if (objectSelectedInfo != null) objectSelectedInfo.style.display = DisplayStyle.None;

        // 2. REPARATIE IERARHIE: Căutăm scripturile și în PĂRINȚI
        // (Deoarece Collider-ul poate fi pe un copil, iar scriptul pe părinte)
        Employee employee = obj.GetComponentInParent<Employee>();
        WorkStation shelf = obj.GetComponentInParent<WorkStation>();

        if (employee != null)
        {
            Debug.Log("Am selectat angajatul: " + employee.employeeName);
            // Aici poți deschide UI-ul specific angajatului
        }
        else if (shelf != null)
        {
            Debug.Log("Am selectat stația de lucru: " + shelf.name);

            if (objectSelectedInfo != null)
            {
                // Afișăm panoul
                objectSelectedInfo.style.display = DisplayStyle.Flex;

                // Setăm numele
                if (objectNameLabel != null)
                    objectNameLabel.text = shelf.name; // Sau o variabilă din WorkStation
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