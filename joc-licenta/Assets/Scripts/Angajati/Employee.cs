using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class Employee : MonoBehaviour
{
    #region Configuration Constants
    private const int MAX_CARRY_CAPACITY = 5;
    private const float WORK_DURATION = 1.0f;
    private const float WANDER_INTERVAL = 3.0f;
    private const float WANDER_RADIUS = 8f;
    private const float DESTINATION_THRESHOLD = 0.5f;
    private const float STORAGE_THRESHOLD_OFFSET = 0.5f;
    private const float HOME_THRESHOLD = 1.0f;
    private const int TASK_CHECK_FRAME_INTERVAL = 30;
    private const float ROTATION_SPEED = 5f;
    #endregion

    #region Notification Variables
    private EmployeeNotification _notificationSystem;
    private string _currentProblemMessage = null;
    private float _problemCooldown = 0f;
    private const float PROBLEM_COOLDOWN_DURATION = 5f; // Nu spam-ui notificări
    #endregion

    #region Inspector Fields
    [Header("Identity")]
    [SerializeField] private string _employeeName;
    [SerializeField] private EmployeeRole _role;
    [SerializeField] private Transform _myWorkStation;
    [SerializeField] private Transform _secondaryTarget;
    [SerializeField] private GameObject boxVisual;
    #endregion

    #region Public Properties
    public string employeeName
    {
        get => _employeeName;
        set => _employeeName = value;
    }

    public EmployeeRole role
    {
        get => _role;
        set => _role = value;
    }

    public Transform myWorkStation
    {
        get => _myWorkStation;
        set => _myWorkStation = value;
    }

    public Transform secondaryTarget
    {
        get => _secondaryTarget;
        set => _secondaryTarget = value;
    }
    #endregion

    #region Private Fields
    private NavMeshAgent agent;
    private bool isWorking = false;
    private Vector3 homePosition;
    private float workTimer = 0f;

    // Restocker specific
    private RestockerStateMachine restockerStateMachine;
    private DoorManager doorManager;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Setup Notification System
        agent = GetComponent<NavMeshAgent>();
        doorManager = new DoorManager();
        restockerStateMachine = new RestockerStateMachine(this, doorManager, MAX_CARRY_CAPACITY);

        // Adaugă:
        _notificationSystem = GetComponent<EmployeeNotification>();

        if (boxVisual != null)
        {
            boxVisual.SetActive(false);
        }
    }

    private void Update()
    {
        // Cooldown pentru probleme
        if (_problemCooldown > 0)
        {
            _problemCooldown -= Time.deltaTime;
        }

        if (!isWorking)
        {
            HandleEndShiftMovement();
            return;
        }

        ExecuteRoleSpecificWork();
    }
    #endregion

    #region Public Methods
    public void AssignRole(EmployeeRole newRole, Transform station)
    {
        _role = newRole;
        _myWorkStation = station;
    }

    public void StartShift(Vector3 spawnPos)
    {
        homePosition = spawnPos;
        isWorking = true;

        _currentProblemMessage = null; // Resetăm memoria din Employee
        _problemCooldown = 0f;         // Resetăm cooldown-ul
        ClearProblem();

        if (_role == EmployeeRole.Restocker && restockerStateMachine != null)
        {
            restockerStateMachine.ResetDailyMemory();
        }

        gameObject.SetActive(true);
        agent.Warp(spawnPos);
    }

    public void EndShift()
    {
        isWorking = false;

        if (boxVisual != null)
        {
            boxVisual.SetActive(false);
        }

        agent.SetDestination(homePosition);
    }

    public void WakeUpAndWork()
    {
        if (_role == EmployeeRole.Restocker)
        {
            restockerStateMachine.WakeUp(agent);
        }
    }

    /// Raportează o problemă vizibilă jucătorului
    public void ReportProblem(string problemMessage)
    {
        // Evită spam de notificări pentru aceeași problemă
        if (_currentProblemMessage == problemMessage) return;
        if (_problemCooldown > 0) return;

        _currentProblemMessage = problemMessage;
        _problemCooldown = PROBLEM_COOLDOWN_DURATION;

        if (_notificationSystem != null)
        {
            _notificationSystem.ShowNotification(problemMessage);
        }
        else
        {
            Debug.LogWarning($"[{employeeName}] EmployeeNotification component missing!");
        }
    }

    /// Șterge problema curentă
    public void ClearProblem()
    {
        _currentProblemMessage = null;
        _notificationSystem?.HideNotification();
    }

    /// /// Verifică dacă angajatul are o problemă activă
    public bool HasActiveProblem => !string.IsNullOrEmpty(_currentProblemMessage);
    #endregion

    #region Private Methods - Role Execution
    private void ExecuteRoleSpecificWork()
    {
        switch (_role)
        {
            case EmployeeRole.Janitor:
                DoJanitorWork();
                break;
            case EmployeeRole.Cashier:
                DoCashierWork();
                break;
            case EmployeeRole.Restocker:
                DoRestockerWork();
                break;
        }
    }

    private void DoJanitorWork()
    {
        WanderBehavior();
    }

    private void DoCashierWork()
    {
        if (_myWorkStation == null) return;

        agent.SetDestination(_myWorkStation.position);

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _myWorkStation.rotation,
                Time.deltaTime * ROTATION_SPEED
            );
        }
    }

    private void DoRestockerWork()
    {
        if (_myWorkStation == null)
        {
            WanderBehavior();
            return;
        }

        restockerStateMachine.Update(
            agent,
            _myWorkStation,
            _secondaryTarget,
            boxVisual,
            ref workTimer
        );
    }
    #endregion

    #region Private Methods - Movement
    private void HandleEndShiftMovement()
    {
        if (Vector3.Distance(transform.position, homePosition) < HOME_THRESHOLD)
        {
            gameObject.SetActive(false);
        }
    }

    private void WanderBehavior()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            workTimer += Time.deltaTime;

            if (workTimer >= WANDER_INTERVAL)
            {
                Vector3 newPos = RandomNavSphere(transform.position, WANDER_RADIUS, -1);
                agent.SetDestination(newPos);
                workTimer = 0;
            }
        }
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        Vector3 randDirection = Random.insideUnitSphere * dist;
        randDirection += origin;

        NavMesh.SamplePosition(randDirection, out NavMeshHit navHit, dist, layermask);
        return navHit.position;
    }
    #endregion

    #region Internal Getters (for RestockerStateMachine)
    internal float DestinationThreshold => DESTINATION_THRESHOLD;
    internal float StorageThresholdOffset => STORAGE_THRESHOLD_OFFSET;
    internal float WorkDuration => WORK_DURATION;
    internal int TaskCheckFrameInterval => TASK_CHECK_FRAME_INTERVAL;
    internal void SetSecondaryTarget(Transform target) => _secondaryTarget = target;
    #endregion

}

