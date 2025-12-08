using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    private Vector3 lastPosition;
    [SerializeField] private LayerMask placementLayerMask;

    // Evenimente separate pentru drag-and-place
    public event Action OnClick;           // Click standard (pentru compatibilitate)
    public event Action OnMouseDown;       // Mouse pressed
    public event Action OnMouseUp;         // Mouse released
    public event Action OnRightClick;      // Right click pentru anulare
    public event Action OnExit;
    public event Action OnRotate;

    private bool isMouseDown = false;

    void Update()
    {
        if (Mouse.current != null)
        {
            // Detectăm mouse down (începutul drag-ului)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                isMouseDown = true;
                OnMouseDown?.Invoke();
                OnClick?.Invoke(); // Pentru compatibilitate cu sistemele vechi
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