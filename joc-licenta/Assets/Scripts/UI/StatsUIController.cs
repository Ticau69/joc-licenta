using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class StatsUIController : MonoBehaviour
{
    [Header("Referințe Document UI")]
    [SerializeField] private UIDocument _gameUIDoc;

    [Header("Setări Grafic")]
    private const int MAX_HISTORY_DAYS = 5;

    // --- Componente / Manageri ---
    private GameManager _gameManager;
    private ClockManager _clockManager;
    private TimeManager _timeManager;

    // --- Referințe UI ---
    private BarChart _barChart;
    private Label _maxValLabel;
    private Button _statsButton;
    private VisualElement _statsPanel;
    private CircularProgress _playerLevelBar;

    // --- Stare ---
    private bool _isStatsPanelOpen = false;

    // --- Date Financiare ---
    private List<DayData> _history = new List<DayData>();
    private float _lastKnownMoney;
    private float _currentDayIncome = 0;
    private float _currentDayExpense = 0;

    #region Unity Lifecycle

    private void Awake()
    {
        // Preluăm componentele de pe același GameObject
        _gameManager = GetComponent<GameManager>();
        _clockManager = GetComponent<ClockManager>();
        _timeManager = GetComponent<TimeManager>();

        if (_gameUIDoc == null)
            _gameUIDoc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        InitializeUI();
        SubscribeEvents();
        InitializeData();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region Inițializare

    private void InitializeUI()
    {
        if (_gameUIDoc == null) return;

        var root = _gameUIDoc.rootVisualElement;

        _barChart = root.Q<BarChart>("ProfitChart");
        _maxValLabel = root.Q<Label>("MaxMoneyLabel");
        _statsButton = root.Q<Button>("Stats");
        _statsPanel = root.Q<VisualElement>("StatsPanel");
        _playerLevelBar = root.Q<CircularProgress>("PlayerLevel");

        // Ascundem panoul la început
        if (_statsPanel != null)
        {
            _statsPanel.style.display = DisplayStyle.None;
            _isStatsPanelOpen = false;
        }

        // Conectăm butonul de toggle
        if (_statsButton != null)
        {
            _statsButton.clicked += ToggleStatsPanel;
        }

        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Subscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void SubscribeEvents()
    {
        if (_gameManager != null) _gameManager.OnMoneyChanged += UpdateCurrentDayStats;
        if (_timeManager != null) _timeManager.OnDayChanged += OnNewDayStarted;

        if (PlayerXPManager.Instance != null)
        {
            PlayerXPManager.Instance.OnXpChanged += HandleXpChanged;
            PlayerXPManager.Instance.OnLevelChanged += HandleLevelChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (_gameManager != null) _gameManager.OnMoneyChanged -= UpdateCurrentDayStats;
        if (_timeManager != null) _timeManager.OnDayChanged -= OnNewDayStarted;

        if (PlayerXPManager.Instance != null)
        {
            PlayerXPManager.Instance.OnXpChanged -= HandleXpChanged;
            PlayerXPManager.Instance.OnLevelChanged -= HandleLevelChanged;
        }

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Unsubscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void InitializeData()
    {
        // 1. Inițializăm banii
        if (_gameManager != null)
        {
            _lastKnownMoney = _gameManager.CurrentMoney;
        }

        // 2. Simulăm ziua curentă (pentru a nu avea graficul complet gol la Play)
        _history.Clear();
        if (_clockManager != null)
        {
            _history.Add(new DayData(_clockManager.currentDate.ToString("dd MMM"), 0, 0));
        }

        for (int i = 0; i < MAX_HISTORY_DAYS; i++)
        {
            string dateLabel = i == MAX_HISTORY_DAYS - 1
                ? (_clockManager != null
                ? _clockManager.currentDate.ToString("dd MMM")
                : "Azi")
                : "";

            _history.Add(new DayData(dateLabel, 0, 0));
        }

        UpdateChartDisplay();

        // 3. Forțăm o actualizare a barei de XP la deschiderea jocului
        if (PlayerXPManager.Instance != null)
        {
            UpdateXPBar(PlayerXPManager.Instance.Level, PlayerXPManager.Instance.XpInLevel, PlayerXPManager.Instance.XpToNext);
        }
    }

    #endregion

    #region Interacțiune UI

    private void ToggleStatsPanel()
    {
        if (_statsPanel == null) return;

        // Dacă panoul e închis, dar se va deschide acum, strigăm și noi să se închidă restul!
        if (!_isStatsPanelOpen)
        {
            if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            {
                eventBus.Publish(new CloseAllUIEvent());
            }
        }

        _isStatsPanelOpen = !_isStatsPanelOpen;
        _statsPanel.style.display = _isStatsPanelOpen ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnCloseAllUIReceived(CloseAllUIEvent e)
    {
        // Aici FORȚĂM închiderea, nu mai folosim Toggle!
        if (_statsPanel == null) return;

        _isStatsPanelOpen = false;
        _statsPanel.style.display = DisplayStyle.None;
    }

    #endregion

    #region Logica XP

    private void HandleXpChanged(int oldXp, int newXp, int xpToNext)
    {
        int currentLevel = PlayerXPManager.Instance.Level;
        UpdateXPBar(currentLevel, newXp, xpToNext);
    }

    private void HandleLevelChanged(int oldLevel, int newLevel)
    {
        int currentXp = PlayerXPManager.Instance.XpInLevel;
        int targetXp = PlayerXPManager.Instance.XpToNext;
        UpdateXPBar(newLevel, currentXp, targetXp);
    }

    private void UpdateXPBar(int levelToShow, int currentXp, int targetXp)
    {
        if (_playerLevelBar == null) return;

        float progressPercentage = ((float)currentXp / targetXp) * 100f;
        _playerLevelBar.Progress = progressPercentage;
        _playerLevelBar.CenterText = levelToShow.ToString();
    }

    #endregion

    #region Logica Financiară / Grafic

    // Se apelează la fiecare tranzacție (OnMoneyChanged)
    private void UpdateCurrentDayStats()
    {
        float currentMoney = _gameManager.CurrentMoney;
        float diff = currentMoney - _lastKnownMoney;

        if (diff > 0)
        {
            _currentDayIncome += diff;
        }
        else if (diff < 0)
        {
            _currentDayExpense += Mathf.Abs(diff);
        }

        _lastKnownMoney = currentMoney;

        // Suprascriem datele pentru ultima zi din listă
        int lastIndex = _history.Count - 1;
        if (lastIndex >= 0)
        {
            string currentDateLabel = _history[lastIndex].DateLabel;
            _history[lastIndex] = new DayData(currentDateLabel, _currentDayIncome, _currentDayExpense);
        }

        UpdateChartDisplay();
    }

    // Se apelează când se schimbă ziua în joc (OnDayChanged)
    public void OnNewDayStarted()
    {
        _currentDayIncome = 0;
        _currentDayExpense = 0;

        // Calculăm data reală a noii zile
        int daysPassed = _timeManager.CurrentDay - 1;
        System.DateTime newDate = _clockManager.startDate.AddDays(daysPassed);

        // Adăugăm o bară nouă, proaspătă, la începutul zilei
        _history.Add(new DayData(newDate.ToString("dd MMM"), 0, 0));

        // Păstrăm doar ultimile zile configurate
        if (_history.Count > MAX_HISTORY_DAYS)
        {
            _history.RemoveAt(0);
        }

        UpdateChartDisplay();
    }

    private void UpdateChartDisplay()
    {
        if (_barChart == null) return;

        _barChart.SetData(_history);

        // Aflăm cea mai mare valoare pentru a scrie limita sus pe grafic
        float maxVal = 0;
        foreach (var d in _history)
        {
            if (d.Income > maxVal) maxVal = d.Income;
            if (d.Expense > maxVal) maxVal = d.Expense;
        }

        if (_maxValLabel != null)
        {
            _maxValLabel.text = maxVal.ToString("0") + " RON";
        }
    }

    #endregion
}