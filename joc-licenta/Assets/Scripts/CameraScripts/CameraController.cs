using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    InputSystem inputSystem;

    Vector2 currentMovementInput;
    Vector3 currentMovement;
    bool isMovementPressed;

    public float panSpeed = 20f;
    public float scrollSpeed = 20f;
    public float panBorderThickness = 10f;
    public float minY = 6f;
    public float maxY = 17f;
    public InputAction mousePositionAction;
    public Vector2 panLimit;

    void Awake()
    {
        inputSystem = new InputSystem();

        inputSystem.CameraControl.Move.started += onMovemmentInput;
        inputSystem.CameraControl.Move.canceled += onMovemmentInput;
        inputSystem.CameraControl.Move.performed += onMovemmentInput;
    }

    void onMovemmentInput(InputAction.CallbackContext context)
    {
        currentMovementInput = context.ReadValue<Vector2>();
        currentMovement.x = currentMovementInput.x;
        currentMovement.z = currentMovementInput.y;
        isMovementPressed = currentMovementInput.x != 0 || currentMovementInput.y != 0;
    }

    void OnEnable()
    {
        inputSystem.CameraControl.Enable();

        if (mousePositionAction == null || !mousePositionAction.enabled)
        {
            mousePositionAction = new InputAction(
                type: InputActionType.Value,
                binding: "<Pointer>/position" // Works for mouse & touch
            );
            mousePositionAction.Enable();
        }
    }

    void OnDisable()
    {
        inputSystem.CameraControl.Disable();
        mousePositionAction.Disable();
    }

    void Update()
    {
        // Read the current mouse/touch position in screen coordinates
        Vector2 screenPos = mousePositionAction.ReadValue<Vector2>();

        Vector3 pos = transform.position;

        //Move the camera based on mouse input
        if (screenPos.y >= Screen.height - panBorderThickness)
        {
            pos += Vector3.forward * panSpeed * Time.deltaTime;
        }
        else if (screenPos.y <= panBorderThickness)
        {
            pos -= Vector3.forward * panSpeed * Time.deltaTime;
        }
        else if (screenPos.x >= Screen.width - panBorderThickness)
        {
            pos += Vector3.right * panSpeed * Time.deltaTime;
        }
        else if (screenPos.x <= panBorderThickness)
        {
            pos -= Vector3.right * panSpeed * Time.deltaTime;
        }

        //Move the camera based on keyboard input
        if (isMovementPressed)
        {
            pos += currentMovement * panSpeed * Time.deltaTime;
        }

        //Zoom the camera based on scroll wheel input
        float scroll = inputSystem.CameraControl.Zoom.ReadValue<float>();
        pos.y += scroll * scrollSpeed * 50f * Time.deltaTime;

        pos.x = Mathf.Clamp(pos.x, -panLimit.x, panLimit.x);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        pos.z = Mathf.Clamp(pos.z, -panLimit.y, panLimit.y);

        transform.position = pos;
    }
}
