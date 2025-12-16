using UnityEngine;
using UnityEngine.UIElements;

public class DeleteUI : MonoBehaviour
{
    [Header("Referințe")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private WallGridData wallData;

    private Button deleteButton;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        deleteButton = root.Q<Button>("Delete");

        if (deleteButton != null)
        {
            deleteButton.clicked += OnDeleteButtonClicked;
        }
        else
        {
            Debug.LogWarning("DeleteUI: Nu am găsit butonul 'DeleteButton'");
        }
    }

    private void OnDisable()
    {
        if (deleteButton != null)
        {
            deleteButton.clicked -= OnDeleteButtonClicked;
        }
    }

    private void OnDeleteButtonClicked()
    {
        if (placementSystem != null)
        {
            placementSystem.StartRemoving();
        }
    }
}
