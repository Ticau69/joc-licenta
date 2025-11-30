using UnityEngine;

public interface IBuldingState
{
    void EndState();
    void OnAction(Vector3Int gridPosition);
    void UpdateState(Vector3Int gridPosition);
}
