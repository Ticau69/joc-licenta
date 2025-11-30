using System;
using System.Collections.Generic;
using System.Data.Common;
using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Grid grid;

    [SerializeField] private ObjectDataBase database;
    [SerializeField] private GameObject gridVisualization;
    private GridData floorData, furnitureData;
    [SerializeField] private PreviewSystem previewSystem;

    private Vector3Int lastDetectedPosition = Vector3Int.zero;
    [SerializeField] private ObjectPlacer objectPlacer;

    IBuldingState buildingState;

    private void Start()
    {
        StopPlacement();
        floorData = new();
        furnitureData = new();
    }

    public void StartPlacement(int ID)
    {
        StopPlacement();
        gridVisualization.SetActive(true);

        buildingState = new PlacementState(ID,
                                          grid,
                                          previewSystem,
                                          database,
                                          floorData,
                                          furnitureData,
                                          objectPlacer);

        playerInput.OnClick += PlaceStructure;
        playerInput.OnExit += StopPlacement;
    }

    private void PlaceStructure()
    {
        if (playerInput.IsPointerOverUI())
            return;

        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        buildingState.OnAction(gridPosition);
    }

    // private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex)
    // {
    //     GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 ? floorData : furnitureData;

    //     return selectedData.canPlaceObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size);
    // }

    private void StopPlacement()
    {
        if (buildingState == null)
            return;

        gridVisualization.SetActive(false);
        buildingState.EndState();

        playerInput.OnClick -= PlaceStructure;
        playerInput.OnExit -= StopPlacement;

        lastDetectedPosition = Vector3Int.zero;
        buildingState = null;
    }

    void Update()
    {
        if (buildingState == null)
            return;
        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        if (lastDetectedPosition != gridPosition)
        {
            buildingState.UpdateState(gridPosition);
            lastDetectedPosition = gridPosition;
        }
    }
}
