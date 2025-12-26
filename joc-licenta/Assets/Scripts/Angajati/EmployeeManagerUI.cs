using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

public class EmployeeManagerUI : MonoBehaviour
{
    [Header("Referințe UI")]
    [SerializeField] private UIDocument gameUIDoc;
    [SerializeField] private VisualTreeAsset employeeCardTemplate; // Trage EmployeeCard.uxml aici!

    [Header("Nume Aleatorii")]
    [SerializeField] private List<string> randomNames = new List<string> { "Ion", "Maria", "Andrei", "Elena", "Dan", "Ioana" };

    // Elemente UI
    private VisualElement root;
    private VisualElement employeePanel; // Referință la întreg panoul
    private ScrollView employeeListContainer;
    private Button hireButton;
    private Button openPanelButton; // Butonul din Hotbar

    void Start()
    {
        if (gameUIDoc == null) gameUIDoc = GetComponent<UIDocument>();

        root = gameUIDoc.rootVisualElement;

        // 1. Găsim elementele principale
        employeePanel = root.Q<VisualElement>("EmployeePanel");
        employeeListContainer = root.Q<ScrollView>("EmployeeList");

        // 2. Găsim butoanele
        hireButton = root.Q<Button>("HireButton");
        openPanelButton = root.Q<Button>("Angajati"); // Numele butonului din Hotbar

        // 3. Conectăm Butonul de Deschidere/Închidere Panou (TOGGLE)
        if (openPanelButton != null)
        {
            openPanelButton.clicked += () =>
            {
                ToggleEmployeePanel();
            };
        }
        else
        {
            Debug.LogError("Nu am găsit butonul 'Angajati' în UXML!");
        }

        // 4. Conectăm Butonul de Angajare
        if (hireButton != null)
        {
            hireButton.clicked += OnHireClicked;
        }

        // Aici poți reîncărca angajații existenți dacă ai un sistem de Save/Load
    }

    // Funcția care ascunde sau arată panoul
    private void ToggleEmployeePanel()
    {
        if (employeePanel == null) return;

        // Verificăm dacă e vizibil
        bool isVisible = employeePanel.style.display == DisplayStyle.Flex;

        if (isVisible)
        {
            // Dacă e deschis -> Îl închidem
            employeePanel.style.display = DisplayStyle.None;
        }
        else
        {
            // Dacă e închis -> Îl deschidem
            employeePanel.style.display = DisplayStyle.Flex;
        }
    }

    private void OnHireClicked()
    {
        string newName = randomNames[UnityEngine.Random.Range(0, randomNames.Count)];

        // Managerul creează logica
        Employee newEmp = EmployeeManager.Instance.HireEmployee(newName);

        // UI-ul creează grafica
        if (newEmp != null)
        {
            CreateEmployeeCard(newEmp);
        }
    }

    private void CreateEmployeeCard(Employee employee)
    {
        if (employeeCardTemplate == null) return;

        TemplateContainer card = employeeCardTemplate.Instantiate();
        Label nameLabel = card.Q<Label>("EmployeeName");
        EnumField jobDropdown = card.Q<EnumField>("AssignJob");
        Button fireButton = card.Q<Button>("FireButton");

        if (nameLabel != null) nameLabel.text = employee.employeeName;

        if (jobDropdown != null)
        {
            jobDropdown.Init(employee.role);
            jobDropdown.RegisterValueChangedCallback((evt) =>
            {
                EmployeeRole selectedRole = (EmployeeRole)evt.newValue;
                EmployeeManager.Instance.ChangeEmployeeRole(employee, selectedRole);
            });
        }

        if (fireButton != null)
        {
            fireButton.clicked += () =>
            {
                EmployeeManager.Instance.FireEmployee(employee);
                employeeListContainer.Remove(card);
            };
        }

        employeeListContainer.Add(card);
    }
}
