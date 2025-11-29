using System;
using System.Data.Common;
using UnityEngine;
using static Unity.VisualScripting.Member;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] GameObject mouseIndicator, cellIndicator;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Grid grid;

    [SerializeField] private ObjectDataBase database;
    private int selectedObjectIndex = -1;

    [SerializeField] private GameObject gridVisualization;

    private void Start()
    {
        StopPlacement();
    }

    public void StartPlacement(int ID)
    {
        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);
        if (selectedObjectIndex < 0)
        {
            Debug.LogError($"No ID found {ID}");
            return;
        }
        gridVisualization.SetActive(true);
        cellIndicator.SetActive(true);

        playerInput.OnClick += PlaceStructure;
        playerInput.OnExit += StopPlacement;
    }

    private void PlaceStructure()
    {
        if (playerInput.IsPointerOverUI())
            return;

        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        GameObject newObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
        newObject.transform.position = grid.CellToWorld(gridPosition);
    }

    private void StopPlacement()
    {
        selectedObjectIndex = -1;

        gridVisualization.SetActive(false);
        cellIndicator.SetActive(false);

        playerInput.OnClick -= PlaceStructure;
        playerInput.OnExit -= StopPlacement;
    }

    void Update()
    {
        if (selectedObjectIndex < 0)
            return;
        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        mouseIndicator.transform.position = mousePosition;
        cellIndicator.transform.position = grid.CellToWorld(gridPosition);
    }
}
