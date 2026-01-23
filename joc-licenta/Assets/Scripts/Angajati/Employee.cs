using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic; // Necesar pentru List

[RequireComponent(typeof(NavMeshAgent))]
public class Employee : MonoBehaviour
{
    // ... (Variabilele tale existente rămân la fel) ...
    [Header("Identitate")]
    public string employeeName;
    public EmployeeRole role;
    public Transform myWorkStation;
    public Transform secondaryTarget;
    public GameObject boxVisual;

    private enum RestockerState
    {
        Idle,               // Stă degeaba / Se plimbă
        MovingToShelf,      // Merge spre un raft (să pună sau să ia marfă)
        MovingToStorage,    // Merge spre depozit (să pună sau să ia marfă)
        WorkingAtLocation   // A ajuns și execută acțiunea (animație/timp)
    }
    private enum TaskType
    {
        None,       // Nimic
        Restocking, // Aprovizionare (Depozit -> Raft)
        Clearing    // Curățare (Raft -> Depozit)
    }
    private RestockerState currentState = RestockerState.Idle;
    private TaskType currentTask = TaskType.None;

    private int productsInHand = 0;
    private ProductType productInHandType = ProductType.None;
    private int maxCarryCapacity = 5;

    private NavMeshAgent agent;
    private bool isWorking = false;
    private Vector3 homePosition;
    private float workTimer = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (boxVisual != null) boxVisual.SetActive(false);
    }

    // ... (Metodele AssignRole, StartShift, EndShift rămân la fel) ...
    public void AssignRole(EmployeeRole newRole, Transform station) { role = newRole; myWorkStation = station; }
    public void StartShift(Vector3 spawnPos) { homePosition = spawnPos; isWorking = true; gameObject.SetActive(true); agent.Warp(spawnPos); }
    public void EndShift() { isWorking = false; if (boxVisual != null) boxVisual.SetActive(false); agent.SetDestination(homePosition); }

    void Update()
    {
        if (!isWorking)
        {
            if (Vector3.Distance(transform.position, homePosition) < 1.0f) gameObject.SetActive(false);
            return;
        }

        switch (role)
        {
            case EmployeeRole.Janitor: DoJanitorWork(); break;
            case EmployeeRole.Cashier: DoCashierWork(); break;
            case EmployeeRole.Restocker: DoRestockerWork(); break;
        }
    }

    public void WakeUpAndWork()
    {
        if (role == EmployeeRole.Restocker && currentState == RestockerState.Idle)
        {
            if (agent != null && agent.isActiveAndEnabled) agent.ResetPath();
            workTimer = 0;
            FindTask();
        }
    }

    // --- LOGICĂ RESTOCKER ACTUALIZATĂ ---
    private void DoRestockerWork()
    {
        if (myWorkStation == null) { WanderBehavior(); return; }

        switch (currentState)
        {
            case RestockerState.Idle:
                WanderBehavior();
                if (Time.frameCount % 30 == 0) FindTask(); // Căutăm de muncă
                break;

            case RestockerState.MovingToShelf:
                if (secondaryTarget == null) { currentState = RestockerState.Idle; return; }

                agent.SetDestination(secondaryTarget.position);
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    currentState = RestockerState.WorkingAtLocation;
                    workTimer = 0;
                }
                break;

            case RestockerState.MovingToStorage:
                agent.SetDestination(myWorkStation.position);
                // Toleranță mai mare la depozit
                if (!agent.pathPending && agent.remainingDistance < agent.stoppingDistance + 0.5f)
                {
                    currentState = RestockerState.WorkingAtLocation;
                    workTimer = 0;
                }
                break;

            case RestockerState.WorkingAtLocation:
                workTimer += Time.deltaTime;
                if (workTimer > 1.0f) // Timp de muncă (încărcare/descărcare)
                {
                    HandleWorkAction();
                }
                break;
        }
    }

    private void FindTask()
    {
        var allShelves = FindObjectsByType<WorkStation>(FindObjectsSortMode.None)
            .Where(x => x.stationType == StationType.Shelf).ToList();

        // PRIORITATE 1: Curățarea rafturilor (Când jucătorul schimbă produsul)
        var shelvesToClear = allShelves.Where(x => x.NeedsClearing).ToList();

        if (shelvesToClear.Count > 0)
        {
            WorkStation target = shelvesToClear[Random.Range(0, shelvesToClear.Count)];
            AssignTarget(target);
            currentTask = TaskType.Clearing;
            currentState = RestockerState.MovingToShelf; // Mergem întâi la raft să luăm marfa
            Debug.Log($"[Angajat] Mă duc să golesc raftul {target.name}");
            return;
        }

        // PRIORITATE 2: Aprovizionare (Restock)
        var shelvesToStock = allShelves.Where(x => x.NeedsRestocking).ToList();

        if (shelvesToStock.Count > 0)
        {
            WorkStation target = shelvesToStock[Random.Range(0, shelvesToStock.Count)];
            AssignTarget(target);
            currentTask = TaskType.Restocking;
            currentState = RestockerState.MovingToStorage; // Mergem întâi la depozit să luăm marfa
            // Debug.Log($"[Angajat] Mă duc să aprovizionez raftul {target.name}");
            return;
        }
    }

    private void AssignTarget(WorkStation station)
    {
        if (station.interactionPoint != null) secondaryTarget = station.interactionPoint;
        else secondaryTarget = station.transform;
    }

    private void HandleWorkAction()
    {
        WorkStation shelfScript = null;
        if (secondaryTarget != null) shelfScript = secondaryTarget.GetComponentInParent<WorkStation>();
        WorkStation storageScript = myWorkStation.GetComponentInParent<WorkStation>();

        // == SCENARIUL 1: APROVIZIONARE (RESTOCKING) ==
        if (currentTask == TaskType.Restocking)
        {
            // CUM ȘTIM UNDE SUNTEM?
            // Dacă nu avem produse în mână, înseamnă că abia am ajuns la DEPOZIT să luăm.
            // Nu mai verificăm Vector3.Distance, avem încredere în StateMachine.
            if (productsInHand == 0)
            {
                if (shelfScript != null)
                {
                    ProductType needed = shelfScript.slot1Product;

                    // Încercăm să luăm marfa
                    if (storageScript != null && storageScript.TakeFromStorage(needed, maxCarryCapacity))
                    {
                        productsInHand = maxCarryCapacity;
                        productInHandType = needed;
                        if (boxVisual != null) boxVisual.SetActive(true);

                        Debug.Log($"[Angajat] Am luat {needed} din depozit. Plec la raft.");
                        currentState = RestockerState.MovingToShelf;
                    }
                    else
                    {
                        Debug.Log($"[Angajat] Depozitul nu are {needed} sau nu există scriptul!");
                        currentState = RestockerState.Idle;
                    }
                }
            }
            // Dacă AVEM produse în mână, înseamnă că suntem la RAFT să le punem.
            else
            {
                if (shelfScript != null)
                {
                    shelfScript.AddProduct(productsInHand);
                    Debug.Log($"[Angajat] Am pus marfa pe raft.");

                    productsInHand = 0;
                    if (boxVisual != null) boxVisual.SetActive(false);
                }
                currentState = RestockerState.Idle; // Gata tura
            }
        }

        // == SCENARIUL 2: CURĂȚARE (CLEARING) ==
        else if (currentTask == TaskType.Clearing)
        {
            // Aici e invers: 
            // Dacă NU avem produse, suntem la RAFT să le scoatem.
            if (productsInHand == 0)
            {
                if (shelfScript != null)
                {
                    int taken = shelfScript.TakeProduct(maxCarryCapacity);

                    if (taken > 0)
                    {
                        productsInHand = taken;
                        productInHandType = shelfScript.slot1Product;
                        if (boxVisual != null) boxVisual.SetActive(true);

                        Debug.Log($"[Angajat] Am scos marfa veche. O duc la depozit.");
                        currentState = RestockerState.MovingToStorage;
                    }
                    else
                    {
                        currentState = RestockerState.Idle;
                    }
                }
            }
            // Dacă AVEM produse, suntem la DEPOZIT să le lăsăm.
            else
            {
                if (storageScript != null)
                {
                    storageScript.AddToStorage(productInHandType, productsInHand);
                    Debug.Log($"[Angajat] Returnat marfa în depozit.");
                }

                productsInHand = 0;
                if (boxVisual != null) boxVisual.SetActive(false);

                currentState = RestockerState.Idle;
            }
        }
    }

    // Returnează TRUE dacă a găsit un raft care are nevoie de marfă
    private bool FindTargetShelf()
    {
        // Găsim toate rafturile
        var allShelves = FindObjectsByType<WorkStation>(FindObjectsSortMode.None)
                      .Where(x => x.stationType == StationType.Shelf).ToList();

        // FIX: Adăugăm condiția && x.slot1Product != ProductType.None
        var needyShelves = allShelves.Where(x => x.NeedsRestocking && x.slot1Product != ProductType.None).ToList();

        if (needyShelves.Count > 0)
        {
            // Alegem un raft random
            WorkStation chosenShelf = needyShelves[Random.Range(0, needyShelves.Count)];

            // Mergem la interactionPoint dacă există
            if (chosenShelf.interactionPoint != null)
                secondaryTarget = chosenShelf.interactionPoint;
            else
                secondaryTarget = chosenShelf.transform;

            return true;
        }
        else
        {
            secondaryTarget = null;
            return false;
        }
    }

    // --- ALTE JOBURI ---
    private void DoJanitorWork() { WanderBehavior(); }

    private void DoCashierWork()
    {
        if (myWorkStation != null)
        {
            agent.SetDestination(myWorkStation.position);
            if (agent.remainingDistance <= agent.stoppingDistance)
                transform.rotation = Quaternion.Slerp(transform.rotation, myWorkStation.rotation, Time.deltaTime * 5f);
        }
    }

    private void WanderBehavior()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            workTimer += Time.deltaTime;
            if (workTimer >= 3.0f)
            {
                Vector3 newPos = RandomNavSphere(transform.position, 8f, -1);
                agent.SetDestination(newPos);
                workTimer = 0;
            }
        }
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        Vector3 randDirection = Random.insideUnitSphere * dist;
        randDirection += origin;
        NavMeshHit navHit;
        NavMesh.SamplePosition(randDirection, out navHit, dist, layermask);
        return navHit.position;
    }
}

public enum EmployeeRole
{
    None,
    Janitor,    // Îngrijitor
    Cashier,    // Casier
    Restocker   // Aranjator marfă
}