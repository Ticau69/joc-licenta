using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class EmployeeUIManager : MonoBehaviour
{
    [Header("Referințe Externe")]
    public UIDocument mainHUDDocument;

    [Header("UI Templates")]
    public VisualTreeAsset employeeCardTemplate;

    [Header("Economie")]
    public int hireCost = 500;

    private VisualElement _employeePanel;
    private ScrollView _employeeListScrollView;

    // Butoane UI
    private Button _closeButton;
    private Button _openButton;
    private Button _hireButton;
    private Button _fireButton;

    // Referințe panou dreapta
    private Label _selectedNameText;
    private DropdownField _roleDropdown;

    // NOU: Referințe pentru progres și salariu
    private CircularProgress _levelProgress;
    private Label _salaryText;
    private SliderInt _salarySlider;
    private Label _expectedSalaryText;

    private Employee _selectedEmployee;

    private void Start()
    {
        // Ne abonăm în Start, ca să fim siguri că EventBus a fost creat deja!
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Subscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void OnEnable()
    {
        var localRoot = GetComponent<UIDocument>().rootVisualElement;

        _employeePanel = localRoot.Q<VisualElement>("EmployeeManagementContainer");
        if (_employeePanel == null) return;

        _employeePanel.style.display = DisplayStyle.None;

        _employeeListScrollView = _employeePanel.Q<ScrollView>("EmployeeListScrollView");
        _closeButton = _employeePanel.Q<Button>("ClosePanelButton");

        _selectedNameText = _employeePanel.Q<Label>("SelectedEmployeeName");
        _roleDropdown = _employeePanel.Q<DropdownField>("RoleDropdown");

        // --- CONECTĂM NOILE ELEMENTE ---
        _levelProgress = _employeePanel.Q<CircularProgress>("EmployeeLevelProgress");
        _salaryText = _employeePanel.Q<Label>("EmployeeSalaryText");
        _salarySlider = _employeePanel.Q<SliderInt>("SalarySlider");
        _expectedSalaryText = _employeePanel.Q<Label>("ExpectedSalaryText");

        _hireButton = _employeePanel.Q<Button>("HireNewButton");
        _fireButton = _employeePanel.Q<Button>("FireButton");

        if (_roleDropdown != null)
        {
            _roleDropdown.choices = new List<string> { "Fără Rol", "Casier", "Manipulant Marfă", "Curățător" };
            _roleDropdown.RegisterValueChangedCallback(evt => OnRoleDropdownChanged(evt.newValue));
        }

        // Când tragi de slider, schimbăm instant salariul angajatului!
        if (_salarySlider != null)
        {
            _salarySlider.RegisterValueChangedCallback(evt => OnSalarySliderChanged(evt.newValue));
        }

        if (_hireButton != null) _hireButton.clicked += RequestHireEmployee;
        if (_fireButton != null) _fireButton.clicked += RequestFireEmployee;
        if (_closeButton != null) _closeButton.clicked += ClosePanel;

        if (mainHUDDocument != null)
        {
            var mainRoot = mainHUDDocument.rootVisualElement;
            _openButton = mainRoot.Q<Button>("Angajati");
            if (_openButton != null) _openButton.clicked += OpenPanel;
        }
    }

    private void OnDestroy()
    {
        if (_openButton != null) _openButton.clicked -= OpenPanel;
        if (_closeButton != null) _closeButton.clicked -= ClosePanel;
        if (_hireButton != null) _hireButton.clicked -= RequestHireEmployee;
        if (_fireButton != null) _fireButton.clicked -= RequestFireEmployee;

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Unsubscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void OnCloseAllUIReceived(CloseAllUIEvent e)
    {
        ClosePanel();
    }

    private string GetMoodEmoji(float mood)
    {
        if (mood >= 80f) return "😁";
        if (mood >= 30f) return "😐";
        return "😠";
    }

    public void OpenPanel()
    {
        if (_employeePanel == null) return;

        bool wasAlreadyOpen = _employeePanel.style.display == DisplayStyle.Flex;

        // 1. STRIGĂM PRIN RADIO să se închidă restul panourilor!
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Publish(new CloseAllUIEvent());
        }

        // 2. Dacă era deja deschis, ne oprim aici
        if (wasAlreadyOpen) return;

        // 3. Altfel, îl deschidem
        _employeePanel.style.display = DisplayStyle.Flex;
        PopulateEmployeeList();
        ClearRightPanel();
    }

    public void ClosePanel()
    {
        if (_employeePanel == null) return;
        _employeePanel.style.display = DisplayStyle.None;
        _employeeListScrollView.Clear();
        _selectedEmployee = null;
    }

    private void RequestHireEmployee()
    {
        if (EmployeeManager.Instance == null) return;

        // 1. Verificăm mai întâi limita de angajați
        if (EmployeeManager.Instance.CurrentEmployeeCount >= EmployeeManager.Instance.MaxEmployeeCount)
        {
            if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            {
                eventBus.Publish(new ShowNotificationEvent(
                    "Limită atinsă!",
                    "Ai atins numărul maxim de angajați pentru nivelul tău actual.",
                    NotificationType.Warning
                ));
            }
            return; // Oprim execuția
        }

        // 2. Verificăm dacă jucătorul are bani suficienți
        if (ServiceLocator.Instance.TryGet(out IMoneyService money))
        {
            /* ATENȚIE: Aici folosesc o metodă fictivă numită "HasEnough". 
               Va trebui să o înlocuiești cu proprietatea reală din IMoneyService-ul tău 
               care verifică balanța (ex: money.Balance >= hireCost sau money.CanAfford(hireCost)) */

            if (!money.CanAfford(hireCost)) // <-- Verifică numele acestei funcții în IMoneyService-ul tău!
            {
                if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
                {
                    eventBus.Publish(new ShowNotificationEvent(
                        "Fonduri insuficiente!",
                        $"Costul de recrutare este de {hireCost} RON. Nu îți permiți acum.",
                        NotificationType.Error
                    ));
                }
                return; // Oprim execuția (nu primește angajatul)
            }

            // Dacă are bani, îi luăm!
            money.TrySpend(hireCost);
        }
        else
        {
            Debug.LogWarning("[EmployeeUI] Nu s-a găsit IMoneyService! Trecem peste cost (Mod Test).");
        }

        // 3. Dacă am ajuns aici, totul e OK (are loc liber și a plătit). Facem angajarea!
        string[] randomNames = { "Andrei", "Elena", "Mihai", "Ana", "George", "Ioana", "Vasile" };
        string generatedName = randomNames[UnityEngine.Random.Range(0, randomNames.Length)];

        Employee newEmp = EmployeeManager.Instance.HireEmployee(generatedName);

        if (newEmp != null)
        {
            PopulateEmployeeList(); // Reîmprospătăm interfața (care va actualiza și numărul de pe buton)
        }
    }

    private void RequestFireEmployee()
    {
        if (EmployeeManager.Instance == null || _selectedEmployee == null) return;
        EmployeeManager.Instance.FireEmployee(_selectedEmployee);
        ClearRightPanel();
        PopulateEmployeeList();
    }

    private void PopulateEmployeeList()
    {
        _employeeListScrollView.Clear();
        if (EmployeeManager.Instance == null) return;

        List<Employee> allEmployees = EmployeeManager.Instance.AllEmployees;

        foreach (var emp in allEmployees)
        {
            VisualElement newCard = employeeCardTemplate.Instantiate();
            newCard.Q<Label>("EmployeeNameLabel").text = emp.employeeName;
            newCard.Q<Label>("EmployeeRoleLabel").text = TranslateRoleToRomanian(emp.role);

            // Acum emoji-ul din stânga se va actualiza cu valoarea reală!
            newCard.Q<Label>("EmployeeMoodEmoji").text = GetMoodEmoji(emp.mood);

            // Setăm și emoji-ul (dacă ai implementat partea cu Mood-ul)
            // newCard.Q<Label>("EmployeeMoodEmoji").text = GetMoodEmoji(emp.mood);

            Button cardBtn = newCard.Q<Button>("CardButton");
            cardBtn.clicked += () => OnEmployeeCardClicked(emp);

            _employeeListScrollView.Add(newCard);
        }

        // --- NOU: Actualizăm butonul după ce am desenat lista ---
        UpdateHireButtonState();
    }

    private void OnEmployeeCardClicked(Employee emp)
    {
        _selectedEmployee = emp;
        _selectedNameText.text = $"{emp.employeeName} {GetMoodEmoji(emp.mood)}";

        if (_roleDropdown != null)
            _roleDropdown.value = TranslateRoleToRomanian(emp.role);

        // --- ACTUALIZĂM UI-UL CU DATELE REALE ALE ANGAJATULUI ---

        // 1. Setăm Slider-ul și textul de salariu
        if (_salarySlider != null)
        {
            // Oprim temporar notificările slider-ului ca să nu suprascriem datele din greșeală
            _salarySlider.SetValueWithoutNotify(emp.currentSalary);
        }

        if (_salaryText != null) _salaryText.text = $"{emp.currentSalary} RON";

        if (_expectedSalaryText != null)
            _expectedSalaryText.text = $"Salariu așteptat: {emp.ExpectedSalary} RON";

        // 2. Setăm Circular Progress Bar-ul (Nivelul și XP-ul)
        if (_levelProgress != null)
        {
            _levelProgress.CenterText = emp.level.ToString();

            // Calculăm procentajul de XP (ex: 50 din 100 XP = 50%)
            float xpPercentage = ((float)emp.currentXP / emp.XPForNextLevel) * 100f;
            _levelProgress.Progress = xpPercentage;
        }
    }

    private void ClearRightPanel()
    {
        _selectedEmployee = null;
        _selectedNameText.text = "Selectează un angajat din stânga";
        if (_roleDropdown != null) _roleDropdown.value = "";

        if (_salaryText != null) _salaryText.text = "-";
        if (_expectedSalaryText != null) _expectedSalaryText.text = "";

        if (_levelProgress != null)
        {
            _levelProgress.CenterText = "-";
            _levelProgress.Progress = 0f;
        }
    }

    private void OnRoleDropdownChanged(string newRoleNameRO)
    {
        if (_selectedEmployee == null || EmployeeManager.Instance == null) return;

        EmployeeRole newRole = TranslateRomanianToRole(newRoleNameRO);

        if (_selectedEmployee.role != newRole)
        {
            EmployeeManager.Instance.ChangeEmployeeRole(_selectedEmployee, newRole);
            PopulateEmployeeList();
        }
    }

    // --- FUNCȚIA CARE SE APELEAZĂ CÂND JUCĂTORUL TRAGE DE SLIDER ---
    private void OnSalarySliderChanged(int newValue)
    {
        if (_selectedEmployee == null) return;

        // Actualizăm salariul în memoria angajatului
        _selectedEmployee.currentSalary = newValue;

        // Actualizăm textul roșu mare
        if (_salaryText != null)
        {
            _salaryText.text = $"{newValue} RON";
        }
    }

    private void UpdateHireButtonState()
    {
        if (_hireButton == null || EmployeeManager.Instance == null) return;

        int currentCount = EmployeeManager.Instance.CurrentEmployeeCount;
        int maxCount = EmployeeManager.Instance.MaxEmployeeCount;

        if (currentCount >= maxCount)
        {
            _hireButton.SetEnabled(false);
            _hireButton.text = $"LIMITĂ ATINSĂ ({currentCount}/{maxCount})";
        }
        else
        {
            _hireButton.SetEnabled(true);
            // NOU: Afișăm și costul pe buton
            _hireButton.text = $"+ ANGAJEAZĂ NOU - {hireCost} RON ({currentCount}/{maxCount})";
        }
    }

    private string TranslateRoleToRomanian(EmployeeRole role)
    {
        switch (role)
        {
            case EmployeeRole.Cashier: return "Casier";
            case EmployeeRole.Restocker: return "Manipulant Marfă";
            case EmployeeRole.Janitor: return "Curățător";
            default: return "Fără Rol";
        }
    }

    private EmployeeRole TranslateRomanianToRole(string roleRO)
    {
        switch (roleRO)
        {
            case "Casier": return EmployeeRole.Cashier;
            case "Manipulant Marfă": return EmployeeRole.Restocker;
            case "Curățător": return EmployeeRole.Janitor;
            default: return EmployeeRole.None;
        }
    }
}