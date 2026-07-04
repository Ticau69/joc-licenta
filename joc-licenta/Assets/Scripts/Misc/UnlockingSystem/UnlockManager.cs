using System.Collections.Generic;
using UnityEngine;

public class UnlockManager : MonoBehaviour
{
    [SerializeField] private LevelUnlocksSO unlocksSO;

    private PlayerXPManager _xp;
    private HashSet<int> _appliedLevels = new(); // simplu: aplicăm doar o dată per level prag

    private void Start()
    {
        _xp = PlayerXPManager.Instance;
        if (_xp == null)
        {
            Debug.LogError("[UnlockManager] PlayerXPManager missing!");
            enabled = false;
            return;
        }

        _xp.OnLevelChanged += HandleLevelChanged;

        // aplică și unlock-urile pentru level-ul curent (dacă reîncarci jocul etc.)
        ApplyUnlocksUpTo(_xp.Level);
    }

    private void OnDestroy()
    {
        if (_xp != null)
            _xp.OnLevelChanged -= HandleLevelChanged;
    }

    private void HandleLevelChanged(int oldLevel, int newLevel)
    {
        ApplyUnlocksUpTo(newLevel);
    }

    private void ApplyUnlocksUpTo(int level)
    {
        if (unlocksSO == null || unlocksSO.unlocks == null) return;

        foreach (var u in unlocksSO.unlocks)
        {
            if (u == null) continue;
            if (level < u.levelRequired) continue;
            if (_appliedLevels.Contains(u.levelRequired)) continue;

            ApplyUnlock(u);
            _appliedLevels.Add(u.levelRequired);
        }
    }

    private void ApplyUnlock(LevelUnlock u)
    {
        Debug.Log($"[UnlockManager] Unlock at level {u.levelRequired}");

        // 2. Limita de Angajați
        if (u.maxEmployees > 0 && EmployeeManager.Instance != null)
        {
            EmployeeManager.Instance.SetMaxEmployees(u.maxEmployees);
            Debug.Log($"[UnlockManager] Max Employees set to {u.maxEmployees}");
        }

        // 3. Limita de Clienți (Vom lega asta de Spawner-ul tău de clienți)
        if (u.maxCustomers > 0)
        {
            // Aici vei apela funcția din managerul tău de clienți
            // ex: CustomerSpawner.Instance.SetMaxCustomers(u.maxCustomers);
            Debug.Log($"[UnlockManager] Max Customers limit increased to {u.maxCustomers}");
        }

        // aici în viitor: unlock raft tier, unlock produse, etc
    }
}