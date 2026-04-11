using UnityEngine;
using System.Collections.Generic;

public class TrafficLane : MonoBehaviour
{
    [Header("Waypoints")]
    [Tooltip("Punctul de spawn și direcție pentru banda 1 (ex: stânga → dreapta).")]
    public Transform laneOneStart;
    public Transform laneOneEnd;

    [Tooltip("Punctul de spawn și direcție pentru banda 2 (sens opus).")]
    public Transform laneTwoStart;
    public Transform laneTwoEnd;

    [Header("Cars")]
    [Tooltip("Prefaburile de mașini disponibile.")]
    public List<GameObject> carPrefabs = new List<GameObject>();

    [Tooltip("Câte mașini pot fi simultan pe fiecare bandă.")]
    public int maxCarsPerLane = 4;

    [Tooltip("Interval între spawn-uri (secunde reale).")]
    public float spawnInterval = 3f;

    [Tooltip("Distanță minimă între mașini (pentru a nu se suprapune la spawn).")]
    public float minSpawnDistance = 8f;

    [Header("Speed")]
    public float minSpeed = 6f;
    public float maxSpeed = 12f;

    // ── Private ───────────────────────────────────────────────────────────────

    private List<TrafficCar> _laneOneCars = new List<TrafficCar>();
    private List<TrafficCar> _laneTwoCars = new List<TrafficCar>();
    private float _spawnTimer = 0f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Update()
    {
        _spawnTimer += Time.deltaTime;

        if (_spawnTimer >= spawnInterval)
        {
            _spawnTimer = 0f;
            TrySpawnCar(_laneOneCars, laneOneStart, laneOneEnd);
            TrySpawnCar(_laneTwoCars, laneTwoStart, laneTwoEnd);
        }

        // Curățăm mașinile care au ajuns la destinație și s-au distrus
        _laneOneCars.RemoveAll(c => c == null);
        _laneTwoCars.RemoveAll(c => c == null);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void TrySpawnCar(List<TrafficCar> lane, Transform start, Transform end)
    {
        if (start == null || end == null) return;
        if (lane.Count >= maxCarsPerLane) return;
        if (carPrefabs.Count == 0) return;

        // Verificăm că nu există altă mașină prea aproape de punctul de spawn
        foreach (TrafficCar car in lane)
        {
            if (car == null) continue;
            if (Vector3.Distance(car.transform.position, start.position) < minSpawnDistance)
                return;
        }

        // Instanțiem mașina
        GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Count)];
        GameObject carObj = Instantiate(prefab, start.position, start.rotation);

        TrafficCar trafficCar = carObj.GetComponent<TrafficCar>();
        if (trafficCar == null)
            trafficCar = carObj.AddComponent<TrafficCar>();

        float speed = Random.Range(minSpeed, maxSpeed);
        trafficCar.Initialize(start.position, end.position, speed);

        lane.Add(trafficCar);
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        DrawLaneGizmo(laneOneStart, laneOneEnd, Color.green);
        DrawLaneGizmo(laneTwoStart, laneTwoEnd, Color.red);
    }

    private void DrawLaneGizmo(Transform start, Transform end, Color color)
    {
        if (start == null || end == null) return;
        Gizmos.color = color;
        Gizmos.DrawSphere(start.position, 0.3f);
        Gizmos.DrawSphere(end.position, 0.3f);
        Gizmos.DrawLine(start.position, end.position);

        // Săgeată direcție
        Vector3 dir = (end.position - start.position).normalized;
        Vector3 mid = (start.position + end.position) / 2f;
        Vector3 right = Vector3.Cross(dir, Vector3.up) * 0.5f;
        Gizmos.DrawLine(mid, mid - dir * 1f + right);
        Gizmos.DrawLine(mid, mid - dir * 1f - right);
    }
#endif
}
