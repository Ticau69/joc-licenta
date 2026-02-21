using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WorkStation))]
public class CashRegisterQueue : MonoBehaviour
{
    [Header("Queue")]
    [SerializeField] private int maxQueueSize = 6;
    [SerializeField] private float queueSpacing = 1.1f;
    [SerializeField] private Transform[] queueSpots; // optional; dacă e gol, se generează în spatele payPoint

    [Header("Checkout")]
    [SerializeField] private float cashierDetectRadius = 1.5f;
    [SerializeField] private float checkoutCooldown = 0.25f;

    private WorkStation _ws;
    private readonly List<CustomerAI> _queue = new();
    private float _cooldown;

    private IMoneyService _money;
    private IEconomyService _economy; // optional

    public int QueueCount => _queue.Count;

    // ── NOU: expus pentru CustomerAI să poată verifica dacă coada e plină ──
    public int MaxQueueSize => maxQueueSize;

    /// <summary>
    /// Returnează poziția în coadă unde s-ar așeza clientul următor.
    /// Folosit de CustomerAI pentru a calcula distanța corectă față de coadă.
    /// </summary>
    public Vector3 GetNextQueuePosition()
    {
        int nextIndex = _queue.Count; // indexul spotului unde s-ar băga clientul nou

        // 1) Spot manual setat în Inspector
        if (queueSpots != null && nextIndex < queueSpots.Length && queueSpots[nextIndex] != null)
            return queueSpots[nextIndex].position;

        // 2) Fallback: calculăm poziția în spatele interactionPoint (același calcul ca GetQueueSpot)
        if (_ws != null && _ws.interactionPoint != null)
        {
            Vector3 backDir = -_ws.interactionPoint.forward;
            return _ws.interactionPoint.position + backDir * (queueSpacing * (nextIndex + 1));
        }

        return transform.position;
    }

    private void Awake()
    {
        _ws = GetComponent<WorkStation>();
        if (_ws.stationType != StationType.CashRegister)
            Debug.LogWarning($"[{name}] CashRegisterQueue pe un WorkStation care nu e CashRegister!");

        ServiceLocator.Instance.TryGet(out _money);
        ServiceLocator.Instance.TryGet(out _economy);
    }

    private void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        if (_queue.Count == 0) return;
        if (_cooldown > 0f) return;

        // Dacă primul client e la punctul de plată + avem cashier, procesăm
        var first = _queue[0];
        if (first == null)
        {
            _queue.RemoveAt(0);
            UpdateQueueDestinations();
            return;
        }

        if (!IsCashierPresent()) return;
        if (!first.IsAtDestination()) return;

        int total = first.CalculateTotalPriceRON();
        if (total > 0 && _money != null)
            _money.Add(total);

        first.OnCheckoutComplete();
        _queue.RemoveAt(0);

        UpdateQueueDestinations();
        _cooldown = checkoutCooldown;
    }

    public bool TryEnqueue(CustomerAI customer)
    {
        if (customer == null) return false;
        if (_queue.Count >= maxQueueSize) return false;
        if (_queue.Contains(customer)) return true;

        _queue.Add(customer);
        UpdateQueueDestinations();
        return true;
    }

    private void UpdateQueueDestinations()
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            var c = _queue[i];
            if (c == null) continue;

            Transform spot = GetQueueSpot(i);
            c.SetQueueTarget(spot);
        }
    }

    private Transform GetQueueSpot(int index)
    {
        // 1) dacă ai spots setate manual, folosește-le (inclusiv index 0)
        if (queueSpots != null && index >= 0 && index < queueSpots.Length && queueSpots[index] != null)
            return queueSpots[index];

        // 2) fallback dacă nu ai spots: generăm în spatele interactionPoint
        if (_ws.interactionPoint == null)
            return transform;

        Vector3 backDir = -_ws.interactionPoint.forward;
        Vector3 pos = _ws.interactionPoint.position + backDir * (queueSpacing * (index + 1));

        var go = new GameObject($"{name}_QueueSpot_{index}");
        go.transform.position = pos;
        go.transform.rotation = _ws.interactionPoint.rotation;
        go.transform.SetParent(transform);
        return go.transform;
    }

    private bool IsCashierPresent()
    {
        if (_ws.interactionPoint == null) return false;

        var employees = FindObjectsByType<Employee>(FindObjectsSortMode.None);
        foreach (var e in employees)
        {
            if (e == null) continue;
            if (e.role != EmployeeRole.Cashier) continue;
            if (e.myWorkStation == null) continue;

            float dStation = Vector3.Distance(e.myWorkStation.position, _ws.interactionPoint.position);
            if (dStation > 0.75f) continue;

            float d = Vector3.Distance(e.transform.position, _ws.interactionPoint.position);
            if (d <= cashierDetectRadius) return true;
        }
        return false;
    }
}