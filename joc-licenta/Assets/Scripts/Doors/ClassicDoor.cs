using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ușă clasică pe balama.
/// NPC-ul se oprește 0.3s (animație "apăsat mâner") apoi ușa se deschide.
/// Se închide automat după ce toți requesterii au plecat.
/// </summary>
public class ClassicDoor : MonoBehaviour, IDoor
{
    [Header("Hinge Settings")]
    [Tooltip("Transform-ul ușii care se rotește (copilul cu mesh).")]
    public Transform doorPivot;

    [Tooltip("Unghi de deschidere (ex: 90 = deschis complet spre interior).")]
    public float openAngle = 90f;
    public float closedAngle = 0f;

    [Tooltip("Viteza de rotație (grade/secundă).")]
    public float rotationSpeed = 150f;

    [Header("Interaction")]
    [Tooltip("Durata animației de 'apăsat mâner' înainte să se deschidă.")]
    public float handlePressDuration = 0.3f;

    [Tooltip("Delay înainte de închidere după ce toți au trecut.")]
    public float autoCloseDelay = 1.5f;

    [Header("State")]
    [SerializeField] private bool _isLocked = false;

    // ── IDoor ─────────────────────────────────────────────────────────────────
    public bool IsOpen => _isOpen;
    public bool IsLocked => _isLocked;

    // ── Private ───────────────────────────────────────────────────────────────
    private bool _isOpen = false;
    private float _targetAngle;
    private float _currentAngle;
    private Coroutine _closeCoroutine;
    private HashSet<DoorInteractor> _activeRequesters = new HashSet<DoorInteractor>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (doorPivot == null)
            doorPivot = transform;

        _currentAngle = closedAngle;
        _targetAngle = closedAngle;
    }

    void Update()
    {
        if (Mathf.Approximately(_currentAngle, _targetAngle)) return;

        _currentAngle = Mathf.MoveTowards(
            _currentAngle,
            _targetAngle,
            rotationSpeed * Time.deltaTime
        );

        doorPivot.localRotation = Quaternion.Euler(0f, _currentAngle, 0f);
    }

    // ── IDoor implementation ──────────────────────────────────────────────────

    public void Open()
    {
        if (_isLocked) return;
        _isOpen = true;
        _targetAngle = openAngle;

        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
            _closeCoroutine = null;
        }
    }

    public void Close()
    {
        _isOpen = false;
        _targetAngle = closedAngle;
    }

    public void RequestOpen(DoorInteractor requester)
    {
        if (_isLocked) return;
        _activeRequesters.Add(requester);

        // Animație "apăsat mâner" → deschidere cu delay
        StartCoroutine(OpenWithHandlePress());
    }

    public void RequestClose(DoorInteractor requester)
    {
        _activeRequesters.Remove(requester);

        // Închidem doar dacă nu mai are nimeni nevoie
        if (_activeRequesters.Count == 0)
        {
            if (_closeCoroutine != null)
                StopCoroutine(_closeCoroutine);

            _closeCoroutine = StartCoroutine(CloseAfterDelay());
        }
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator OpenWithHandlePress()
    {
        // NPC-ul "apasă mânerul" — ușa tremură ușor
        float elapsed = 0f;
        Vector3 originalPos = doorPivot.localPosition;

        while (elapsed < handlePressDuration)
        {
            elapsed += Time.deltaTime;
            float shake = Mathf.Sin(elapsed * 40f) * 0.002f;
            doorPivot.localPosition = originalPos + new Vector3(shake, 0f, 0f);
            yield return null;
        }

        doorPivot.localPosition = originalPos;
        Open();
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(autoCloseDelay);

        if (_activeRequesters.Count == 0)
            Close();

        _closeCoroutine = null;
    }
}