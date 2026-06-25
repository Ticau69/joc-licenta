// Fișier: Employee.cs
using UnityEngine;
using UnityEngine.AI;

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
    private const float PROBLEM_COOLDOWN_DURATION = 5f;
    #endregion

    #region Inspector Fields
    [Header("Identity")]
    [SerializeField] private string _employeeName;
    [SerializeField] private EmployeeRole _role;
    [SerializeField] private Transform _myWorkStation;
    [SerializeField] private Transform _secondaryTarget;

    [Header("Visuals (Tools)")]
    [SerializeField] private GameObject boxVisual;
    [SerializeField] private GameObject broomVisual; // <-- Adăugat pentru Janitor
    #endregion

    #region Public Properties
    [Header("Progression & Mood")]
    [SerializeField] private int _level = 1;
    [SerializeField] private int _currentXP = 0;
    [SerializeField] private int _currentSalary = 150;
    [SerializeField] private float _mood = 100f;

    public int XPForNextLevel => _level * 100;

    [Header("Salary Expectations")]
    [SerializeField] private int salaryRangeMin = 30;
    [SerializeField] private int salaryRangeMax = 80;
    private int _salaryExpectationOffset;

    public int level => _level;
    public int currentXP => _currentXP;
    public EmployeeGender gender;

    public int currentSalary
    {
        get => _currentSalary;
        set => _currentSalary = value;
    }

    public float mood => _mood;
    public string employeeName { get => _employeeName; set => _employeeName = value; }
    public EmployeeRole role { get => _role; set => _role = value; }
    public Transform myWorkStation { get => _myWorkStation; set => _myWorkStation = value; }
    public Transform secondaryTarget { get => _secondaryTarget; set => _secondaryTarget = value; }

    public int ExpectedSalaryMin => 50 + (_level * 100) - salaryRangeMin;
    public int ExpectedSalaryMax => 50 + (_level * 100) + salaryRangeMax;
    public int ExpectedSalary => 50 + (_level * 100) + _salaryExpectationOffset;
    #endregion

    #region Private Fields
    private NavMeshAgent agent;
    private Animator animator;
    private bool isWorking = false;
    private Vector3 homePosition;
    private float workTimer = 0f;
    private float baseSpeed = 3.5f;

    // State Machines separate
    private JanitorStateMachine _janitorStateMachine;
    private RestockerStateMachine restockerStateMachine;

    // --- OPTIMIZARE: Hash-uri pentru Animator ---
    private readonly int isWalkingHash = Animator.StringToHash("isWalking");
    private readonly int isCarryingHash = Animator.StringToHash("isCarrying");
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null) animator = GetComponent<Animator>();

        agent = GetComponent<NavMeshAgent>();
        _notificationSystem = GetComponent<EmployeeNotification>();
        baseSpeed = agent.speed;

        _salaryExpectationOffset = Random.Range(salaryRangeMin, salaryRangeMax);

        // Injectăm referințele direct în mașinile de stări externe
        _janitorStateMachine = new JanitorStateMachine(this, animator, broomVisual);
        restockerStateMachine = new RestockerStateMachine(this, MAX_CARRY_CAPACITY);

        if (boxVisual != null) boxVisual.SetActive(false);
    }

    private void Update()
    {
        if (_problemCooldown > 0) _problemCooldown -= Time.deltaTime;

        if (!isWorking)
        {
            HandleEndShiftMovement();
            UpdateAnimations();
            return;
        }

        ExecuteRoleSpecificWork();
        UpdateAnimations();
    }
    #endregion

    #region Public Methods
    public void AddXP(int amount)
    {
        _currentXP += amount;

        if (_currentXP >= XPForNextLevel)
        {
            _currentXP -= XPForNextLevel;
            _level++;

            if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            {
                eventBus.Publish(new ScoreGainedEvent { Amount = 5, Source = "Angajat level up" });
            }

            _mood = Mathf.Clamp(_mood + 20f, 0f, 100f);
        }
    }

    public float GetWorkDurationMultiplier()
    {
        if (_mood >= 80f) return 0.75f;
        if (_mood >= 30f) return 1.0f;
        return 1.5f;
    }

    public float GetMovementSpeedMultiplier()
    {
        if (_mood >= 80f) return 1.25f;
        if (_mood >= 30f) return 1.0f;
        return 0.7f;
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

        _currentProblemMessage = null;
        _problemCooldown = 0f;
        ClearProblem();

        if (_role == EmployeeRole.Restocker && restockerStateMachine != null)
            restockerStateMachine.ResetDailyMemory();

        if (_role == EmployeeRole.Janitor)
            _janitorStateMachine.Reset();

        gameObject.SetActive(true);
        agent.speed = baseSpeed * GetMovementSpeedMultiplier();
        agent.Warp(spawnPos);
    }

    public void EndShift()
    {
        isWorking = false;
        if (boxVisual != null) boxVisual.SetActive(false);

        agent.SetDestination(homePosition);
        CalculateDailyMood();
    }

    private void CalculateDailyMood()
    {
        if (_currentSalary >= ExpectedSalaryMax)
        {
            _mood = Mathf.Clamp(_mood + 15f, 0f, 100f);
        }
        else if (_currentSalary >= ExpectedSalaryMin)
        {
            _mood = Mathf.Clamp(_mood + 5f, 0f, 100f);
        }
        else
        {
            float penalty = (ExpectedSalaryMin - _currentSalary) * 0.2f;
            _mood = Mathf.Clamp(_mood - penalty, 0f, 100f);

            if (_mood <= 0)
            {
                if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
                {
                    eventBus.Publish(new ShowNotificationEvent(
                        "Demisie!",
                        $"{employeeName} a plecat din cauza salariului prea mic ({_currentSalary} RON).",
                        NotificationType.Error,
                        8f
                    ));
                }

                if (EmployeeManager.Instance != null) EmployeeManager.Instance.FireEmployee(this);
            }
        }

        if (ServiceLocator.Instance.TryGet(out IMoneyService money))
        {
            money.TrySpend(_currentSalary);
            if (FinanceManager.Instance != null)
            {
                FinanceManager.Instance.RegisterTransaction(TransactionCategory.Salarii_Angajati, _currentSalary);
            }
        }
    }

    public void WakeUpAndWork()
    {
        if (_role == EmployeeRole.Restocker)
        {
            restockerStateMachine.WakeUp(agent);
        }
    }

    public void ReportProblem(string problemMessage)
    {
        if (_currentProblemMessage == problemMessage) return;
        if (_problemCooldown > 0) return;

        _currentProblemMessage = problemMessage;
        _problemCooldown = PROBLEM_COOLDOWN_DURATION;

        if (_notificationSystem != null) _notificationSystem.ShowNotification(problemMessage);
    }

    public void ClearProblem()
    {
        _currentProblemMessage = null;
        _notificationSystem?.HideNotification();
    }

    public bool HasActiveProblem => !string.IsNullOrEmpty(_currentProblemMessage);
    #endregion

    #region Private Methods - Role Execution
    private void ExecuteRoleSpecificWork()
    {
        switch (_role)
        {
            case EmployeeRole.Janitor:
                _janitorStateMachine.Update(agent);
                break;
            case EmployeeRole.Cashier:
                DoCashierWork();
                break;
            case EmployeeRole.Restocker:
                DoRestockerWork();
                break;
        }
    }

    private void DoCashierWork()
    {
        if (_myWorkStation == null) return;

        agent.SetDestination(_myWorkStation.position);

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            // OPTIMIZARE: Se rotește doar dacă unghiul e mai mare de 1 grad
            if (Quaternion.Angle(transform.rotation, _myWorkStation.rotation) > 1f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    _myWorkStation.rotation,
                    Time.deltaTime * ROTATION_SPEED
                );
            }
        }
    }

    private void DoRestockerWork()
    {
        if (_myWorkStation == null)
        {
            WanderBehavior();
            return;
        }
        restockerStateMachine.Update(agent, _myWorkStation, _secondaryTarget, boxVisual, ref workTimer);
    }
    #endregion

    #region Animations
    private void UpdateAnimations()
    {
        if (animator == null) return;

        // OPTIMIZARE: sqrMagnitude e extrem de rapid pentru procesor față de .magnitude clasic
        bool isMoving = agent.velocity.sqrMagnitude > 0.01f;
        animator.SetBool(isWalkingHash, isMoving);

        bool isCarrying = boxVisual != null && boxVisual.activeSelf;
        animator.SetBool(isCarryingHash, isCarrying);
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