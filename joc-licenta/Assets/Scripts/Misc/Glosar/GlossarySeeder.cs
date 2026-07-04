#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Populează automat un GlossaryDatabase cu termenii originali din scriptul vechi.
/// Folosire: Tools → Glossary → Seed Database, selectează/creează asset-ul.
/// După rulare poți șterge acest fișier — a servit doar la migrare.
/// </summary>
public static class GlossarySeeder
{
    [MenuItem("Tools/Glossary/Seed Database (creates asset in Assets/Data)")]
    public static void Seed()
    {
        const string folder = "Assets/Data";
        const string path = folder + "/GlossaryDatabase.asset";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Data");

        var db = AssetDatabase.LoadAssetAtPath<GlossaryDatabase>(path);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<GlossaryDatabase>();
            AssetDatabase.CreateAsset(db, path);
        }

        db.terms = new List<GlossaryTermData>
        {
            T("marja_bruta", "Marjă Brută", "Prețuri & Profit",
                "Diferența dintre prețul de vânzare și costul de achiziție al unui produs. " +
                "O marjă pozitivă înseamnă că vinzi mai scump decât ai cumpărat — esențial pentru supraviețuire.",
                "Cumperi Apă cu 1 RON și o vinzi cu 2 RON → Marjă brută = 1 RON (50%). " +
                "Verifică marja în tab-ul Inventar la fiecare produs.",
                "Marjă Brută = Preț Vânzare − Cost Achiziție"),

            T("loss_leader", "Loss Leader", "Strategie Prețuri",
                "Vânzarea unui produs sub cost (în pierdere) pentru a atrage clienți " +
                "care vor cumpăra și alte produse profitabile. Strategie folosită de mari lanțuri retail.",
                "Dacă setezi prețul Apei sub cost, Fane te va avertiza. " +
                "Această strategie are sens doar dacă ai alte produse cu marjă mare care compensează.",
                null),

            T("price_war", "Război al Prețurilor", "Strategie Prețuri",
                "Competiție agresivă în care firmele reduc succesiv prețurile pentru a câștiga clienți. " +
                "Periculos pe termen lung — poate duce la pierderi pentru toți competitorii.",
                "Când un competitor vinde mai ieftin, ai opțiunea să reduci și tu prețul. " +
                "Verifică tab-ul Inventar → prețul concurentului e afișat în timp real.",
                null),

            T("par_stoc", "Par Stoc (Reorder Point)", "Logistică",
                "Nivelul minim de stoc sub care trebuie să reaprovizionezi. " +
                "Marile lanțuri folosesc sisteme automate — sub par stoc, comanda se plasează automat.",
                "Dacă Apa se epuizează de 3 ori consecutiv, Fane te va sfătui să crești " +
                "cantitatea comenzii sau frecvența. Accesează tab-ul Livrări pentru a comanda.",
                "Par Stoc = Consum Zilnic Mediu × Timp Livrare (zile)"),

            T("inflatie", "Inflație", "Economie Macro",
                "Creșterea generalizată a prețurilor în economie. Reduce puterea de cumpărare " +
                "a banilor — același RON cumpără mai puțin mâine decât azi.",
                "În joc, inflația crește costul de achiziție al produselor. " +
                "Urmărește evoluția în tab-ul Inflație — graficul arată trending-ul ultimelor 5 zile.",
                "Rata Inflației = ((Preț Curent − Preț Anterior) / Preț Anterior) × 100%"),

            T("deflatie", "Deflație", "Economie Macro",
                "Scăderea generalizată a prețurilor. Sună bine, dar e problematică: " +
                "consumatorii amână achizițiile dacă știu că mâine e mai ieftin, ducând la recesiune.",
                "Când inflația scade sub 0, costurile de achiziție scad și ele. " +
                "Totuși, veniturile din vânzări pot scădea dacă și prețurile de vânzare trebuie ajustate.",
                null),

            T("dobanda", "Dobândă", "Finanțe",
                "Costul împrumutului de bani. Exprimată ca procent anual din suma împrumutată. " +
                "Băncile centrale ajustează dobânzile pentru a controla inflația.",
                "La tab-ul Credite, fiecare bancă afișează dobânda anuală. " +
                "Dobânda crește automat când inflația crește — acesta este mecanismul real din economie.",
                "Dobândă = Rată Bază + Inflație Curentă × Sensibilitate"),

            T("tva", "TVA (Taxa pe Valoarea Adăugată)", "Fiscalitate",
                "Taxă indirectă aplicată la fiecare etapă a lanțului de producție-consum. " +
                "Tu, ca comerciant, colectezi TVA de la clienți și îl plătești statului — ești intermediar.",
                "La fiecare achiziție de marfă, TVA-ul e inclus în costul afișat. " +
                "Verifică tab-ul Cash Flow pentru defalcarea cheltuielilor pe categorii.",
                "Preț cu TVA = Preț fără TVA × (1 + Cota TVA)"),

            T("cash_flow", "Cash Flow (Flux de Numerar)", "Finanțe",
                "Mișcarea reală a banilor în și din afacere. O companie poate fi profitabilă " +
                "pe hârtie și totuși să dea faliment dacă nu are lichidități — cash flow-ul bate profitul.",
                "Urmărește balanța zilnică în tab-ul Cash Flow. " +
                "Dacă ai credite mari de plătit dar vânzările întârzie, cash flow-ul poate fi negativ.",
                "Cash Flow = Încasări − Plăți (într-o perioadă)"),

            T("debt_to_income", "Raport Datorii/Venituri", "Finanțe",
                "Procentul din venituri care merge către plata datoriilor. " +
                "Peste 40% e considerat risc ridicat de orice bancă sau analist financiar.",
                "Dacă ai 3 credite active, Fane te avertizează. " +
                "Calculează: rate lunare totale ÷ venituri zilnice medii × 100.",
                "DTI = (Rate Lunare Totale / Venituri Lunare) × 100%"),

            T("levier_financiar", "Levier Financiar", "Finanțe",
                "Utilizarea datoriilor pentru a amplifica potențialul de câștig. " +
                "Un credit bine folosit poate genera profit mai mare decât costul dobânzii. " +
                "Dar amplifică și pierderile.",
                "Iei un credit de 5000 RON pentru a extinde depozitul. " +
                "Dacă generezi 800 RON/lună extra profit și plătești 300 RON rată, levierul funcționează.",
                "ROI Levier = (Profit Extra − Dobândă) / Capital Propriu Investit"),

            T("runway", "Runway (Autonomie Financiară)", "Finanțe",
                "Cât timp poate supraviețui o afacere fără venituri, doar din rezervele existente. " +
                "Regula de aur: minimum 3 luni de cheltuieli fixe ca rezervă de urgență.",
                "Dacă cheltuielile fixe zilnice sunt 500 RON și ai 15.000 RON în cont, " +
                "runway-ul tău e 30 zile. Verifică balanța curentă în panoul Money.",
                "Runway = Rezerve Disponibile / Cheltuieli Zilnice Medii"),

            T("fluctuatie_personal", "Fluctuație de Personal", "Management",
                "Rata cu care angajații părăsesc o companie. Cost ridicat: recrutare, training, " +
                "productivitate pierdută. Un studiu Gallup estimează 50-200% din salariul anual per angajat.",
                "Dacă un angajat demisionează din cauza salariului mic, Fane te avertizează. " +
                "Crește salariul în panoul Angajați pentru a reduce fluctuația.",
                null),

            T("economie_scara", "Economie de Scară", "Management",
                "Reducerea costului mediu per unitate odată cu creșterea volumului de producție/vânzări. " +
                "Cu cât cumperi mai mult, cu atât costul per bucată scade.",
                "Comanzile mari la furnizori pot aduce discount de preț (relație Prietenos). " +
                "Extinderea flotei îți permite mai multe comenzi simultan.",
                null),

            T("cost_oportunitate", "Cost de Oportunitate", "Management",
                "Valoarea celei mai bune alternative la care renunți când iei o decizie. " +
                "Banii ținuți în cont 'costă' câștigul pe care l-ai fi obținut investindu-i.",
                "Dacă ai 10.000 RON neutilizați și nu extizi magazinul sau stocul, " +
                "costul de oportunitate e profitul pe care l-ai fi generat cu acea investiție.",
                null),

            T("supply_chain", "Lanț de Aprovizionare", "Logistică",
                "Rețeaua de furnizori, transportatori și distribuitori care aduc produsul " +
                "de la producător la consumatorul final. Vulnerabil la disruptions.",
                "În joc, ai furnizori diferiți cu prețuri și termene de livrare diferite. " +
                "Dacă un furnizor refuză comenzile (datorie neachitată), supply chain-ul e blocat.",
                null),

            T("diversificare_furnizori", "Diversificarea Furnizorilor", "Logistică",
                "Strategie de a lucra cu mai mulți furnizori pentru același produs, " +
                "reducând riscul de dependență față de o singură sursă.",
                "Poți comanda Apă de la PepiCo sau de la alți furnizori. " +
                "Dacă relația cu unul se deteriorează, poți comuta la altul imediat.",
                null),
        };

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Selection.activeObject = db;
        Debug.Log($"GlossaryDatabase populat cu {db.terms.Count} termeni la: {path}");
    }

    private static GlossaryTermData T(string id, string name, string category,
        string definition, string example, string formula)
    {
        return new GlossaryTermData
        {
            id = id,
            name = name,
            category = category,
            definition = definition,
            example = example,
            formula = formula
        };
    }
}
#endif