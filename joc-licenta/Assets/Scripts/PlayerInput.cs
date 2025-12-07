using System;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    private Vector3 lastPosition;
    [SerializeField] private LayerMask placementLayerMask;

    public event Action OnClick, OnExit;
    public event Action OnRotate;

    void Update()
    {

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnClick?.Invoke();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OnExit?.Invoke();
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            OnRotate?.Invoke();
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

}
