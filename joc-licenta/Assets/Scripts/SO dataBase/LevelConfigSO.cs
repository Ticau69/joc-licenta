using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfigSO", menuName = "Scriptable Objects/LevelConfigSO")]
public class LevelConfigSO : ScriptableObject
{
    [Header("Leveling curve")]
    [Tooltip("XP necesar pentru a trece de la level N la N+1 (index 0 = pentru level 1->2)")]
    public int[] xpToNextLevel = { 10, 20, 35, 55, 80, 110, 145 };

    public int GetXpToNext(int currentLevel)
    {
        // level 1 folosește index 0
        int idx = Mathf.Max(0, currentLevel - 1);

        if (xpToNextLevel == null || xpToNextLevel.Length == 0)
            return 10;

        if (idx >= xpToNextLevel.Length)
        {
            // după ultimul entry, creștem gradual
            int last = xpToNextLevel[xpToNextLevel.Length - 1];
            int extra = (idx - (xpToNextLevel.Length - 1)) * 25;
            return last + extra;
        }

        return xpToNextLevel[idx];
    }
}