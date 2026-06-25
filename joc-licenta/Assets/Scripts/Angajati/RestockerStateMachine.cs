// Fișier: RestockerStateMachine.cs
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// State machine for Restocker employee logic.
/// Complet optimizat: fără LINQ, fără alocări Garbage Collection în Update.
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
    private string lastReportedProblem = null;

    public RestockerStateMachine(Employee owner, int maxCapacity)
    {
        this.owner = owner;
        this.maxCarryCapacity = maxCapacity;
    }

    public void ResetDailyMemory()
    {
        lastReportedProblem = null;
        currentState = State.Idle;
        currentTask = TaskType.None;
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

    public void Update(NavMeshAgent agent, Transform workStation, Transform secondaryTarget, GameObject boxVisual, ref float workTimer)
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
            currentState = State.WorkingAtLocation;
            workTimer = 0;
        }
    }

    private void HandleMovingToStorage(NavMeshAgent agent, Transform workStation, ref float workTimer)
    {
        agent.SetDestination(workStation.position);

        float threshold = agent.stoppingDistance + owner.StorageThresholdOffset + 0.8f;

        if (!agent.pathPending && agent.remainingDistance <= threshold)
        {
            currentState = State.WorkingAtLocation;
            workTimer = 0;
        }
    }

    private void HandleWorking(Transform workStation, Transform secondaryTarget, GameObject boxVisual, ref float workTimer)
    {
        workTimer += Time.deltaTime;

        if (workTimer > owner.WorkDuration)
        {
            ExecuteWorkAction(workStation, secondaryTarget, boxVisual);
        }
    }

    // --- METODA OPTIMIZATĂ ---
    private void FindTask()
    {
        // ATENȚIE: Înlocuiește cu funcția ta exactă din WorkStationRegistry (ex: GetShelves() sau GetAllShelves())
        var allShelves = WorkStationRegistry.Instance.GetAllShelves();

        if (allShelves == null || allShelves.Count == 0) return;

        // Prioritate 1: Căutăm primul raft care are nevoie de curățare
        foreach (var shelf in allShelves)
        {
            if (shelf.NeedsClearing)
            {
                AssignTarget(shelf);
                currentTask = TaskType.Clearing;
                currentState = State.MovingToShelf;
                return; // Am găsit de muncă, ieșim instant
            }
        }

        // Prioritate 2: Căutăm primul raft care are nevoie de aprovizionare
        if (ServiceLocator.Instance.TryGet(out IInventoryService inventory))
        {
            foreach (var shelf in allShelves)
            {
                if (shelf.NeedsRestocking)
                {
                    ProductType needed = shelf.slotProduct;
                    StorageRacks rackWithMarfa = inventory.FindRackWithProduct(needed);

                    if (rackWithMarfa != null)
                    {
                        AssignTarget(shelf);
                        owner.myWorkStation = rackWithMarfa.transform;
                        currentTask = TaskType.Restocking;
                        currentState = State.MovingToStorage;
                        ClearProblem();
                        return; // Am găsit de muncă, ieșim instant
                    }
                }
            }
            ReportProblem("Nu avem marfa necesară în depozit!");
        }
    }

    private void AssignTarget(WorkStation station)
    {
        Transform targetTransform = station.interactionPoint != null ? station.interactionPoint : station.transform;
        owner.SetSecondaryTarget(targetTransform);
    }

    private void ExecuteWorkAction(Transform workStation, Transform secondaryTarget, GameObject boxVisual)
    {
        if (secondaryTarget == null)
        {
            ReportProblem("Raftul la care lucram a fost demolat!");
            currentState = State.Idle;
            return;
        }

        WorkStation shelfScript = secondaryTarget.GetComponentInParent<WorkStation>();

        if (currentTask == TaskType.Restocking)
            HandleRestockingAction(shelfScript, workStation, boxVisual);
        else if (currentTask == TaskType.Clearing)
            HandleClearingAction(shelfScript, workStation, boxVisual);
    }

    private void HandleRestockingAction(WorkStation shelf, Transform storageTransform, GameObject boxVisual)
    {
        if (productsInHand == 0)
        {
            if (storageTransform == null)
            {
                ReportProblem("Depozitul la care mă duceam a fost distrus!");
                currentState = State.Idle;
                return;
            }

            StorageRacks rack = storageTransform.GetComponentInParent<StorageRacks>();

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
                currentState = State.Idle;
            }
        }
        else
        {
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
            if (storageTransform == null)
            {
                ReportProblem("Depozitul meu a dispărut!");
                currentState = State.Idle;
                return;
            }

            StorageRacks rack = storageTransform.GetComponentInParent<StorageRacks>();
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
        if (boxVisual != null) boxVisual.SetActive(visible);
    }

    private void ReportProblem(string problem)
    {
        if (lastReportedProblem == problem) return;
        lastReportedProblem = problem;
        owner.ReportProblem(problem);
    }

    private void ClearProblem()
    {
        lastReportedProblem = null;
        owner.ClearProblem();
    }
}