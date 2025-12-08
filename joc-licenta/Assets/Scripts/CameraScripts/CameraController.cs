using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    InputSystem inputSystem;

    Vector2 currentMovementInput;
    Vector3 currentMovement;
    bool isMovementPressed;
    float currentRotationInput;

    Vector3 targetPosition;
    [Header("Movement Settings")]
    public float panSpeed = 20f;
    public float scrollSpeed = 20f;
    public float panBorderThickness = 5f;
    public Vector2 panLimit;

    [Header("Zoom Settings")]
    public float minY = 6f;
    public float maxY = 17f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 100f;

    [Header("Smoothing")]
    public float smoothTime = 0.2f;

    public InputAction mousePositionAction;
    private Vector3 _moveVelocity = Vector3.zero;
    private float targetRotationY;
    private float _rotateVelocity;

    void Awake()
    {
        inputSystem = new InputSystem();

        inputSystem.CameraControl.Move.started += onMovemmentInput;
        inputSystem.CameraControl.Move.canceled += onMovemmentInput;
        inputSystem.CameraControl.Move.performed += onMovemmentInput;

        inputSystem.CameraControl.Rotate.started += onRotateInput;
        inputSystem.CameraControl.Rotate.canceled += onRotateInput;
        inputSystem.CameraControl.Rotate.performed += onRotateInput;
    }

    void Start()
    {
        targetPosition = transform.position;
        targetRotationY = transform.eulerAngles.y; // Inițializăm cu rotația curentă
    }

    void onMovemmentInput(InputAction.CallbackContext context)
    {
        currentMovementInput = context.ReadValue<Vector2>();
        currentMovement.x = currentMovementInput.x;
        currentMovement.z = currentMovementInput.y;
        isMovementPressed = currentMovementInput.x != 0 || currentMovementInput.y != 0;
    }

    void onRotateInput(InputAction.CallbackContext context)
    {
        currentRotationInput = context.ReadValue<float>();
    }

    void OnEnable()
    {
        inputSystem.CameraControl.Enable();

        /*if (mousePositionAction == null || !mousePositionAction.enabled)
        {
            mousePositionAction = new InputAction(
                type: InputActionType.Value,
                binding: "<Pointer>/position" // Works for mouse & touch
            );
            mousePositionAction.Enable();
        }*/
    }

    void OnDisable()
    {
        inputSystem.CameraControl.Disable();
        mousePositionAction.Disable();
    }

    void HandleRotation()
    {
        // 1. Calculăm ținta rotației
        if (currentRotationInput != 0)
        {
            targetRotationY += currentRotationInput * rotationSpeed * Time.deltaTime;
        }

        // 2. Aplicăm rotația fluidă pe obiect
        float currentY = transform.eulerAngles.y;
        // SmoothDampAngle este esențial pentru a gestiona corect trecerea de la 360 la 0 grade
        float smoothedY = Mathf.SmoothDampAngle(currentY, targetRotationY, ref _rotateVelocity, smoothTime);

        transform.rotation = Quaternion.Euler(60, smoothedY, 0);
    }

    void HandleMovement()
    {
        Vector2 screenPos = mousePositionAction.ReadValue<Vector2>();

        // Calculăm direcțiile FATA și DREAPTA relative la rotația camerei
        // Important: Setăm y=0 pentru a nu intra în pământ când apăsăm W
        Vector3 camForward = transform.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = transform.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDir = Vector3.zero;

        // Mouse Edge Scrolling
        /*if (screenPos.y >= Screen.height - panBorderThickness)
            moveDir += camForward;
        else if (screenPos.y <= panBorderThickness)
            moveDir -= camForward;

        if (screenPos.x >= Screen.width - panBorderThickness)
            moveDir += camRight;
        else if (screenPos.x <= panBorderThickness)
            moveDir -= camRight;*/

        // Keyboard Input (WASD)
        if (isMovementPressed)
        {
            // Combinăm direcțiile relative cu inputul WASD
            moveDir += (camForward * currentMovementInput.y) + (camRight * currentMovementInput.x);
        }

        moveDir.Normalize(); // Previne viteza dublă pe diagonală

        // Adăugăm la poziția țintă
        targetPosition += moveDir * panSpeed * Time.deltaTime;

        // Zoom Logic
        float scroll = inputSystem.CameraControl.Zoom.ReadValue<float>();
        if (scroll != 0)
        {
            // Normalizăm scroll-ul pentru consistență
            float scrollDir = scroll > 0 ? 1 : -1;
            targetPosition.y += scrollDir * scrollSpeed * 10f * Time.deltaTime;
        }

        // Limitări (Clamp)
        targetPosition.x = Mathf.Clamp(targetPosition.x, -panLimit.x, panLimit.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, -panLimit.y, panLimit.y);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        // Aplicăm mișcarea finală
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _moveVelocity, smoothTime);
    }

    void Update()
    {
        HandleRotation();
        HandleMovement();
    }
}
