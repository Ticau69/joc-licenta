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

    [Header("Pooling")]
    [Tooltip("Câte mașini din fiecare prefab să pre-instanțiem la Start, ca să evităm Instantiate() în timpul jocului.")]
    [SerializeField] private int prewarmPerPrefab = 2;

    // ── Private ───────────────────────────────────────────────────────────────

    private readonly List<TrafficCar> _laneOneCars = new List<TrafficCar>();
    private readonly List<TrafficCar> _laneTwoCars = new List<TrafficCar>();

    // OPTIMIZARE: un pool separat de obiecte inactive pentru fiecare prefab,
    // ca să nu mai facem Instantiate/Destroy la fiecare mașină.
    private readonly Dictionary<GameObject, Queue<TrafficCar>> _pools = new();

    // Reverse lookup: de la instanța spawnată, aflăm din ce prefab provine,
    // ca s-o punem înapoi în coada corectă la ReturnToPool.
    private readonly Dictionary<TrafficCar, GameObject> _prefabOfInstance = new();

    private float _spawnTimer = 0f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        Prewarm();
    }

    void Update()
    {
        _spawnTimer += Time.deltaTime;

        if (_spawnTimer >= spawnInterval)
        {
            _spawnTimer = 0f;
            TrySpawnCar(_laneOneCars, laneOneStart, laneOneEnd);
            TrySpawnCar(_laneTwoCars, laneTwoStart, laneTwoEnd);
        }

        // Nu mai avem nevoie de curățare periodică cu RemoveAll(c => c == null):
        // mașinile nu mai sunt distruse, sunt returnate explicit în pool prin
        // ReturnToPool(), care le scoate din listă exact atunci.
    }

    // ── Prewarm ───────────────────────────────────────────────────────────────

    private void Prewarm()
    {
        foreach (var prefab in carPrefabs)
        {
            if (prefab == null) continue;

            var queue = new Queue<TrafficCar>();
            _pools[prefab] = queue;

            for (int i = 0; i < prewarmPerPrefab; i++)
            {
                var car = CreateInstance(prefab);
                car.gameObject.SetActive(false);
                queue.Enqueue(car);
            }
        }
    }

    private TrafficCar CreateInstance(GameObject prefab)
    {
        GameObject carObj = Instantiate(prefab);

        TrafficCar trafficCar = carObj.GetComponent<TrafficCar>();
        if (trafficCar == null)
            trafficCar = carObj.AddComponent<TrafficCar>();

        _prefabOfInstance[trafficCar] = prefab;
        return trafficCar;
    }

    // ── Spawn / recycle ──────────────────────────────────────────────────────

    private void TrySpawnCar(List<TrafficCar> lane, Transform start, Transform end)
    {
        if (start == null || end == null) return;
        if (lane.Count >= maxCarsPerLane) return;
        if (carPrefabs.Count == 0) return;

        // Verificăm că nu există altă mașină prea aproape de punctul de spawn
        for (int i = 0; i < lane.Count; i++)
        {
            var car = lane[i];
            if (car == null) continue;
            if (Vector3.Distance(car.transform.position, start.position) < minSpawnDistance)
                return;
        }

        GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Count)];
        TrafficCar trafficCar = GetFromPool(prefab);

        trafficCar.transform.SetPositionAndRotation(start.position, start.rotation);
        trafficCar.gameObject.SetActive(true);

        float speed = Random.Range(minSpeed, maxSpeed);
        // 'this' + 'lane' permit lui TrafficCar să se auto-întoarcă în pool
        // când ajunge la destinație, în loc să se distrugă.
        trafficCar.Initialize(start.position, end.position, speed, this, lane);

        lane.Add(trafficCar);
    }

    private TrafficCar GetFromPool(GameObject prefab)
    {
        if (!_pools.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<TrafficCar>();
            _pools[prefab] = queue;
        }

        while (queue.Count > 0)
        {
            var candidate = queue.Dequeue();
            if (candidate != null) return candidate;
        }

        return CreateInstance(prefab);
    }

    /// <summary>
    /// Apelată de TrafficCar când a ajuns la capătul benzii, în loc de
    /// Destroy(gameObject). Dezactivează mașina și o pune înapoi în pool-ul
    /// prefab-ului corect, gata de refolosire la următorul spawn.
    /// </summary>
    public void ReturnToPool(TrafficCar car, List<TrafficCar> lane)
    {
        if (car == null) return;

        lane.Remove(car);
        car.gameObject.SetActive(false);

        if (_prefabOfInstance.TryGetValue(car, out var prefab) && _pools.TryGetValue(prefab, out var queue))
        {
            queue.Enqueue(car);
        }
        else
        {
            // Fallback defensiv — n-ar trebui să se întâmple în flux normal.
            Destroy(car.gameObject);
        }
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