#region Supporting Classes

/// <summary>
/// Manages door operations for employees
/// </summary>
public class DoorManager
{
    private SimpleDoorController lastOpenedDoor = null;

    public void OpenDoor(WorkStation station, string context)
    {
        if (station == null)
        {
            Debug.LogWarning($"[DoorManager] Station is NULL in context: {context}");
            return;
        }

        if (station.doorController == null)
        {
            Debug.LogWarning($"[DoorManager] Station '{station.name}' has no doorController assigned!");
            return;
        }

        Debug.Log($"[DoorManager] Opening door at {station.name} ({context})");
        station.doorController.Open();
        lastOpenedDoor = station.doorController;
    }

    public void CloseLastDoor()
    {
        if (lastOpenedDoor != null)
        {
            Debug.Log("[DoorManager] Closing door");
            lastOpenedDoor.Close();
            lastOpenedDoor = null;
        }
    }
}

/// <summary>
/// State machine for Restocker employee logic
/// </summary>
public class RestockerStateMachine
{
    private enum State
    {
        Idle,
        MovingToShelf,
        MovingToStorage,
        WorkingAtLocation
    }

    private enum TaskType
    {
        None,
        Restocking,
        Clearing
    }

    private State currentState = State.Idle;
    private TaskType currentTask = TaskType.None;
    private int productsInHand = 0;
    private ProductType productInHandType = ProductType.None;
    private readonly int maxCarryCapacity;
    private readonly Employee owner;
    private readonly DoorManager doorManager;

    public RestockerStateMachine(Employee owner, DoorManager doorManager, int maxCapacity)
    {
        this.owner = owner;
        this.doorManager = doorManager;
        this.maxCarryCapacity = maxCapacity;
    }

