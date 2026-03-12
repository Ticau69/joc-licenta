using System.Collections.Generic;
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

    [Header("Auto Limits")]
    [Tooltip("Trage aici podeaua/podelele de start din scenă")]
    public List<Renderer> mapRenderers = new List<Renderer>();
    public bool autoCalculateLimits = true;

    // NOU: O cutie limitatoare precisă (înlocuiește panLimit-ul vechi)
    private Bounds _cameraLimits;

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
        targetRotationY = transform.eulerAngles.y;

        // Apelăm funcția la începutul jocului
        RecalculateLimits();
    }

    // Funcție pe care o apelăm când se deblochează o parcelă nouă
    public void AddMapRenderer(Renderer newRenderer)
    {
        if (newRenderer != null && !mapRenderers.Contains(newRenderer))
        {
            mapRenderers.Add(newRenderer);
            RecalculateLimits();
        }
    }

    public void RecalculateLimits()
    {
        if (autoCalculateLimits && mapRenderers.Count > 0)
        {
            // 1. Luăm forma primei podele din listă
            Bounds combinedBounds = mapRenderers[0].bounds;

            // 2. O extindem ca să cuprindă și restul podelelor adăugate
            for (int i = 1; i < mapRenderers.Count; i++)
            {
                if (mapRenderers[i] != null)
                {
                    combinedBounds.Encapsulate(mapRenderers[i].bounds);
                }
            }

            // 3. Adăugăm marginea de siguranță (panBorder)
            float margin = 5f;
            combinedBounds.Expand(new Vector3(margin * 2, 0, margin * 2));

            _cameraLimits = combinedBounds;

            Debug.Log($"[Camera] Limite asimetrice recalculate! Acoperă {mapRenderers.Count} parcele.");
        }
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
            targetRotationY += currentRotationInput * rotationSpeed * Time.unscaledDeltaTime;
        }

        // 2. Aplicăm rotația fluidă pe obiect
        float currentY = transform.eulerAngles.y;
        // SmoothDampAngle este esențial pentru a gestiona corect trecerea de la 360 la 0 grade
        float smoothedY = Mathf.SmoothDampAngle(currentY, targetRotationY, ref _rotateVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        transform.rotation = Quaternion.Euler(60, smoothedY, 0);
    }

    void HandleMovement()
    {
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
            moveDir += (camForward * currentMovementInput.y) + (camRight * currentMovementInput.x);
        }

        moveDir.Normalize(); // Previne viteza dublă pe diagonală

        // Adăugăm la poziția țintă
        targetPosition += moveDir * panSpeed * Time.unscaledDeltaTime;

        // Zoom Logic
        float scroll = inputSystem.CameraControl.Zoom.ReadValue<float>();
        if (scroll != 0)
        {
            // Normalizăm scroll-ul pentru consistență
            float scrollDir = scroll > 0 ? 1 : -1;
            targetPosition.y += scrollDir * scrollSpeed * 10f * Time.unscaledDeltaTime;
        }

        // Limitări (Clamp)
        targetPosition.x = Mathf.Clamp(targetPosition.x, _cameraLimits.min.x, _cameraLimits.max.x);
        targetPosition.z = Mathf.Clamp(targetPosition.z, _cameraLimits.min.z, _cameraLimits.max.z);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        // Aplicăm mișcarea finală
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _moveVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    void Update()
    {
        HandleRotation();
        HandleMovement();
    }
}
