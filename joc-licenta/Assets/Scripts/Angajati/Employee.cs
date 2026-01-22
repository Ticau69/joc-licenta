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

    private enum RestockerState { MovingToStorage, Loading, MovingToShelf, Unloading, Idle }
    private RestockerState restockerState = RestockerState.Idle;

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
        // Reacționăm doar dacă suntem Restocker și stăm degeaba (Idle)
        if (role == EmployeeRole.Restocker && restockerState == RestockerState.Idle)
        {
            Debug.Log($"[Angajat] {name} a primit notificare de muncă!");

            // Oprim imediat plimbarea curentă
            if (agent != null && agent.isActiveAndEnabled)
                agent.ResetPath();

            workTimer = 0; // Resetăm timerul de plimbare

            // Verificăm imediat dacă e nevoie de noi
            if (FindTargetShelf())
            {
                restockerState = RestockerState.MovingToStorage;
            }
        }
    }

    // --- LOGICĂ RESTOCKER ACTUALIZATĂ ---
    private void DoRestockerWork()
    {
        if (myWorkStation == null) { WanderBehavior(); return; }

        switch (restockerState)
        {
            case RestockerState.Idle:
                // Dacă stăm degeaba, ne plimbăm și căutăm de lucru
                WanderBehavior();

                // La fiecare câteva secunde verificăm dacă a apărut un raft gol
                if (Time.frameCount % 60 == 0) // Optimizare: verifică o dată la 60 frame-uri
                {
                    if (FindTargetShelf()) // Dacă găsim un raft care are nevoie de marfă
                    {
                        restockerState = RestockerState.MovingToStorage; // Începem treaba
                        agent.ResetPath(); // Oprim plimbarea
                    }
                }
                break;

            case RestockerState.MovingToStorage:
                // Mergem spre depozit
                agent.SetDestination(myWorkStation.position);

                // VERIFICARE: Am ajuns la depozit?
                // Mărim toleranța la 2.5f sau folosim agent.stoppingDistance
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                {
                    // Suntem lângă depozit. Verificăm dacă știm ce trebuie să luăm.
                    if (secondaryTarget != null)
                    {
                        WorkStation targetShelf = secondaryTarget.GetComponentInParent<WorkStation>();

                        if (targetShelf != null && targetShelf.slot1Product != ProductType.None)
                        {
                            ProductType neededProduct = targetShelf.slot1Product;
                            int amountCarried = 5;

                            // Luăm referința scriptului Depozit
                            WorkStation storageScript = myWorkStation.GetComponentInParent<WorkStation>();

                            if (storageScript != null)
                            {
                                // Încercăm să luăm marfa
                                bool gotGoods = storageScript.TakeFromStorage(neededProduct, amountCarried);

                                if (gotGoods)
                                {
                                    Debug.Log($"[SUCCES] Angajatul a luat {neededProduct}. Pleacă spre raft.");

                                    // Dacă ai cutie vizuală, o activăm aici
                                    if (boxVisual != null) boxVisual.SetActive(true);

                                    agent.SetDestination(secondaryTarget.position);
                                    restockerState = RestockerState.MovingToShelf;
                                }
                                else
                                {
                                    // Depozitul e gol sau nu are produsul
                                    Debug.LogWarning($"[ESEC] Depozitul nu are stoc pentru {neededProduct}! Verifică inventarul.");

                                    // Important: Îl trimitem la Idle ca să nu rămână blocat aici
                                    restockerState = RestockerState.Idle;
                                }
                            }
                            else
                            {
                                Debug.LogError("Obiectul Depozit nu are componenta WorkStation!");
                            }
                        }
                        else
                        {
                            Debug.Log("Raftul țintă nu mai are produs setat sau a dispărut.");
                            restockerState = RestockerState.Idle;
                        }
                    }
                    else
                    {
                        Debug.Log("Am ajuns la depozit dar am uitat raftul țintă (secondaryTarget null).");
                        restockerState = RestockerState.Idle;
                    }
                }
                break;

            case RestockerState.Loading:
                workTimer += Time.deltaTime;
                if (workTimer > 1.0f)
                {
                    if (boxVisual != null) boxVisual.SetActive(true);
                    restockerState = RestockerState.MovingToShelf;
                }
                break;

            case RestockerState.MovingToShelf:
                if (secondaryTarget == null) { restockerState = RestockerState.MovingToStorage; return; }

                agent.SetDestination(secondaryTarget.position);
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    restockerState = RestockerState.Unloading;
                    workTimer = 0f;
                }
                break;

            case RestockerState.Unloading:
                workTimer += Time.deltaTime;
                if (workTimer > 1.5f)
                {
                    if (boxVisual != null) boxVisual.SetActive(false);

                    // --- ACTUALIZARE INVENTAR ---
                    WorkStation shelf = secondaryTarget.GetComponentInParent<WorkStation>();
                    if (shelf != null)
                    {
                        shelf.AddProduct(5); // Punem produsul fizic în date
                    }
                    // ----------------------------

                    // Verificăm dacă mai e nevoie de marfă la acest raft sau la altele
                    if (shelf.NeedsRestocking)
                    {
                        // Raftul încă nu e plin, ne întoarcem după încă o cutie
                        restockerState = RestockerState.MovingToStorage;
                    }
                    else
                    {
                        // Raftul e plin (20/20). Căutăm altul.
                        if (FindTargetShelf())
                        {
                            restockerState = RestockerState.MovingToStorage;
                        }
                        else
                        {
                            // Nu mai sunt rafturi goale -> Pauză (Wondering)
                            restockerState = RestockerState.Idle;
                            secondaryTarget = null;
                        }
                    }
                }
                break;
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