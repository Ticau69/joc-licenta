using System;
using UnityEngine;

public class PlayerXPManager : MonoBehaviour
{
    public static PlayerXPManager Instance { get; private set; }

    [SerializeField] private LevelConfigSO levelConfig;

    [Header("State")]
    [SerializeField] private int level = 1;
    [SerializeField] private int xpInLevel = 0;

    public int Level => level;
    public int XpInLevel => xpInLevel;
    public int XpToNext => levelConfig != null ? levelConfig.GetXpToNext(level) : 10;

    // (oldLevel, newLevel)
    public event Action<int, int> OnLevelChanged;

    // (oldXp, newXp, xpToNext)
    public event Action<int, int, int> OnXpChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        int oldXp = xpInLevel;
        int oldLevel = level;

        xpInLevel += amount;

        // level up loop (dacă primești mult XP deodată)
        while (xpInLevel >= XpToNext)
        {
            xpInLevel -= XpToNext;
            level++;
        }

        OnXpChanged?.Invoke(oldXp, xpInLevel, XpToNext);

        if (level != oldLevel)
        {
            OnLevelChanged?.Invoke(oldLevel, level);
            // Trimitem semnalul către ScoreManager
            if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            {
                eventBus.Publish(new ScoreGainedEvent { Amount = 15, Source = "Player Level Up" });
            }
        }
    }

    public void SetLevel(int newLevel)
    {
        if (newLevel < 1) newLevel = 1;

        int oldLevel = level;
        level = newLevel;
        xpInLevel = 0; // resetăm XP-ul când setezi nivelul direct

        if (level != oldLevel)
            OnLevelChanged?.Invoke(oldLevel, level);

        OnXpChanged?.Invoke(0, xpInLevel, XpToNext);
    }
}