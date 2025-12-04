using System;
using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Grid grid;
    [SerializeField] private ObjectDataBase database;
    [SerializeField] private GameObject gridVisualization;
    [SerializeField] private PreviewSystem previewSystem;
    [SerializeField] private ObjectPlacer objectPlacer;

    private GridData floorData, furnitureData;
    private Vector3Int lastDetectedPosition = Vector3Int.zero;
    private IBuldingState buildingState;

    private void Start()
    {
        gridVisualization.SetActive(false);
        floorData = new();
        furnitureData = new();
    }

    private void Update()
    {
        if (buildingState == null)
            return;

        // Update normal de poziție
        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        if (lastDetectedPosition != gridPosition)
        {
            buildingState.UpdateState(gridPosition);
            lastDetectedPosition = gridPosition;
        }
    }

    public void StartPlacement(int ID)
    {
        StopPlacement();
        gridVisualization.SetActive(true);

        buildingState = new PlacementState(
            ID, grid, previewSystem, database,
            floorData, furnitureData, objectPlacer);

        playerInput.OnClick += PlaceStructure;
        playerInput.OnExit += StopPlacement;
        playerInput.OnRotate += RotateStructure; // ADĂUGAT - Subscribe la event
    }

    public void StartRemoving()
    {
        StopPlacement();
        gridVisualization.SetActive(true);

        buildingState = new RemovingState(
            grid, previewSystem,
            floorData, furnitureData,
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

    // METODĂ NOUĂ - Handler pentru rotație
    private void RotateStructure()
    {
        if (buildingState == null)
            return;

        previewSystem.RotatePreview();

        // Re-validăm plasarea cu noua dimensiune
        Vector3 mousePosition = playerInput.GetSelectedMapPostion();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);
        buildingState.UpdateState(gridPosition);
    }

    private void StopPlacement()
    {
        if (buildingState == null)
            return;

        gridVisualization.SetActive(false);
        buildingState.EndState();

        playerInput.OnClick -= PlaceStructure;
        playerInput.OnExit -= StopPlacement;
        playerInput.OnRotate -= RotateStructure; // ADĂUGAT - Unsubscribe

        lastDetectedPosition = Vector3Int.zero;
        buildingState = null;
    }
}