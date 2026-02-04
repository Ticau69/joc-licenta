using UnityEngine;
using UnityEngine.UIElements;

public class EmployeeNotification : MonoBehaviour
{
    [Header("Configurare")]
    [Tooltip("Asigură-te că acest UIDocument are un PanelSettings setat pe 'World Space'")]
    public UIDocument notificationDocument;

    private VisualElement root;
    private Button clickButton;
    private string currentProblemDescription;

    void Start()
    {
        // Ascundem inițial
        if (notificationDocument != null)
        {
            root = notificationDocument.rootVisualElement;
            root.style.display = DisplayStyle.None;

            // Setup buton (trebuie să existe în UXML)
            clickButton = root.Q<Button>("NotificationButton");
            if (clickButton != null)
                clickButton.clicked += OnNotificationClicked;
        }
    }

    public void ShowNotification(string description)
    {
        if (root == null) return;

        currentProblemDescription = description;
        root.style.display = DisplayStyle.Flex; // Îl facem vizibil
    }

    public void HideNotification()
    {
        if (root != null) root.style.display = DisplayStyle.None;
    }

    private void OnNotificationClicked()
    {
        Debug.Log($"Click pe problemă: {currentProblemDescription}");
        // Aici deschizi meniul de detalii
    }
}
