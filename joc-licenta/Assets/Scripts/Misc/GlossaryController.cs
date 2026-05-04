using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controlează tab-ul Glosar Economic din StatsPanel.
/// Populează termenii, gestionează căutarea și afișarea detaliilor.
/// </summary>
public class GlossaryController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

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
    private List<GlossaryTerm> _allTerms;
    private string _searchQuery = "";

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
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue;
                RefreshTermList();
            });

        BuildTerms();
        RefreshTermList();
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
        var term = _allTerms?.Find(t => t.Id == termId);
        if (term == null) return;

        _searchQuery = "";
        if (_searchField != null) _searchField.SetValueWithoutNotify("");
        RefreshTermList();
        ShowTerm(term);
    }

    // =========================================================================
    // BUILD TERMS
    // =========================================================================

    private void BuildTerms()
    {
        _allTerms = new List<GlossaryTerm>
        {
            // ── PREȚURI & MARJĂ ──────────────────────────────────────────────
            new GlossaryTerm(
                id: "marja_bruta",
                name: "Marjă Brută",
                category: "Prețuri & Profit",
                definition: "Diferența dintre prețul de vânzare și costul de achiziție al unui produs. " +
                            "O marjă pozitivă înseamnă că vinzi mai scump decât ai cumpărat — esențial pentru supraviețuire.",
                example: "Cumperi Apă cu 1 RON și o vinzi cu 2 RON → Marjă brută = 1 RON (50%). " +
                         "Verifică marja în tab-ul Inventar la fiecare produs.",
                formula: "Marjă Brută = Preț Vânzare − Cost Achiziție"
            ),

            new GlossaryTerm(
                id: "loss_leader",
                name: "Loss Leader",
                category: "Strategie Prețuri",
                definition: "Vânzarea unui produs sub cost (în pierdere) pentru a atrage clienți " +
                            "care vor cumpăra și alte produse profitabile. Strategie folosită de mari lanțuri retail.",
                example: "Dacă setezi prețul Apei sub cost, Fane te va avertiza. " +
                         "Această strategie are sens doar dacă ai alte produse cu marjă mare care compensează.",
                formula: null
            ),

            new GlossaryTerm(
                id: "price_war",
                name: "Război al Prețurilor",
                category: "Strategie Prețuri",
                definition: "Competiție agresivă în care firmele reduc succesiv prețurile pentru a câștiga clienți. " +
                            "Periculos pe termen lung — poate duce la pierderi pentru toți competitorii.",
                example: "Când un competitor vinde mai ieftin, ai opțiunea să reduci și tu prețul. " +
                         "Verifică tab-ul Inventar → prețul concurentului e afișat în timp real.",
                formula: null
            ),

            new GlossaryTerm(
                id: "par_stoc",
                name: "Par Stoc (Reorder Point)",
                category: "Logistică",
                definition: "Nivelul minim de stoc sub care trebuie să reaprovizionezi. " +
                            "Marile lanțuri folosesc sisteme automate — sub par stoc, comanda se plasează automat.",
                example: "Dacă Apa se epuizează de 3 ori consecutiv, Fane te va sfătui să crești " +
                         "cantitatea comenzii sau frecvența. Accesează tab-ul Livrări pentru a comanda.",
                formula: "Par Stoc = Consum Zilnic Mediu × Timp Livrare (zile)"
            ),

            // ── ECONOMIE MACRO ────────────────────────────────────────────────
            new GlossaryTerm(
                id: "inflatie",
                name: "Inflație",
                category: "Economie Macro",
                definition: "Creșterea generalizată a prețurilor în economie. Reduce puterea de cumpărare " +
                            "a banilor — același RON cumpără mai puțin mâine decât azi.",
                example: "În joc, inflația crește costul de achiziție al produselor. " +
                         "Urmărește evoluția în tab-ul Inflație — graficul arată trending-ul ultimelor 5 zile.",
                formula: "Rata Inflației = ((Preț Curent − Preț Anterior) / Preț Anterior) × 100%"
            ),

            new GlossaryTerm(
                id: "deflatie",
                name: "Deflație",
                category: "Economie Macro",
                definition: "Scăderea generalizată a prețurilor. Sună bine, dar e problematică: " +
                            "consumatorii amână achizițiile dacă știu că mâine e mai ieftin, ducând la recesiune.",
                example: "Când inflația scade sub 0, costurile de achiziție scad și ele. " +
                         "Totuși, veniturile din vânzări pot scădea dacă și prețurile de vânzare trebuie ajustate.",
                formula: null
            ),

            new GlossaryTerm(
                id: "dobanda",
                name: "Dobândă",
                category: "Finanțe",
                definition: "Costul împrumutului de bani. Exprimată ca procent anual din suma împrumutată. " +
                            "Băncile centrale ajustează dobânzile pentru a controla inflația.",
                example: "La tab-ul Credite, fiecare bancă afișează dobânda anuală. " +
                         "Dobânda crește automat când inflația crește — acesta este mecanismul real din economie.",
                formula: "Dobândă = Rată Bază + Inflație Curentă × Sensibilitate"
            ),

            new GlossaryTerm(
                id: "tva",
                name: "TVA (Taxa pe Valoarea Adăugată)",
                category: "Fiscalitate",
                definition: "Taxă indirectă aplicată la fiecare etapă a lanțului de producție-consum. " +
                            "Tu, ca comerciant, colectezi TVA de la clienți și îl plătești statului — ești intermediar.",
                example: "La fiecare achiziție de marfă, TVA-ul e inclus în costul afișat. " +
                         "Verifică tab-ul Cash Flow pentru defalcarea cheltuielilor pe categorii.",
                formula: "Preț cu TVA = Preț fără TVA × (1 + Cota TVA)"
            ),

            // ── FINANȚE ───────────────────────────────────────────────────────
            new GlossaryTerm(
                id: "cash_flow",
                name: "Cash Flow (Flux de Numerar)",
                category: "Finanțe",
                definition: "Mișcarea reală a banilor în și din afacere. O companie poate fi profitabilă " +
                            "pe hârtie și totuși să dea faliment dacă nu are lichidități — cash flow-ul bate profitul.",
                example: "Urmărește balanța zilnică în tab-ul Cash Flow. " +
                         "Dacă ai credite mari de plătit dar vânzările întârzie, cash flow-ul poate fi negativ.",
                formula: "Cash Flow = Încasări − Plăți (într-o perioadă)"
            ),

            new GlossaryTerm(
                id: "debt_to_income",
                name: "Raport Datorii/Venituri",
                category: "Finanțe",
                definition: "Procentul din venituri care merge către plata datoriilor. " +
                            "Peste 40% e considerat risc ridicat de orice bancă sau analist financiar.",
                example: "Dacă ai 3 credite active, Fane te avertizează. " +
                         "Calculează: rate lunare totale ÷ venituri zilnice medii × 100.",
                formula: "DTI = (Rate Lunare Totale / Venituri Lunare) × 100%"
            ),

            new GlossaryTerm(
                id: "levier_financiar",
                name: "Levier Financiar",
                category: "Finanțe",
                definition: "Utilizarea datoriilor pentru a amplifica potențialul de câștig. " +
                            "Un credit bine folosit poate genera profit mai mare decât costul dobânzii. " +
                            "Dar amplifică și pierderile.",
                example: "Iei un credit de 5000 RON pentru a extinde depozitul. " +
                         "Dacă generezi 800 RON/lună extra profit și plătești 300 RON rată, levierul funcționează.",
                formula: "ROI Levier = (Profit Extra − Dobândă) / Capital Propriu Investit"
            ),

            new GlossaryTerm(
                id: "runway",
                name: "Runway (Autonomie Financiară)",
                category: "Finanțe",
                definition: "Cât timp poate supraviețui o afacere fără venituri, doar din rezervele existente. " +
                            "Regula de aur: minimum 3 luni de cheltuieli fixe ca rezervă de urgență.",
                example: "Dacă cheltuielile fixe zilnice sunt 500 RON și ai 15.000 RON în cont, " +
                         "runway-ul tău e 30 zile. Verifică balanța curentă în panoul Money.",
                formula: "Runway = Rezerve Disponibile / Cheltuieli Zilnice Medii"
            ),

            // ── MANAGEMENT ────────────────────────────────────────────────────
            new GlossaryTerm(
                id: "fluctuatie_personal",
                name: "Fluctuație de Personal",
                category: "Management",
                definition: "Rata cu care angajații părăsesc o companie. Cost ridicat: recrutare, training, " +
                            "productivitate pierdută. Un studiu Gallup estimează 50-200% din salariul anual per angajat.",
                example: "Dacă un angajat demisionează din cauza salariului mic, Fane te avertizează. " +
                         "Crește salariul în panoul Angajați pentru a reduce fluctuația.",
                formula: null
            ),

            new GlossaryTerm(
                id: "economie_scara",
                name: "Economie de Scară",
                category: "Management",
                definition: "Reducerea costului mediu per unitate odată cu creșterea volumului de producție/vânzări. " +
                            "Cu cât cumperi mai mult, cu atât costul per bucată scade.",
                example: "Comanzile mari la furnizori pot aduce discount de preț (relație Prietenos). " +
                         "Extinderea flotei îți permite mai multe comenzi simultan.",
                formula: null
            ),

            new GlossaryTerm(
                id: "cost_oportunitate",
                name: "Cost de Oportunitate",
                category: "Management",
                definition: "Valoarea celei mai bune alternative la care renunți când iei o decizie. " +
                            "Banii ținuți în cont 'costă' câștigul pe care l-ai fi obținut investindu-i.",
                example: "Dacă ai 10.000 RON neutilizați și nu extizi magazinul sau stocul, " +
                         "costul de oportunitate e profitul pe care l-ai fi generat cu acea investiție.",
                formula: null
            ),

            // ── LANȚ APROVIZIONARE ────────────────────────────────────────────
            new GlossaryTerm(
                id: "supply_chain",
                name: "Lanț de Aprovizionare",
                category: "Logistică",
                definition: "Rețeaua de furnizori, transportatori și distribuitori care aduc produsul " +
                            "de la producător la consumatorul final. Vulnerabil la disruptions.",
                example: "În joc, ai furnizori diferiți cu prețuri și termene de livrare diferite. " +
                         "Dacă un furnizor refuză comenzile (datorie neachitată), supply chain-ul e blocat.",
                formula: null
            ),

            new GlossaryTerm(
                id: "diversificare_furnizori",
                name: "Diversificarea Furnizorilor",
                category: "Logistică",
                definition: "Strategie de a lucra cu mai mulți furnizori pentru același produs, " +
                            "reducând riscul de dependență față de o singură sursă.",
                example: "Poți comanda Apă de la PepiCo sau de la alți furnizori. " +
                         "Dacă relația cu unul se deteriorează, poți comuta la altul imediat.",
                formula: null
            ),
        };
    }

    // =========================================================================
    // UI
    // =========================================================================

    private void RefreshTermList()
    {
        if (_termList == null) return;
        _termList.Clear();

        var filtered = string.IsNullOrEmpty(_searchQuery)
            ? _allTerms
            : _allTerms.Where(t =>
                t.Name.ToLower().Contains(_searchQuery.ToLower()) ||
                t.Category.ToLower().Contains(_searchQuery.ToLower()) ||
                t.Definition.ToLower().Contains(_searchQuery.ToLower())
              ).ToList();

        // Grupăm pe categorii
        var grouped = filtered.GroupBy(t => t.Category).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            // Header categorie
            var catLabel = new Label(group.Key.ToUpper());
            catLabel.style.color = new Color(1f, 0.78f, 0.2f);
            catLabel.style.fontSize = 9;
            catLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            catLabel.style.marginTop = 8;
            catLabel.style.marginBottom = 4;
            catLabel.style.paddingLeft = 6;
            _termList.Add(catLabel);

            foreach (var term in group.OrderBy(t => t.Name))
            {
                var btn = new Button(() => ShowTerm(term));
                btn.text = term.Name;
                btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
                btn.style.color = new Color(1f, 1f, 1f, 0.85f);
                btn.style.fontSize = 12;
                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                btn.style.borderTopWidth = 0;
                btn.style.borderRightWidth = 0;
                btn.style.borderBottomWidth = 0;
                btn.style.borderLeftWidth = 0;
                btn.style.borderTopLeftRadius = 5;
                btn.style.borderTopRightRadius = 5;
                btn.style.borderBottomLeftRadius = 5;
                btn.style.borderBottomRightRadius = 5;
                btn.style.paddingLeft = 10;
                btn.style.paddingTop = 6;
                btn.style.paddingBottom = 6;
                btn.style.marginBottom = 2;
                _termList.Add(btn);
            }
        }

        // Dacă nu e nimic selectat, selectăm primul
        if (filtered.Count > 0)
            ShowTerm(filtered[0]);
    }

    private void ShowTerm(GlossaryTerm term)
    {
        if (_termName != null) _termName.text = term.Name;
        if (_termCategory != null) _termCategory.text = term.Category.ToUpper();
        if (_termDefinition != null) _termDefinition.text = term.Definition;

        if (_termExample != null && _exampleBox != null)
        {
            bool hasExample = !string.IsNullOrEmpty(term.Example);
            _exampleBox.style.display = hasExample ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasExample) _termExample.text = term.Example;
        }

        if (_termFormula != null && _formulaBox != null)
        {
            bool hasFormula = !string.IsNullOrEmpty(term.Formula);
            _formulaBox.style.display = hasFormula ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasFormula) _termFormula.text = term.Formula;
        }
    }
}

// =============================================================================
// DATA CLASS
// =============================================================================

public class GlossaryTerm
{
    public string Id;
    public string Name;
    public string Category;
    public string Definition;
    public string Example;
    public string Formula;

    public GlossaryTerm(string id, string name, string category,
                        string definition, string example, string formula)
    {
        Id = id;
        Name = name;
        Category = category;
        Definition = definition;
        Example = example;
        Formula = formula;
    }
}