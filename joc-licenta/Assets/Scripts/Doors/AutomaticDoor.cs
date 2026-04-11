using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Ușă glisantă automată (tip supermarket).
/// Se deschide când un NPC intră în zona trigger și se închide
/// după ce toți au ieșit din zonă.
/// </summary>
public class AutoSlidingDoor : MonoBehaviour, IDoor
{
    [Header("Door Panels")]
    [Tooltip("Panoul stâng al ușii (se mișcă spre stânga).")]
    public Transform leftPanel;
    [Tooltip("Panoul drept al ușii (se mișcă spre dreapta).")]
    public Transform rightPanel;

    [Header("Slide Settings")]
    [Tooltip("Cât de mult se deplasează fiecare panou (metri).")]
    public float slideDistance = 0.9f;
    public float slideSpeed = 3f;

    [Header("Timing")]
    [Tooltip("Delay înainte de închidere după ce toți au ieșit.")]
    public float autoCloseDelay = 1.0f;

    [Header("Trigger Zone")]
    [Tooltip("Collider trigger în fața ușii (zona de detectare NPC).")]
    public Collider triggerZone;

    [Header("State")]
    [SerializeField] private bool _isLocked = false;

    // ── IDoor ─────────────────────────────────────────────────────────────────
    public bool IsOpen => _isOpen;
    public bool IsLocked => _isLocked;

    // ── Private ───────────────────────────────────────────────────────────────
    private bool _isOpen = false;
    private Vector3 _leftClosed, _leftOpen;
    private Vector3 _rightClosed, _rightOpen;
    private Coroutine _closeCoroutine;

    // Contor NPC în zonă — deschis cât timp > 0
    private int _npcsInZone = 0;

    // Pentru RequestOpen/Close (folosit de DoorInteractor)
    private HashSet<DoorInteractor> _activeRequesters = new HashSet<DoorInteractor>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (leftPanel != null)
        {
            _leftClosed = leftPanel.localPosition;
            _leftOpen = _leftClosed + Vector3.left * slideDistance;
        }

        if (rightPanel != null)
        {
            _rightClosed = rightPanel.localPosition;
            _rightOpen = _rightClosed + Vector3.right * slideDistance;
        }

        // Trigger zone opțional — dacă nu e setat, funcționează doar prin DoorInteractor
        if (triggerZone != null)
            triggerZone.isTrigger = true;
    }

    void Update()
    {
        if (leftPanel == null || rightPanel == null) return;

        Vector3 leftTarget = _isOpen ? _leftOpen : _leftClosed;
        Vector3 rightTarget = _isOpen ? _rightOpen : _rightClosed;

        leftPanel.localPosition = Vector3.MoveTowards(
            leftPanel.localPosition, leftTarget, slideSpeed * Time.deltaTime);

        rightPanel.localPosition = Vector3.MoveTowards(
            rightPanel.localPosition, rightTarget, slideSpeed * Time.deltaTime);
    }

    // ── Trigger automat (pentru zona din față ușii) ───────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_isLocked) return;

        // Orice NPC (Customer sau Employee)
        if (other.GetComponent<DoorInteractor>() != null ||
            other.GetComponentInParent<DoorInteractor>() != null)
        {
            _npcsInZone++;

            if (_closeCoroutine != null)
            {
                StopCoroutine(_closeCoroutine);
                _closeCoroutine = null;
            }

            Open();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<DoorInteractor>() != null ||
            other.GetComponentInParent<DoorInteractor>() != null)
        {
            _npcsInZone = Mathf.Max(0, _npcsInZone - 1);

            if (_npcsInZone == 0 && _activeRequesters.Count == 0)
            {
                if (_closeCoroutine != null)
                    StopCoroutine(_closeCoroutine);

                _closeCoroutine = StartCoroutine(CloseAfterDelay());
            }
        }
    }

    // ── IDoor implementation ──────────────────────────────────────────────────

    public void Open()
    {
        if (_isLocked) return;
        _isOpen = true;
    }

    public void Close()
    {
        _isOpen = false;
    }

    public void RequestOpen(DoorInteractor requester)
    {
        if (_isLocked) return;
        _activeRequesters.Add(requester);

        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
            _closeCoroutine = null;
        }

        Open();
    }

    public void RequestClose(DoorInteractor requester)
    {
        _activeRequesters.Remove(requester);

        if (_npcsInZone == 0 && _activeRequesters.Count == 0)
        {
            if (_closeCoroutine != null)
                StopCoroutine(_closeCoroutine);

            _closeCoroutine = StartCoroutine(CloseAfterDelay());
        }
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(autoCloseDelay);

        if (_npcsInZone == 0 && _activeRequesters.Count == 0)
            Close();

        _closeCoroutine = null;
    }
}