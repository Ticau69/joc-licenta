using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private LayerMask placementLayerMask;
    [SerializeField] private LayerMask selectionLayerMask;

    // Evenimente separate pentru drag-and-place
    [SerializeField] private InputActionAsset inputActions;
    private InputAction confirmAction;
    private Vector3 lastPosition;
    public event Action OnClick;
    public event Action OnMouseDown;
    public event Action OnMouseUp;
    public event Action OnRightClick;
    public event Action OnExit;
    public event Action OnRotate;
    public event Action OnConfirm;

    public event Action<GameObject> OnObjectClicked;

    private bool isMouseDown = false;
    public bool canInteract = true;

    private void Awake()
    {
        // Găsim acțiunea Confirm din action map-ul Building
        var buildingMap = inputActions.FindActionMap("Building");
        confirmAction = buildingMap?.FindAction("Confirm");
    }

    private void OnDisable()
    {
        DisableConfirm();
    }

    private void OnConfirmPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsPointerOverUI())
            OnConfirm?.Invoke();
    }

    public void EnableConfirm()
    {
        if (confirmAction != null)
        {
            confirmAction.Enable();
            confirmAction.performed += OnConfirmPerformed;
        }
    }

    public void DisableConfirm()
    {
        if (confirmAction != null)
        {
            confirmAction.performed -= OnConfirmPerformed;
            confirmAction.Disable();
        }
    }

    void Update()
    {
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverUI())
                    HandleObjectSelection();

                isMouseDown = true;
                OnMouseDown?.Invoke();
                OnClick?.Invoke();
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (isMouseDown)
                {
                    OnMouseUp?.Invoke();
                    isMouseDown = false;
                }
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
                OnRightClick?.Invoke();
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                OnExit?.Invoke();

            if (Keyboard.current.rKey.wasPressedThisFrame)
                OnRotate?.Invoke();
        }
    }

    private void HandleObjectSelection()
    {
        if (!canInteract) return;

        Vector3 mousePos = Mouse.current.position.ReadValue();
        Ray ray = sceneCamera.ScreenPointToRay(mousePos);

        // Tragem raza doar pe layerele de selecție (ex: Default sau Interactive)
        if (Physics.Raycast(ray, out RaycastHit hit, 100, selectionLayerMask))
        {
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