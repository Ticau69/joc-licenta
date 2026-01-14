using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    private Vector3 lastPosition;
    [SerializeField] private LayerMask placementLayerMask;
    [SerializeField] private LayerMask selectionLayerMask;

    // Evenimente separate pentru drag-and-place
    public event Action OnClick;           // Click standard (pentru compatibilitate)
    public event Action OnMouseDown;       // Mouse pressed
    public event Action OnMouseUp;         // Mouse released
    public event Action OnRightClick;      // Right click pentru anulare
    public event Action OnExit;
    public event Action OnRotate;

    public event Action<GameObject> OnObjectClicked;

    private bool isMouseDown = false;

    void Update()
    {
        if (Mouse.current != null)
        {
            // Detectăm CLICK STÂNGA
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // 1. Verificăm dacă nu dăm click prin UI
                if (!IsPointerOverUI())
                {
                    // 2. Încercăm să selectăm un obiect 3D
                    HandleObjectSelection();
                }

                // 3. Logica veche pentru Building System
                isMouseDown = true;
                OnMouseDown?.Invoke();
                OnClick?.Invoke();
            }

            // Detectăm mouse up (sfârșitul drag-ului)
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (isMouseDown)
                {
                    OnMouseUp?.Invoke();
                    isMouseDown = false;
                }
            }

            // Detectăm right-click pentru anulare
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                OnRightClick?.Invoke();
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnExit?.Invoke();
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                OnRotate?.Invoke();
            }
        }
    }

    private void HandleObjectSelection()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Ray ray = sceneCamera.ScreenPointToRay(mousePos);
        RaycastHit hit;

        // Tragem raza doar pe layerele de selecție (ex: Default sau Interactive)
        if (Physics.Raycast(ray, out hit, 100, selectionLayerMask))
        {
            Debug.Log($"[Input] Click pe: {hit.collider.name}");

            // Trimitem obiectul lovit către oricine ascultă (ex: UIManager)
            OnObjectClicked?.Invoke(hit.collider.gameObject);
        }
    }

    public bool IsPointerOverUI()
        => EventSystem.current.IsPointerOverGameObject();

    public Vector3 GetSelectedMapPostion()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        mousePos.z = sceneCamera.nearClipPlane;
        Ray ray = sceneCamera.ScreenPointToRay(mousePos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100, placementLayerMask))
        {
            lastPosition = hit.point;
        }
        return lastPosition;
    }

    public bool IsMouseDown() => isMouseDown;
}