using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Afișează obiectivele contextuale — toggle via butonul 🎯 din HUD.
/// Implicit ascuns, pasiv față de experiența de joc.
/// </summary>
public class ObjectiveUIController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIDocument mainHudDocument; // TestUI — pentru butonul toggle

    [Header("Timing")]
    [SerializeField] private float completedLingerTime = 2.5f;
    [SerializeField] private float newObjectiveNotifyTime = 4f; // cât stă notificarea

    private VisualElement _container;
    private VisualElement _list;
    private Label _progressLabel;
    private Button _toggleBtn; // butonul 🎯 din HUD

    private bool _isVisible = false;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        _container = root.Q("ObjectivesContainer");
        _list = root.Q("ObjectivesList");
        _progressLabel = root.Q<Label>("ObjectivesProgress");

        // Ascuns implicit
        if (_container != null)
            _container.style.display = DisplayStyle.None;

        // Butonul X din panelul de obiective
        var closeBtn = root.Q<Button>("ObjectivesCloseBtn");
        if (closeBtn != null)
            closeBtn.clicked += () => { _isVisible = false; _container.style.display = DisplayStyle.None; UpdateToggleBtnStyle(); };

        // Butonul toggle din HUD principal
        if (mainHudDocument != null)
        {
            _toggleBtn = mainHudDocument.rootVisualElement.Q<Button>("ObjectivesToggleBtn");
            if (_toggleBtn != null)
                _toggleBtn.clicked += TogglePanel;
        }
    }

    void Start()
    {
        if (ContextualObjectiveSystem.Instance == null) return;

        ContextualObjectiveSystem.Instance.OnObjectiveUnlocked += OnUnlocked;
        ContextualObjectiveSystem.Instance.OnObjectiveCompleted += OnCompleted;
        ContextualObjectiveSystem.Instance.OnObjectiveProgressChanged += OnProgressChanged;

        RebuildList();
    }

    void OnDestroy()
    {
        if (_toggleBtn != null)
            _toggleBtn.clicked -= TogglePanel;

        if (ContextualObjectiveSystem.Instance == null) return;
        ContextualObjectiveSystem.Instance.OnObjectiveUnlocked -= OnUnlocked;
        ContextualObjectiveSystem.Instance.OnObjectiveCompleted -= OnCompleted;
        ContextualObjectiveSystem.Instance.OnObjectiveProgressChanged -= OnProgressChanged;
    }

    // =========================================================================
    // TOGGLE
    // =========================================================================

    private void TogglePanel()
    {
        _isVisible = !_isVisible;
        if (_container != null)
            _container.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;

        UpdateToggleBtnStyle();
    }

    // =========================================================================
    // HANDLERS
    // =========================================================================

    private void OnUnlocked(Objective obj)
    {
        RebuildList();

        // Pulsăm butonul 🎯 pentru a atrage atenția — fără a forța deschiderea
        StartCoroutine(PulseToggleButton());
    }

    private void OnCompleted(Objective obj)
    {
        MarkRowCompleted(obj.Id);
        UpdateProgressLabel();

        // Deschidem panelul automat să se vadă bifa, îl închidem după delay
        StartCoroutine(ShowCompletionAndClose(obj.Id));
        StartCoroutine(PulseToggleButton());
    }

    private void OnProgressChanged(Objective obj)
    {
        UpdateRowProgress(obj);
    }

    // =========================================================================
    // BUILD UI
    // =========================================================================

    private void RebuildList()
    {
        if (_list == null) return;
        _list.Clear();

        foreach (var obj in ContextualObjectiveSystem.Instance.ActiveObjectives)
            _list.Add(CreateRow(obj));

        UpdateProgressLabel();
    }

    private VisualElement CreateRow(Objective obj)
    {
        var row = new VisualElement();
        row.name = $"obj_{obj.Id}";
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 6;
        row.style.paddingTop = 4;
        row.style.paddingBottom = 4;
        row.style.paddingLeft = 6;
        row.style.paddingRight = 6;
        row.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
        row.style.borderTopLeftRadius = 6;
        row.style.borderTopRightRadius = 6;
        row.style.borderBottomLeftRadius = 6;
        row.style.borderBottomRightRadius = 6;

        // Icon
        var icon = new Label(obj.Icon);
        icon.style.fontSize = 16;
        icon.style.marginRight = 8;
        icon.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(icon);

        // Text container
        var textCol = new VisualElement();
        textCol.style.flexGrow = 1;
        textCol.style.flexDirection = FlexDirection.Column;

        var title = new Label(obj.Title);
        title.name = $"title_{obj.Id}";
        title.style.color = new Color(1f, 1f, 1f, 0.95f);
        title.style.fontSize = 11;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        textCol.Add(title);

        var desc = new Label(obj.Description);
        desc.name = $"desc_{obj.Id}";
        desc.style.color = new Color(1f, 1f, 1f, 0.5f);
        desc.style.fontSize = 9;
        desc.style.whiteSpace = WhiteSpace.Normal;
        textCol.Add(desc);

        if (obj.HasProgress)
        {
            var barBg = new VisualElement();
            barBg.name = $"barbg_{obj.Id}";
            barBg.style.height = 3;
            barBg.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
            barBg.style.marginTop = 3;
            barBg.style.borderTopLeftRadius = 2;
            barBg.style.borderTopRightRadius = 2;
            barBg.style.borderBottomLeftRadius = 2;
            barBg.style.borderBottomRightRadius = 2;

            var barFill = new VisualElement();
            barFill.name = $"barfill_{obj.Id}";
            barFill.style.height = Length.Percent(100);
            barFill.style.width = Length.Percent(obj.ProgressFraction * 100f);
            barFill.style.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            barFill.style.borderTopLeftRadius = 2;
            barFill.style.borderTopRightRadius = 2;
            barFill.style.borderBottomLeftRadius = 2;
            barFill.style.borderBottomRightRadius = 2;
            barBg.Add(barFill);
            textCol.Add(barBg);
        }

        row.Add(textCol);

        var check = new Label("✅");
        check.name = $"check_{obj.Id}";
        check.style.fontSize = 14;
        check.style.display = DisplayStyle.None;
        row.Add(check);

        return row;
    }

    // =========================================================================
    // UPDATE ROW
    // =========================================================================

    private void UpdateRowProgress(Objective obj)
    {
        if (_list == null) return;
        var barFill = _list.Q($"barfill_{obj.Id}");
        if (barFill != null)
            barFill.style.width = Length.Percent(obj.ProgressFraction * 100f);
    }

    private void MarkRowCompleted(string id)
    {
        if (_list == null) return;
        var row = _list.Q($"obj_{id}");
        if (row == null) return;

        row.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 0.25f);

        var title = _list.Q<Label>($"title_{id}");
        if (title != null)
        {
            title.text = $"<s>{title.text}</s>";
            title.style.color = new Color(0.5f, 1f, 0.5f);
        }

        var check = _list.Q($"check_{id}");
        if (check != null)
            check.style.display = DisplayStyle.Flex;
    }

    private IEnumerator RemoveRowAfterDelay(string id, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_list == null) yield break;

        var row = _list.Q($"obj_{id}");
        if (row != null)
            _list.Remove(row);
    }

    // =========================================================================
    // PULSE BUTTON — atrage atenția fără a forța deschiderea
    // =========================================================================

    private IEnumerator ShowCompletionAndClose(string id)
    {
        // Deschidem panelul dacă e închis
        bool wasVisible = _isVisible;
        if (!_isVisible)
        {
            _isVisible = true;
            if (_container != null)
                _container.style.display = DisplayStyle.Flex;
            UpdateToggleBtnStyle();
        }

        // Așteptăm să se vadă bifa
        yield return new WaitForSeconds(completedLingerTime);

        // Scoatem rândul
        if (_list != null)
        {
            var row = _list.Q($"obj_{id}");
            if (row != null) _list.Remove(row);
        }

        // Închidem panelul dacă nu era deschis inițial
        if (!wasVisible)
        {
            _isVisible = false;
            if (_container != null)
                _container.style.display = DisplayStyle.None;
            UpdateToggleBtnStyle();
        }
    }

    private void UpdateToggleBtnStyle()
    {
        if (_toggleBtn == null) return;
        _toggleBtn.style.backgroundColor = _isVisible
            ? new Color(1f, 0.78f, 0.2f, 0.25f)
            : new Color(0f, 0f, 0f, 0f);
    }

    private IEnumerator PulseToggleButton()
    {
        if (_toggleBtn == null) yield break;

        for (int i = 0; i < 3; i++)
        {
            _toggleBtn.style.backgroundColor = new Color(1f, 0.78f, 0.2f, 0.5f);
            yield return new WaitForSeconds(0.3f);
            _toggleBtn.style.backgroundColor = _isVisible
                ? new Color(1f, 0.78f, 0.2f, 0.25f)
                : new Color(0f, 0f, 0f, 0f);
            yield return new WaitForSeconds(0.3f);
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void UpdateProgressLabel()
    {
        if (_progressLabel == null || ContextualObjectiveSystem.Instance == null) return;
        int done = ContextualObjectiveSystem.Instance.GetCompletedCount();
        int total = ContextualObjectiveSystem.Instance.GetTotalCount();
        _progressLabel.text = $"{done}/{total}";
    }
}