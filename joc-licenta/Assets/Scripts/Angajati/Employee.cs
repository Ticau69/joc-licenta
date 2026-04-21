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
    private const float HOME_THRESHOLD = 2f;
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

    [Header("Progression & Mood")]
    [SerializeField] private int _level = 1;
    [SerializeField] private int _currentXP = 0;
    [SerializeField] private int _currentSalary = 150;
    [SerializeField] private float _mood = 100f; // 0% - 100%

    public int XPForNextLevel => _level * 100;

    [Header("Salary Expectations")]
    [SerializeField] private int salaryRangeMin = 30;  // variație minimă față de baza
    [SerializeField] private int salaryRangeMax = 80;
    private JanitorStateMachine _janitorStateMachine;

    private int _salaryExpectationOffset;

    public int level => _level;
    public int currentXP => _currentXP;

    public int currentSalary
    {
        get => _currentSalary;
        set => _currentSalary = value; // UI-ul va modifica această valoare prin Slider
    }

    public float mood => _mood;

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

    public int ExpectedSalaryMin => 50 + (_level * 100) - salaryRangeMin;
    public int ExpectedSalaryMax => 50 + (_level * 100) + salaryRangeMax;
    public int ExpectedSalary => 50 + (_level * 100) + _salaryExpectationOffset;
    #endregion

    #region Private Fields
    private NavMeshAgent agent;
    private bool isWorking = false;
    private Vector3 homePosition;
    private float workTimer = 0f;
    private float baseSpeed = 3.5f;
    private Animator animator;

    // Restocker specific
    private RestockerStateMachine restockerStateMachine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null) animator = GetComponent<Animator>();

        _salaryExpectationOffset = Random.Range(salaryRangeMin, salaryRangeMax);
        _janitorStateMachine = new JanitorStateMachine(this);

        // Setup Notification System
        agent = GetComponent<NavMeshAgent>();
        restockerStateMachine = new RestockerStateMachine(this, MAX_CARRY_CAPACITY);

        // Adaugă:
        _notificationSystem = GetComponent<EmployeeNotification>();
        baseSpeed = agent.speed;
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
            UpdateAnimations(); // <-- NOU: Animațiile merg și când pleacă acasă!
            return;
        }

        ExecuteRoleSpecificWork();

        // --- NOU: Actualizăm vizualul în fiecare cadru ---
        UpdateAnimations();
    }
    #endregion

    #region Public Methods

    public void AddXP(int amount)
    {
        _currentXP += amount;
        Debug.Log($"[{employeeName}] a primit +{amount} XP. Total: {_currentXP}/{XPForNextLevel}");

        // Verificăm dacă a făcut Level Up
        if (_currentXP >= XPForNextLevel)
        {
            _currentXP -= XPForNextLevel; // Păstrăm restul de XP pentru nivelul următor
            _level++;

            // Când face level up, se simte mândru, deci îi dăm un mic bonus la mood
            _mood = Mathf.Clamp(_mood + 20f, 0f, 100f);

            Debug.Log($"🎉 [{employeeName}] a ajuns la Nivelul {_level}! Acum se așteaptă la un salariu de {ExpectedSalary} RON.");
        }
    }
    //Employee Buffs bazate pe mood
    public float GetWorkDurationMultiplier()
    {
        if (_mood >= 80f) return 0.75f; // Fericit: Termină treaba cu 25% mai repede
        if (_mood >= 30f) return 1.0f;  // Normal: Viteza standard (1x)
        return 1.5f;                    // Supărat: Durează cu 50% mai mult să facă ceva!
    }

    public float GetMovementSpeedMultiplier()
    {
        if (_mood >= 80f) return 1.25f; // Fericit: Fuge pe hartă (1.25x viteză)
        if (_mood >= 30f) return 1.0f;  // Normal
        return 0.7f;                    // Supărat: Merge târșâit
    }

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

        if (_role == EmployeeRole.Janitor)
            _janitorStateMachine.Reset();

        gameObject.SetActive(true);
        agent.speed = baseSpeed * GetMovementSpeedMultiplier();
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

        // --- NOU: CALCULUL MORALULUI LA FINAL DE ZI ---
        CalculateDailyMood();
    }

    private void CalculateDailyMood()
    {
        int difference = _currentSalary - ExpectedSalary;

        if (_currentSalary >= ExpectedSalaryMax)
        {
            // Plătit foarte bine
            _mood = Mathf.Clamp(_mood + 15f, 0f, 100f);
            Debug.Log($"[{employeeName}] este foarte fericit pentru bonus! Mood: {_mood}%");
        }
        else if (_currentSalary >= ExpectedSalaryMin)
        {
            // Plătit corect
            _mood = Mathf.Clamp(_mood + 5f, 0f, 100f);
            Debug.Log($"[{employeeName}] este mulțumit de salariu. Mood: {_mood}%");
        }
        else
        {
            // Sub-plătit
            float penalty = (ExpectedSalaryMin - _currentSalary) * 0.2f;
            _mood = Mathf.Clamp(_mood - penalty, 0f, 100f);

            Debug.LogWarning($"[{employeeName}] este supărat pe salariul mic ({_currentSalary} vs {ExpectedSalary})! Mood scade la: {_mood}%");

            if (_mood <= 0)
            {
                Debug.LogError($"🚨 [{employeeName}] și-a dat demisia din cauza condițiilor de muncă!");

                // --- NOU: TRIMITEM NOTIFICARE CĂTRE JUCĂTOR ---
                if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
                {
                    eventBus.Publish(new ShowNotificationEvent(
                        "Demisie!",
                        $"{employeeName} a plecat din cauza salariului prea mic ({_currentSalary} RON).",
                        NotificationType.Error,
                        8f // Îl lăsăm mai mult pe ecran ca să fie sigur văzut
                    ));
                }

                if (EmployeeManager.Instance != null)
                {
                    EmployeeManager.Instance.FireEmployee(this);
                }
            }
        }

        if (ServiceLocator.Instance.TryGet(out IMoneyService money))
        {
            // Atenție: Dacă metoda ta din IMoneyService se numește altfel (ex: Subtract, Spend, Remove), modifică cuvântul "Spend" de mai jos:
            money.TrySpend(_currentSalary);
            if (FinanceManager.Instance != null)
            {
                // Trimitem salariile plătite astăzi către contabilitate!
                FinanceManager.Instance.RegisterTransaction(TransactionCategory.Salarii_Angajati, _currentSalary);
            }
            Debug.Log($"S-au plătit {_currentSalary} RON pentru salariul lui {employeeName}.");
        }
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
        _janitorStateMachine.Update(agent);
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

    #region Animations

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // 1. Verificăm dacă agentul se deplasează efectiv (dacă viteza e mai mare decât o marjă foarte mică)
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool("isWalking", isMoving);

        // 2. Verificăm dacă angajatul cară o cutie 
        // Știm sigur asta dacă boxVisual a fost activat de RestockerStateMachine
        bool isCarrying = boxVisual != null && boxVisual.activeSelf;

        // Trimitem asta către Animator (Va trebui să creezi acest parametru)
        animator.SetBool("isCarrying", isCarrying);
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
    internal float WorkDuration => WORK_DURATION * GetWorkDurationMultiplier();
    internal int TaskCheckFrameInterval => TASK_CHECK_FRAME_INTERVAL;
    internal void SetSecondaryTarget(Transform target) => _secondaryTarget = target;
    #endregion

}