    private string lastReportedProblem = null;

    // Metodă helper pentru raportare probleme
    private void ReportProblem(string problem)
    {
        if (lastReportedProblem == problem) return; // Evită duplicate

        lastReportedProblem = problem;
        owner.ReportProblem(problem);
    }

    private void ClearProblem()
    {
        lastReportedProblem = null;
        owner.ClearProblem();
    }

    // În interiorul clasei RestockerStateMachine
    public void ResetDailyMemory()
    {
        lastReportedProblem = null; // Aici e cheia: uităm ce am zis ieri
        currentState = State.Idle;
        currentTask = TaskType.None;
        // Opțional: Poți reseta și produsele din mână dacă vrei să înceapă "curat"
        productsInHand = 0;
    }

    public void WakeUp(NavMeshAgent agent)
    {
        if (currentState == State.Idle)
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.ResetPath();
            }
            FindTask();
        }
    }

    public void Update(NavMeshAgent agent, Transform workStation, Transform secondaryTarget,
                       GameObject boxVisual, ref float workTimer)
    {
        switch (currentState)
        {
            case State.Idle:
                HandleIdleState(agent, ref workTimer);
                break;

            case State.MovingToShelf:
                HandleMovingToShelf(agent, secondaryTarget, ref workTimer);
                break;

            case State.MovingToStorage:
                HandleMovingToStorage(agent, workStation, ref workTimer);
                break;

            case State.WorkingAtLocation:
                HandleWorking(workStation, secondaryTarget, boxVisual, ref workTimer);
                break;
        }
    }

    private void HandleIdleState(NavMeshAgent agent, ref float workTimer)
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            workTimer += Time.deltaTime;

            if (workTimer >= 3.0f)
            {
                Vector3 newPos = Employee.RandomNavSphere(agent.transform.position, 8f, -1);
                agent.SetDestination(newPos);
                workTimer = 0;
            }
        }

        if (Time.frameCount % owner.TaskCheckFrameInterval == 0)
        {
            FindTask();
        }
    }

    private void HandleMovingToShelf(NavMeshAgent agent, Transform secondaryTarget, ref float workTimer)
    {
        if (secondaryTarget == null)
        {
            currentState = State.Idle;
            return;
        }

        agent.SetDestination(secondaryTarget.position);

        if (!agent.pathPending && agent.remainingDistance < owner.DestinationThreshold)
        {
            WorkStation station = secondaryTarget.GetComponentInParent<WorkStation>();
            if (station != null)
            {
                doorManager.OpenDoor(station, "Shelf");
            }

            currentState = State.WorkingAtLocation;
            workTimer = 0;
        }
    }

    private void HandleMovingToStorage(NavMeshAgent agent, Transform workStation, ref float workTimer)
    {
        agent.SetDestination(workStation.position);

        float threshold = agent.stoppingDistance + owner.StorageThresholdOffset;
        if (!agent.pathPending && agent.remainingDistance < threshold)
        {
            currentState = State.WorkingAtLocation;
            workTimer = 0;
        }
    }

    private void HandleWorking(Transform workStation, Transform secondaryTarget,
                              GameObject boxVisual, ref float workTimer)
    {
        workTimer += Time.deltaTime;

        if (workTimer > owner.WorkDuration)
        {
            ExecuteWorkAction(workStation, secondaryTarget, boxVisual);
        }
    }

    private void FindTask()
    {
        var allShelves = Object.FindObjectsByType<WorkStation>(FindObjectsSortMode.None)
            .Where(x => x.stationType == StationType.Shelf)
            .ToList();

        // Priority 1: Clearing shelves
        var shelvesToClear = allShelves.Where(x => x.NeedsClearing).ToList();
        if (TryAssignClearingTask(shelvesToClear))
        {
            return;
        }

        // Priority 2: Restocking
        var shelvesToStock = allShelves.Where(x => x.NeedsRestocking).ToList();
        TryAssignRestockingTask(shelvesToStock);
    }

    private bool TryAssignClearingTask(List<WorkStation> shelvesToClear)
    {
        if (shelvesToClear.Count == 0) return false;

        WorkStation target = shelvesToClear[Random.Range(0, shelvesToClear.Count)];
        AssignTarget(target);
        currentTask = TaskType.Clearing;
        currentState = State.MovingToShelf;
        Debug.Log($"[Restocker] Going to clear shelf {target.name}");
        return true;
    }

    private bool TryAssignRestockingTask(List<WorkStation> shelvesToStock)
    {
        if (shelvesToStock.Count == 0) return false;

        WorkStation target = shelvesToStock[Random.Range(0, shelvesToStock.Count)];
        AssignTarget(target);
        currentTask = TaskType.Restocking;
        currentState = State.MovingToStorage;
        return true;
    }

    private void AssignTarget(WorkStation station)
    {
        Transform targetTransform = station.interactionPoint != null
            ? station.interactionPoint
            : station.transform;

        owner.SetSecondaryTarget(targetTransform);
    }

    private void ExecuteWorkAction(Transform workStation, Transform secondaryTarget, GameObject boxVisual)
    {
        WorkStation shelfScript = secondaryTarget?.GetComponentInParent<WorkStation>();
        WorkStation storageScript = workStation?.GetComponentInParent<WorkStation>();

        if (currentTask == TaskType.Restocking)
        {
            HandleRestockingAction(shelfScript, storageScript, boxVisual);
        }
        else if (currentTask == TaskType.Clearing)
        {
            HandleClearingAction(shelfScript, storageScript, boxVisual);
        }
    }

    private void HandleRestockingAction(WorkStation shelf, WorkStation storage, GameObject boxVisual)
    {
        if (productsInHand == 0)
        {
            // At storage, picking up products
            if (shelf != null && storage != null)
            {
                ProductType needed = shelf.slot1Product;

                if (storage.TakeFromStorage(needed, maxCarryCapacity))
                {
                    productsInHand = maxCarryCapacity;
                    productInHandType = needed;
                    SetBoxVisibility(boxVisual, true);
                    Debug.Log($"[Restocker] Picked up {needed} from storage. Going to shelf.");

                    ClearProblem();

                    doorManager.CloseLastDoor();
                    currentState = State.MovingToShelf;
                }
                else
                {
                    ReportProblem($"Depozitul nu are {needed}!");

                    Debug.Log($"[Restocker] Storage doesn't have {needed}");
                    doorManager.CloseLastDoor();
                    currentState = State.Idle;
                }
            }
            else
            {
                if (shelf == null) ReportProblem("Raftul a dispărut!");
                if (storage == null) ReportProblem("Depozitul a dispărut!");

                currentState = State.Idle;
            }
        }
        else
        {
            // At shelf, putting products down
            if (shelf != null)
            {
                shelf.AddProduct(productsInHand);
                Debug.Log("[Restocker] Placed products on shelf.");
                productsInHand = 0;
                SetBoxVisibility(boxVisual, false);

                ClearProblem();
            }

            doorManager.CloseLastDoor();
            currentState = State.Idle;
        }
    }

    private void HandleClearingAction(WorkStation shelf, WorkStation storage, GameObject boxVisual)
    {
        if (productsInHand == 0)
        {
            // At shelf, picking up products
            if (shelf != null)
            {
                int taken = shelf.TakeProduct(maxCarryCapacity);

                if (taken > 0)
                {
                    productsInHand = taken;
                    productInHandType = shelf.slot1Product;
                    SetBoxVisibility(boxVisual, true);
                    Debug.Log("[Restocker] Removed old products. Taking to storage.");

                    doorManager.CloseLastDoor();
                    currentState = State.MovingToStorage;
                }
                else
                {
                    doorManager.CloseLastDoor();
                    currentState = State.Idle;
                }
            }
        }
        else
        {
            // At storage, putting products down
            if (storage != null)
            {
                storage.AddToStorage(productInHandType, productsInHand);
                Debug.Log("[Restocker] Returned products to storage.");
            }

            productsInHand = 0;
            SetBoxVisibility(boxVisual, false);
            doorManager.CloseLastDoor();
            currentState = State.Idle;
        }
    }

    private void SetBoxVisibility(GameObject boxVisual, bool visible)
    {
        if (boxVisual != null)
        {
            boxVisual.SetActive(visible);
        }
    }
}

#endregion

public enum EmployeeRole
{
    None,
    Janitor,
    Cashier,
    Restocker
}