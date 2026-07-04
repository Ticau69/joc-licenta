using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controlează tab-ul Glosar Economic din StatsPanel.
/// Populează termenii, gestionează căutarea și afișarea detaliilor.
///
/// Optimizări față de versiunea anterioară:
///  - Datele termenilor vin dintr-un GlossaryDatabase (ScriptableObject),
///    nu mai sunt hardcodate în cod (~200 linii mai puțin, editabile din Inspector).
///  - Stilurile sunt clase USS (Glossary.uss), nu mai sunt setate inline per-element.
///  - Căutarea e debounce-uită (150ms) — nu se reconstruiește UI-ul la fiecare tastă.
///  - Butoanele din listă sunt object-pooled și reutilizate, nu create/distruse
///    la fiecare filtrare (Clear() + New() pe fiecare căutare == alocări + GC spikes).
///  - Termenul selectat e evidențiat prin clasă USS, fără rebuild.
///  - Lookup O(1) prin Dictionary pentru OpenToTerm, în loc de List.Find (O(n)).
///  - Fără LINQ în calea fierbinte (filtrare la fiecare tastă) — bucle simple,
///    zero alocări de enumeratoare/closures per apel.
/// </summary>
public class GlossaryController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private GlossaryDatabase database;

    [Tooltip("Întârziere (ms) înainte de a filtra lista, după ultima tastă apăsată.")]
    [SerializeField] private long searchDebounceMs = 150;

    private const string CategoryHeaderClass = "glossary-category-header";
    private const string TermButtonClass = "glossary-term-button";
    private const string TermButtonSelectedClass = "glossary-term-button--selected";

    // ── UI Elements ───────────────────────────────────────────────────────────
    private ScrollView _termList;
    private TextField _searchField;
    private Label _termName;
    private Label _termCategory;
    private Label _termDefinition;
    private Label _termExample;
    private Label _termFormula;
    private VisualElement _exampleBox;
    private VisualElement _formulaBox;

    // ── State ─────────────────────────────────────────────────────────────────
    private List<GlossaryTermData> _allTerms;
    private Dictionary<string, GlossaryTermData> _termsById;

    // Grupele complete (nefiltrate), calculate o singură dată la BuildTerms().
    private List<(string category, List<GlossaryTermData> terms)> _groupedTerms;

    // Pool de butoane reutilizate între filtrări, ca să evităm Create/Destroy constant.
    private readonly List<Label> _categoryHeaderPool = new();
    private readonly List<Button> _termButtonPool = new();

    private string _searchQuery = "";
    private string _pendingSearchQuery = "";
    private IVisualElementScheduledItem _debounceHandle;

    private GlossaryTermData _selectedTerm;
    private Button _selectedButton;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        _termList = root.Q<ScrollView>("GlossaryTermList");
        _searchField = root.Q<TextField>("GlossarySearch");
        _termName = root.Q<Label>("GlossaryTermName");
        _termCategory = root.Q<Label>("GlossaryCategory");
        _termDefinition = root.Q<Label>("GlossaryDefinition");
        _termExample = root.Q<Label>("GlossaryExample");
        _termFormula = root.Q<Label>("GlossaryFormula");
        _exampleBox = root.Q("GlossaryExampleBox");
        _formulaBox = root.Q("GlossaryFormulaBox");

        if (_searchField != null)
            _searchField.RegisterValueChangedCallback(OnSearchChanged);

        BuildTerms();
        RefreshTermList();
    }

    void OnDisable()
    {
        _debounceHandle?.Pause();
        if (_searchField != null)
            _searchField.UnregisterValueChangedCallback(OnSearchChanged);
    }

    // =========================================================================
    // API PUBLIC
    // =========================================================================

    /// <summary>
    /// Deschide glosarul și sare direct la un termen specific.
    /// Apelat din Fane sau din alte UI-uri.
    /// </summary>
    public void OpenToTerm(string termId)
    {
        if (_termsById == null || !_termsById.TryGetValue(termId, out var term))
            return;

        _searchQuery = "";
        _pendingSearchQuery = "";
        if (_searchField != null) _searchField.SetValueWithoutNotify("");
        RefreshTermList();
        ShowTerm(term);
    }

    // =========================================================================
    // BUILD TERMS
    // =========================================================================

    private void BuildTerms()
    {
        _allTerms = database != null ? database.terms : new List<GlossaryTermData>();

        _termsById = new Dictionary<string, GlossaryTermData>(_allTerms.Count);
        foreach (var t in _allTerms)
            _termsById[t.id] = t;

        // Grupăm și sortăm o singură dată; filtrarea ulterioară nu mai
        // recalculează grupele/sortarea, doar decide ce e vizibil.
        var byCategory = new Dictionary<string, List<GlossaryTermData>>();
        foreach (var term in _allTerms)
        {
            if (!byCategory.TryGetValue(term.category, out var list))
            {
                list = new List<GlossaryTermData>();
                byCategory[term.category] = list;
            }
            list.Add(term);
        }

        foreach (var list in byCategory.Values)
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.CurrentCulture));

        _groupedTerms = new List<(string, List<GlossaryTermData>)>(byCategory.Count);
        foreach (var kv in byCategory)
            _groupedTerms.Add((kv.Key, kv.Value));
        _groupedTerms.Sort((a, b) => string.Compare(a.category, b.category, System.StringComparison.CurrentCulture));
    }

    // =========================================================================
    // SEARCH (debounced)
    // =========================================================================

    private void OnSearchChanged(ChangeEvent<string> evt)
    {
        _pendingSearchQuery = evt.newValue;

        _debounceHandle?.Pause();
        _debounceHandle = _termList?.schedule.Execute(() =>
        {
            _searchQuery = _pendingSearchQuery;
            RefreshTermList();
        }).StartingIn(searchDebounceMs);
    }

    // =========================================================================
    // UI
    // =========================================================================

    private void RefreshTermList()
    {
        if (_termList == null || _groupedTerms == null) return;

        int headerIndex = 0;
        int buttonIndex = 0;
        bool hasQuery = !string.IsNullOrEmpty(_searchQuery);
        GlossaryTermData firstVisible = null;

        // Pentru sincronizarea ordinii elementelor din ScrollView cu ordinea
        // în care le reutilizăm din pool.
        _termList.Clear();

        foreach (var (category, terms) in _groupedTerms)
        {
            int visibleInGroup = 0;

            // Primă trecere: verificăm dacă vreun termen din grup e vizibil,
            // ca să nu adăugăm un header de categorie gol.
            for (int i = 0; i < terms.Count; i++)
            {
                if (!hasQuery || Matches(terms[i], _searchQuery))
                    visibleInGroup++;
            }
            if (visibleInGroup == 0) continue;

            var header = GetOrCreateHeader(headerIndex++);
            header.text = category.ToUpperInvariant();
            _termList.Add(header);

            for (int i = 0; i < terms.Count; i++)
            {
                var term = terms[i];
                if (hasQuery && !Matches(term, _searchQuery)) continue;

                firstVisible ??= term;

                var btn = GetOrCreateButton(buttonIndex++);
                btn.text = term.name;
                btn.userData = term;
                _termList.Add(btn);
            }
        }

        // Ascundem butoanele/headerele din pool care nu mai sunt folosite acum,
        // dar le păstrăm alocate pentru refresh-uri viitoare.
        TrimPool(_categoryHeaderPool, headerIndex);
        TrimPool(_termButtonPool, buttonIndex);

        RestoreSelectionHighlight();

        if (firstVisible != null && _selectedTerm == null)
            ShowTerm(firstVisible);
    }

    private static bool Matches(GlossaryTermData term, string query)
    {
        return Contains(term.name, query) ||
               Contains(term.category, query) ||
               Contains(term.definition, query);
    }

    private static bool Contains(string haystack, string needle)
    {
        return !string.IsNullOrEmpty(haystack) &&
               haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Label GetOrCreateHeader(int index)
    {
        if (index < _categoryHeaderPool.Count)
            return _categoryHeaderPool[index];

        var label = new Label();
        label.AddToClassList(CategoryHeaderClass);
        _categoryHeaderPool.Add(label);
        return label;
    }

    private Button GetOrCreateButton(int index)
    {
        if (index < _termButtonPool.Count)
            return _termButtonPool[index];

        var btn = new Button();
        btn.AddToClassList(TermButtonClass);
        btn.clicked += () =>
        {
            if (btn.userData is GlossaryTermData term)
                ShowTerm(term);
        };
        _termButtonPool.Add(btn);
        return btn;
    }

    private static void TrimPool<T>(List<T> pool, int usedCount)
    {
        // Elementele neutilizate din pool rămân alocate (nu sunt distruse),
        // pur și simplu nu sunt adăugate în ScrollView acest ciclu.
        // Nimic de făcut aici dincolo de a lăsa pool-ul cum e — Clear() de mai
        // sus pe ScrollView deja le-a scos vizual din listă.
    }

    private void ShowTerm(GlossaryTermData term)
    {
        _selectedTerm = term;

        if (_termName != null) _termName.text = term.name;
        if (_termCategory != null) _termCategory.text = term.category.ToUpperInvariant();
        if (_termDefinition != null) _termDefinition.text = term.definition;

        if (_termExample != null && _exampleBox != null)
        {
            bool hasExample = !string.IsNullOrEmpty(term.example);
            _exampleBox.style.display = hasExample ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasExample) _termExample.text = term.example;
        }

        if (_termFormula != null && _formulaBox != null)
        {
            bool hasFormula = !string.IsNullOrEmpty(term.formula);
            _formulaBox.style.display = hasFormula ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasFormula) _termFormula.text = term.formula;
        }

        RestoreSelectionHighlight();
    }

    private void RestoreSelectionHighlight()
    {
        _selectedButton?.RemoveFromClassList(TermButtonSelectedClass);
        _selectedButton = null;

        if (_selectedTerm == null) return;

        foreach (var btn in _termButtonPool)
        {
            if (btn.userData is GlossaryTermData t && t == _selectedTerm && btn.parent != null)
            {
                btn.AddToClassList(TermButtonSelectedClass);
                _selectedButton = btn;
                break;
            }
        }
    }
}