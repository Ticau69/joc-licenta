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
    private RestockerState restockerState = RestockerState.MovingToStorage;

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
                agent.SetDestination(myWorkStation.position);
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    // Înainte să încărcăm, verificăm dacă mai avem nevoie de raft
                    // (Poate l-a umplut alt angajat între timp)
                    if (secondaryTarget != null)
                    {
                        var shelfInfo = secondaryTarget.GetComponentInParent<WorkStation>();
                        if (!shelfInfo.NeedsRestocking)
                        {

                            // Raftul s-a umplut între timp! Căutăm altul sau intrăm în Idle
                            if (!FindTargetShelf())
                            {
                                restockerState = RestockerState.Idle;
                                return;
                            }
                        }
                    }
                    else if (!FindTargetShelf()) // Dacă nu aveam țintă
                    {
                        restockerState = RestockerState.Idle;
                        return;
                    }

                    restockerState = RestockerState.Loading;
                    workTimer = 0f;
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
                        shelf.AddProduct(); // Punem produsul fizic în date
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