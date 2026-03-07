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

    private string GetMoodEmoji(float mood)
    {
        if (mood >= 80f) return "😁";
        if (mood >= 30f) return "😐";
        return "😠";
    }

    private void OnDisable()
    {
        if (_openButton != null) _openButton.clicked -= OpenPanel;
        if (_closeButton != null) _closeButton.clicked -= ClosePanel;
        if (_hireButton != null) _hireButton.clicked -= RequestHireEmployee;
        if (_fireButton != null) _fireButton.clicked -= RequestFireEmployee;
    }

    public void OpenPanel()
    {
        if (_employeePanel == null) return;
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

        // --- NOU: Plasa de siguranță ---
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
            return;
        }

        string[] randomNames = { "Andrei", "Elena", "Mihai", "Ana", "George", "Ioana" };
        string generatedName = randomNames[UnityEngine.Random.Range(0, randomNames.Length)];

        Employee newEmp = EmployeeManager.Instance.HireEmployee(generatedName);
        if (newEmp != null) PopulateEmployeeList();
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

    // --- NOU: LOGICA PENTRU STAREA BUTONULUI DE ANGAJARE ---
    private void UpdateHireButtonState()
    {
        if (_hireButton == null || EmployeeManager.Instance == null) return;

        int currentCount = EmployeeManager.Instance.CurrentEmployeeCount;
        int maxCount = EmployeeManager.Instance.MaxEmployeeCount;

        if (currentCount >= maxCount)
        {
            // Dezactivăm butonul
            _hireButton.SetEnabled(false);
            _hireButton.text = $"LIMITĂ ATINSĂ ({currentCount}/{maxCount})";
        }
        else
        {
            // Activăm butonul
            _hireButton.SetEnabled(true);
            _hireButton.text = $"+ ANGAJEAZĂ NOU ({currentCount}/{maxCount})";
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