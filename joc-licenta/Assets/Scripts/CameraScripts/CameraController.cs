using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 20f;
    public float scrollSpeed = 20f;

    [Header("Zoom Settings")]
    public float minY = 6f;
    public float maxY = 17f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 100f;

    [Header("Smoothing")]
    public float smoothTime = 0.2f;

    [Header("Auto Limits")]
    [Tooltip("Trage aici podeaua/podelele de start din scenă.")]
    public List<Renderer> mapRenderers = new List<Renderer>();
    public bool autoCalculateLimits = true;

    // ── Private ───────────────────────────────────────────────────────────────

    private InputSystem _input;

    private Vector2 _moveInput;
    private float _rotateInput;

    private Vector3 _targetPosition;
    private float _targetRotationY;

    private Vector3 _moveVelocity = Vector3.zero;
    private float _rotateVelocity;

    private Bounds _cameraLimits;
    private bool _limitsValid = false; // protecție când mapRenderers e gol

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _input = new InputSystem();

        _input.CameraControl.Move.started += OnMoveInput;
        _input.CameraControl.Move.canceled += OnMoveInput;
        _input.CameraControl.Move.performed += OnMoveInput;

        _input.CameraControl.Rotate.started += OnRotateInput;
        _input.CameraControl.Rotate.canceled += OnRotateInput;
        _input.CameraControl.Rotate.performed += OnRotateInput;
    }

    void Start()
    {
        _targetPosition = transform.position;
        _targetRotationY = transform.eulerAngles.y;

        RecalculateLimits();
    }

    void OnEnable() => _input.CameraControl.Enable();
    void OnDisable() => _input.CameraControl.Disable();

    void OnDestroy()
    {
        _input.CameraControl.Move.started -= OnMoveInput;
        _input.CameraControl.Move.canceled -= OnMoveInput;
        _input.CameraControl.Move.performed -= OnMoveInput;

        _input.CameraControl.Rotate.started -= OnRotateInput;
        _input.CameraControl.Rotate.canceled -= OnRotateInput;
        _input.CameraControl.Rotate.performed -= OnRotateInput;

        _input.Dispose();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        bool hasInput = _moveInput != Vector2.zero || _rotateInput != 0f
                        || Mathf.Abs(_input.CameraControl.Zoom.ReadValue<float>()) > 0.01f;
        bool isMoving = _moveVelocity.sqrMagnitude > 0.0001f
                        || Mathf.Abs(_rotateVelocity) > 0.01f;

        if (!hasInput && !isMoving) return;

        HandleRotation(dt);
        HandleMovement(dt);
    }

    // ── Input Callbacks ───────────────────────────────────────────────────────

    private void OnMoveInput(InputAction.CallbackContext ctx)
        => _moveInput = ctx.ReadValue<Vector2>();

    private void OnRotateInput(InputAction.CallbackContext ctx)
        => _rotateInput = ctx.ReadValue<float>();

    // ── Movement & Rotation ───────────────────────────────────────────────────

    private void HandleRotation(float dt)
    {
        if (_rotateInput != 0f)
            _targetRotationY += _rotateInput * rotationSpeed * dt;

        float currentY = transform.eulerAngles.y;
        float smoothedY = Mathf.SmoothDampAngle(
            currentY, _targetRotationY,
            ref _rotateVelocity, smoothTime,
            Mathf.Infinity, dt);

        transform.rotation = Quaternion.Euler(60f, smoothedY, 0f);
    }

    private void HandleMovement(float dt)
    {
        // ── Direcții relative la rotația camerei ─────────────────────────────
        Vector3 camForward = transform.forward; camForward.y = 0f; camForward.Normalize();
        Vector3 camRight = transform.right; camRight.y = 0f; camRight.Normalize();

        // ── WASD ─────────────────────────────────────────────────────────────
        if (_moveInput != Vector2.zero)
        {
            Vector3 moveDir = camForward * _moveInput.y + camRight * _moveInput.x;

            // Normalizăm doar dacă vectorul e non-zero (evităm NaN pe Vector3.zero)
            if (moveDir.sqrMagnitude > 0.001f)
                moveDir.Normalize();

            _targetPosition += moveDir * panSpeed * dt;
        }

        // ── Scroll / Zoom ─────────────────────────────────────────────────────
        float scroll = _input.CameraControl.Zoom.ReadValue<float>();
        if (scroll != 0f)
        {
            float scrollDir = scroll > 0f ? 1f : -1f;
            _targetPosition.y += scrollDir * scrollSpeed * 10f * dt;
        }

        // ── Clamp ─────────────────────────────────────────────────────────────
        if (_limitsValid)
        {
            _targetPosition.x = Mathf.Clamp(_targetPosition.x, _cameraLimits.min.x, _cameraLimits.max.x);
            _targetPosition.z = Mathf.Clamp(_targetPosition.z, _cameraLimits.min.z, _cameraLimits.max.z);
        }
        _targetPosition.y = Mathf.Clamp(_targetPosition.y, minY, maxY);

        // ── SmoothDamp final ──────────────────────────────────────────────────
        transform.position = Vector3.SmoothDamp(
            transform.position, _targetPosition,
            ref _moveVelocity, smoothTime,
            Mathf.Infinity, dt);
    }

    // ── Limits ────────────────────────────────────────────────────────────────

    public void AddMapRenderer(Renderer newRenderer)
    {
        if (newRenderer == null || mapRenderers.Contains(newRenderer)) return;
        mapRenderers.Add(newRenderer);
        RecalculateLimits();
    }

    public void RecalculateLimits()
    {
        if (!autoCalculateLimits || mapRenderers.Count == 0)
        {
            _limitsValid = false;
            return;
        }

        Bounds combined = mapRenderers[0].bounds;

        for (int i = 1; i < mapRenderers.Count; i++)
        {
            if (mapRenderers[i] != null)
                combined.Encapsulate(mapRenderers[i].bounds);
        }

        float margin = 2f;
        combined.Expand(new Vector3(margin * 2f, 0f, margin * 2f));

        _cameraLimits = combined;
        _limitsValid = true;

        Debug.Log($"[Camera] Limite recalculate — {mapRenderers.Count} parcele.");
    }
}