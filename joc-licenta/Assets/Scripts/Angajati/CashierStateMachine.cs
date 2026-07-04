using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// State machine pentru angajatul Casier.
/// Se deplasează la casa de marcat și se aliniază cu orientarea stației.
/// </summary>
public class CashierStateMachine
{
    private const float ROTATION_SPEED = 5f;
    private const float ROTATION_THRESHOLD_DEGREES = 1f;

    private readonly Employee _owner;

    public CashierStateMachine(Employee owner)
    {
        _owner = owner;
    }

    public void Update(NavMeshAgent agent, Transform workStation)
    {
        if (workStation == null) return;

        agent.SetDestination(workStation.position);

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            AlignWithStation(agent, workStation);
        }
    }

    private void AlignWithStation(NavMeshAgent agent, Transform workStation)
    {
        Transform t = agent.transform;

        // OPTIMIZARE: se rotește doar dacă unghiul e mai mare decât pragul,
        // evităm un Slerp inutil când angajatul e deja aliniat corect.
        if (Quaternion.Angle(t.rotation, workStation.rotation) > ROTATION_THRESHOLD_DEGREES)
        {
            t.rotation = Quaternion.Slerp(
                t.rotation,
                workStation.rotation,
                Time.deltaTime * ROTATION_SPEED
            );
        }
    }
}