#region Supporting Classes

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

    public RestockerStateMachine(Employee owner, int maxCapacity)
    {
        this.owner = owner;
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

            currentState = State.WorkingAtLocation;
            workTimer = 0;
        }
    }

    private void HandleMovingToStorage(NavMeshAgent agent, Transform workStation, ref float workTimer)
    {
        agent.SetDestination(workStation.position);

        // NOU: Am mărit toleranța la distanță pentru a compensa coliziunea raftului fizic!
        float threshold = agent.stoppingDistance + owner.StorageThresholdOffset + 0.8f;

        if (!agent.pathPending && agent.remainingDistance <= threshold)
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
        Debug.Log($"[Restocker] Rafturi de reumplut: {shelvesToStock.Count}");

        if (shelvesToStock.Count == 0) return false;

        if (ServiceLocator.Instance.TryGet(out IInventoryService inventory))
        {
            foreach (WorkStation targetShelf in shelvesToStock)
            {
                ProductType needed = targetShelf.slotProduct;
                StorageRacks rackWithMarfa = inventory.FindRackWithProduct(needed);

                Debug.Log($"[Restocker] Raft: {targetShelf.name} | Produs: {needed} | Rack gasit: {rackWithMarfa != null}");

                if (rackWithMarfa != null)
                {
                    AssignTarget(targetShelf);
                    owner.myWorkStation = rackWithMarfa.transform;
                    currentTask = TaskType.Restocking;
                    currentState = State.MovingToStorage;
                    ClearProblem();
                    return true;
                }
            }

            ReportProblem("Nu avem marfa necesară în depozit!");
            return false;
        }

        return false;
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
        // Raftul de magazin e WorkStation
        WorkStation shelfScript = secondaryTarget?.GetComponentInParent<WorkStation>();

        // NOU: Nu mai forțăm depozitul să fie WorkStation, îl transmitem ca Transform
        if (currentTask == TaskType.Restocking)
        {
            HandleRestockingAction(shelfScript, workStation, boxVisual);
        }
        else if (currentTask == TaskType.Clearing)
        {
            HandleClearingAction(shelfScript, workStation, boxVisual);
        }
    }

    private void HandleRestockingAction(WorkStation shelf, Transform storageTransform, GameObject boxVisual)
    {
        if (productsInHand == 0)
        {
            // Căutăm componenta StorageRack pe obiectul de depozit!
            StorageRacks rack = storageTransform?.GetComponentInParent<StorageRacks>();

            if (shelf != null && rack != null)
            {
                ProductType needed = shelf.slotProduct;
                int amountTaken = rack.TakeProduct(needed, maxCarryCapacity);

                if (amountTaken > 0)
                {
                    productsInHand = amountTaken;
                    productInHandType = needed;
                    SetBoxVisibility(boxVisual, true);

                    ClearProblem();
                    currentState = State.MovingToShelf;
                }
                else
                {
                    ReportProblem("Cutia s-a golit fix când am ajuns!");
                    currentState = State.Idle;
                }
            }
            else
            {
                if (shelf == null) ReportProblem("Raftul din magazin a dispărut!");
                if (rack == null) ReportProblem("Raftul de depozit e invalid!");
                currentState = State.Idle;
            }
        }
        else
        {
            // Punem pe raftul din magazin
            if (shelf != null)
            {
                shelf.AddProduct(productsInHand);
                productsInHand = 0;
                owner.AddXP(20);
                SetBoxVisibility(boxVisual, false);
                ClearProblem();
            }

            currentState = State.Idle;
        }
    }

    // Am schimbat și aici al doilea parametru în Transform
    private void HandleClearingAction(WorkStation shelf, Transform storageTransform, GameObject boxVisual)
    {
        if (productsInHand == 0)
        {
            if (shelf != null)
            {
                int taken = shelf.TakeProduct(maxCarryCapacity);
                if (taken > 0)
                {
                    productsInHand = taken;
                    productInHandType = shelf.slotProduct;
                    SetBoxVisibility(boxVisual, true);

                    if (ServiceLocator.Instance.TryGet(out IInventoryService inventory))
                    {
                        StorageRacks emptyRack = inventory.FindRackWithSpace(productInHandType);
                        if (emptyRack != null)
                        {
                            owner.myWorkStation = emptyRack.transform;
                            currentState = State.MovingToStorage;
                            return;
                        }
                    }

                    ReportProblem("Depozitul e plin! Am aruncat marfa.");
                    productsInHand = 0;
                    SetBoxVisibility(boxVisual, false);
                    currentState = State.Idle;
                }
                else
                {
                    currentState = State.Idle;
                }
            }
        }
        else
        {
            // Suntem în depozit, punem marfa pe noul raft
            StorageRacks rack = storageTransform?.GetComponentInParent<StorageRacks>();
            if (rack != null)
            {
                rack.AddProduct(productInHandType, productsInHand);
            }

            productsInHand = 0;
            SetBoxVisibility(boxVisual, false);
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