using UnityEngine;

/// <summary>
/// Instanțiază vizualul 3D al produsului la fiecare displaySlot activ,
/// în funcție de nivelul de umplere al raftului (full / half / low / empty).
/// </summary>
[RequireComponent(typeof(WorkStation))]
public class ShelfDisplayController : MonoBehaviour
{
    [Header("Display Slots")]
    [Tooltip("Transform-urile goale de pe raft unde apar produsele 3D.")]
    public Transform[] displaySlots;

    [Header("Fill Thresholds")]
    [Range(0f, 1f)] public float fullThreshold = 0.66f;
    [Range(0f, 1f)] public float halfThreshold = 0.33f;

    private WorkStation _station;
    private GameObject[] _spawnedVisuals;   // câte un prefab instanțiat per slot
    private ProductType _lastProductType = ProductType.None;
    private int _lastActiveCount = -1;

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _station = GetComponent<WorkStation>();

        if (displaySlots != null)
            _spawnedVisuals = new GameObject[displaySlots.Length];
    }

    void Start() => RefreshDisplay();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Apelează după orice modificare de stoc (AddProduct, TakeProduct, Restock).
    /// </summary>
    public void RefreshDisplay()
    {
        if (displaySlots == null || displaySlots.Length == 0) return;

        ProductType currentProduct = _station.slotProduct;
        int targetCount = GetActiveSlotCount();

        if (currentProduct != _lastProductType)
        {
            DestroyAllVisuals();
            _lastProductType = currentProduct;
            _lastActiveCount = -1;
        }

        if (targetCount == _lastActiveCount) return;
        _lastActiveCount = targetCount;

        GameObject prefab = GetDisplayPrefab(currentProduct);

        for (int i = 0; i < displaySlots.Length; i++)
        {
            // Unity "fake null" — obiectul e distrus dar referinta exista inca
            try
            {
                if (displaySlots[i] == null) continue;
                _ = displaySlots[i].position; // test acces
            }
            catch
            {
                // Slot invalid — curatam vizualul asociat si sarim
                if (_spawnedVisuals != null && _spawnedVisuals[i] != null)
                {
                    Destroy(_spawnedVisuals[i]);
                    _spawnedVisuals[i] = null;
                }
                continue;
            }

            bool shouldBeActive = i < targetCount;

            if (shouldBeActive)
            {
                if (_spawnedVisuals[i] == null && prefab != null)
                {
                    _spawnedVisuals[i] = Instantiate(
                        prefab,
                        displaySlots[i].position,
                        displaySlots[i].rotation,
                        displaySlots[i]
                    );
                }
                else if (_spawnedVisuals[i] != null)
                {
                    _spawnedVisuals[i].SetActive(true);
                }
            }
            else
            {
                if (_spawnedVisuals[i] != null)
                    _spawnedVisuals[i].SetActive(false);
            }
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private int GetActiveSlotCount()
    {
        float ratio = _station.FillRatio;

        if (ratio <= 0f) return 0;
        if (ratio <= halfThreshold) return Mathf.Max(1, displaySlots.Length / 2);
        if (ratio <= fullThreshold) return Mathf.CeilToInt(displaySlots.Length * 0.75f);
        return displaySlots.Length;
    }

    private GameObject GetDisplayPrefab(ProductType type)
    {
        if (type == ProductType.None || _station.productDatabase == null) return null;

        ProductData data = _station.productDatabase.GetByType(type);
        return data?.displayPrefab;
    }

    private void DestroyAllVisuals()
    {
        if (_spawnedVisuals == null) return;

        for (int i = 0; i < _spawnedVisuals.Length; i++)
        {
            if (_spawnedVisuals[i] != null)
            {
                Destroy(_spawnedVisuals[i]);
                _spawnedVisuals[i] = null;
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Resize array de vizuale dacă se adaugă sloturi în Inspector
        if (displaySlots != null &&
            (_spawnedVisuals == null || _spawnedVisuals.Length != displaySlots.Length))
        {
            _spawnedVisuals = new GameObject[displaySlots.Length];
        }
    }
#endif
}