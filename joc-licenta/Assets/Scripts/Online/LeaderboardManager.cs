using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;

public struct LeaderboardEntry
{
    public string Name;
    public int Score;
}

/// <summary>
/// Clasă helper care controlează elementele vizuale ale unui singur rând din șablon.
/// </summary>
public class LeaderboardRowController
{
    private readonly Label _rankLabel;
    private readonly Label _nameLabel;
    private readonly Label _scoreLabel;

    public LeaderboardRowController(VisualElement rowElement)
    {
        _rankLabel = rowElement.Q<Label>("RankLabel");
        _nameLabel = rowElement.Q<Label>("NameLabel");
        _scoreLabel = rowElement.Q<Label>("ScoreLabel");
    }

    public void SetData(int rank, string name, int score)
    {
        if (_rankLabel != null) _rankLabel.text = $"#{rank}";
        if (_nameLabel != null) _nameLabel.text = name;
        if (_scoreLabel != null) _scoreLabel.text = $"{score} pct";
    }
}

/// <summary>
/// Serviciu global (poate fi atașat pe un obiect sau folosit ca utilitar) pentru gestionarea interogărilor Firebase.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public async Task<List<LeaderboardEntry>> GetGlobalTopScoresAsync()
    {
        var db = FirebaseFirestore.DefaultInstance;

        // Luăm primii 10 ordonați după Score (descrescător)
        Query query = db.Collection("Leaderboard")
                        .OrderByDescending("Score")
                        .Limit(10);

        var snapshot = await query.GetSnapshotAsync();
        List<LeaderboardEntry> topPlayers = new();

        foreach (var doc in snapshot.Documents)
        {
            topPlayers.Add(new LeaderboardEntry
            {
                Name = doc.GetValue<string>("Name"),
                Score = doc.GetValue<int>("Score")
            });
        }
        return topPlayers;
    }

    /// <summary>
    /// Trimite scorul jucătorului curent în Firebase.
    /// Va suprascrie scorul vechi pentru a păstra doar cel mai bun rezultat.
    /// </summary>
    public void UploadPlayerScore(string playerName, int currentScore)
    {
        // Verificăm dacă jucătorul este logat
        var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null) return;

        var db = FirebaseFirestore.DefaultInstance;

        // Folosim ID-ul unic al jucătorului ca nume de document pentru a nu crea duplicate
        db.Collection("Leaderboard").Document(user.UserId).SetAsync(new Dictionary<string, object>
        {
            { "Name", playerName },
            { "Score", currentScore }
        });

        Debug.Log($"[Leaderboard] Scorul de {currentScore} pct a fost salvat în Cloud pentru {playerName}!");
    }
}