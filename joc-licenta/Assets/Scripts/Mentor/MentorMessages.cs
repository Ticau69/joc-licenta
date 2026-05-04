using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject care conține toate mesajele mentorului, grupate pe tipuri de evenimente.
/// Adaugă/editează mesajele direct din Inspector — zero cod necesar.
/// </summary>
[CreateAssetMenu(fileName = "MentorMessages", menuName = "Mentor/Message Library")]
public class MentorMessageSO : ScriptableObject
{
    [System.Serializable]
    public class MentorMessage
    {
        [TextArea(2, 5)]
        public string text;

        [Tooltip("Importanța mesajului — determină în ce faze apare.\n" +
                 "Critical = mereu, High = faza 1-3, Medium = faza 1-2, Low = doar faza 1")]
        public MessageImportance importance = MessageImportance.Medium;
    }

    [Header("Mesaje de bun venit / prima zi")]
    public List<MentorMessage> welcomeMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Bun venit! Sunt Fane, consultantul tău de afaceri. " +
                   "Hai să construim împreună un magazin profitabil! " +
                   "Prima regulă: cumperi ieftin, vinzi scump. Simplu, nu?",
            importance = MessageImportance.Critical
        },
        new MentorMessage
        {
            text = "Înainte de orice, asigură-te că ai rafturi pline și o casă de marcat cu casier. " +
                   "Fără stoc, clienții pleacă. Fără casier, nu poți încasa. Bazele, întotdeauna bazele!",
            importance = MessageImportance.Critical
        }
    };

    [Header("Inflație — șoc pozitiv (creștere rapidă)")]
    public List<MentorMessage> inflationSpikeMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "⚡ Șoc de inflație! Prețurile au crescut brusc.\n\n" +
                   "În economie reală, asta se întâmplă din cauza crizelor energetice sau " +
                   "problemelor în lanțul de aprovizionare. Verifică dacă prețurile tale de vânzare " +
                   "mai acoperă costurile!",
            importance = MessageImportance.High
        },
        new MentorMessage
        {
            text = "Inflație în creștere! 📈\n\n" +
                   "Băncile centrale răspund la inflație crescând dobânzile — de aceea creditele " +
                   "tale devin mai scumpe acum. Dacă plănuiești un împrumut, mai bine acum decât mâine.",
            importance = MessageImportance.High
        }
    };

    [Header("Inflație — deflație (scădere)")]
    public List<MentorMessage> deflationMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Inflația a scăzut! Sună bine, dar deflația persistentă e problematică.\n\n" +
                   "Când prețurile scad constant, consumatorii amână cumpărăturile — " +
                   "de ce să cumperi azi dacă mâine e mai ieftin? Paradoxul economiei!",
            importance = MessageImportance.Medium
        }
    };

    [Header("Competitor mai ieftin")]
    public List<MentorMessage> competitorCheaperMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Un competitor vinde mai ieftin ca tine! 🏪\n\n" +
                   "Ai două opțiuni strategice: scazi și tu prețul (price war — periculos pe termen lung) " +
                   "sau te diferențiezi prin stoc constant și servicii mai bune. " +
                   "Walmart și-a câștigat clienții prin volum, nu neapărat calitate.",
            importance = MessageImportance.High
        },
        new MentorMessage
        {
            text = "Concurența a tăiat prețurile. Clienții tăi vor observa!\n\n" +
                   "Sfat: calculează marja ta minimă (cost achiziție + TVA + overhead). " +
                   "Nu poți vinde sub cost decât dacă vrei să dai faliment.",
            importance = MessageImportance.Medium
        }
    };

    [Header("Tu ești mai ieftin decât concurența")]
    public List<MentorMessage> playerCheapestMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Excelent! Ești cel mai ieftin de pe piață la acest produs. 🥇\n\n" +
                   "Atenție: prețul mic atrage clienți, dar dacă marja e prea mică nu supraviețuiești. " +
                   "Asigură-te că tot acoperi costurile — profit mic e mai bun decât pierdere.",
            importance = MessageImportance.Low
        }
    };

    [Header("Angajat demisionat")]
    public List<MentorMessage> employeeResignedMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Un angajat a plecat din cauza salariului mic. 😞\n\n" +
                   "În management real, fluctuația de personal costă enorm — recrutare, training, " +
                   "productivitate pierdută. Un studiu Gallup arată că înlocuirea unui angajat costă " +
                   "50-200% din salariul anual. Plătește corect de la început!",
            importance = MessageImportance.Critical
        }
    };

    [Header("Angajat nemulțumit (mood scăzut)")]
    public List<MentorMessage> employeeLowMoodMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Angajatul tău e nemulțumit! Productivitatea lui a scăzut.\n\n" +
                   "Teoria motivației a lui Maslow spune că oamenii au nevoie de mai mult " +
                   "decât bani — recunoaștere, siguranță, sens. În joc, crește salariul. " +
                   "În viață reală, adaugă și celelalte dimensiuni.",
            importance = MessageImportance.Medium
        }
    };

    [Header("Credit bancar contractat")]
    public List<MentorMessage> loanTakenMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Ai contractat un credit! 🏦\n\n" +
                   "Levierul financiar (debt leverage) poate accelera creșterea, dar și " +
                   "falimentul. Regula de aur: nu împrumuta mai mult decât poți rambursa din " +
                   "profitul operațional. Rata datorii/venituri sub 30% e considerată sănătoasă.",
            importance = MessageImportance.High
        },
        new MentorMessage
        {
            text = "Credit luat! Dobânda pe care o plătești urmează inflația.\n\n" +
                   "Ăsta e motivul pentru care băncile centrale ajustează dobânzile — " +
                   "vor să controleze inflația fără să sufocă economia cu credite prea scumpe. " +
                   "Un echilibru dificil, numit politică monetară.",
            importance = MessageImportance.Medium
        }
    };

    [Header("Rată credit neplătită")]
    public List<MentorMessage> loanMissedMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Ai ratat o rată! Penalizările se acumulează. ⚠️\n\n" +
                   "În lumea reală, neplata unui credit îți afectează scorul de credit — " +
                   "accesul la finanțare viitoare devine mai scump sau imposibil. " +
                   "Cash flow-ul zilnic e mai important decât profitul pe hârtie.",
            importance = MessageImportance.Critical
        }
    };

    [Header("Inspecție sanitară — amendă")]
    public List<MentorMessage> sanitaryFineMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Amendă de la inspecție sanitară! 🧹\n\n" +
                   "Costul curățeniei e mic față de costul unei amenzi sau al reputației afectate. " +
                   "În retail, curățenia e parte din 'experiența clientului' — " +
                   "un concept care valorează miliarde în industria modernă.",
            importance = MessageImportance.High
        }
    };

    [Header("Inspecție sanitară — trecut")]
    public List<MentorMessage> sanitaryPassMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Inspecție trecută cu succes! ✅\n\n" +
                   "Conformitatea cu reglementările nu e doar obligatorie — e și un avantaj competitiv. " +
                   "Clienții din ce în ce mai mult aleg magazinele curate și bine organizate.",
            importance = MessageImportance.Low
        }
    };

    [Header("Raft gol (client nu a găsit produsul)")]
    public List<MentorMessage> outOfStockMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Raft gol! Un client a plecat fără să cumpere. 📦\n\n" +
                   "Ruptura de stoc costă dublu: pierzi vânzarea și riști să pierzi clientul permanent. " +
                   "Marile lanțuri folosesc sisteme automate de reaprovizionare — " +
                   "tu ai angajatul Restocker. Ține-l activ!",
            importance = MessageImportance.Medium
        }
    };

    [Header("Zi profitabilă")]
    public List<MentorMessage> profitableDayMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Zi profitabilă! Felicitări! 🎉\n\n" +
                   "Amintește-ți: profitul pe hârtie ≠ bani în cont. " +
                   "Un business poate fi profitabil și totuși să dea faliment din lipsă de lichiditate. " +
                   "Urmărește mereu cash flow-ul zilnic!",
            importance = MessageImportance.Low
        },
        new MentorMessage
        {
            text = "Bun rezultat azi! 💰\n\n" +
                   "Reinvestiția profitului e cheia creșterii. Bezos a reinvestit 100% din " +
                   "profitul Amazon timp de 7 ani înainte să devină profitabil pentru acționari. " +
                   "Strategia pe termen lung bate câștigul imediat.",
            importance = MessageImportance.Low
        }
    };

    [Header("Zi neprofitabilă")]
    public List<MentorMessage> unprofitableDayMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Pierdere azi. Nu e motiv de panică, dar e un semnal. 📉\n\n" +
                   "Verifică: cheltuielile fixe (salarii, credite) vs veniturile variabile (vânzări). " +
                   "Dacă cheltuielile fixe depășesc veniturile medii, structura de costuri e greșită.",
            importance = MessageImportance.High
        }
    };

    [Header("Prea puțini bani (risc financiar)")]
    public List<MentorMessage> lowFundsMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Fonduri critice! ⚠️\n\n" +
                   "Regula de aur în business: menține mereu o rezervă de urgență de minimum " +
                   "3 luni de cheltuieli fixe. Fără aceasta, orice surpriză neplăcută poate " +
                   "închide afacerea. Se numește 'runway' — cât timp poți supraviețui fără venituri.",
            importance = MessageImportance.Critical
        }
    };

    [Header("Prima comandă la furnizor")]
    public List<MentorMessage> firstSupplierOrderMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Prima comandă la furnizor! 🚚\n\n" +
                   "Relația cu furnizorii e un activ strategic. " +
                   "Companiile mari negociază termene de plată de 90-120 zile — " +
                   "asta înseamnă că vând produsul înainte să-l plătească. " +
                   "Construiește relații bune și vei obține condiții mai avantajoase.",
            importance = MessageImportance.High
        }
    };

    [Header("Relație furnizor deteriorată")]
    public List<MentorMessage> supplierAngryMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Furnizorul tău e supărat! Comenzile sunt blocate. 😤\n\n" +
                   "Supply chain disruption — un termen familiar după 2020. " +
                   "Dependența de un singur furnizor e un risc major. " +
                   "Diversificarea furnizorilor e o strategie de reziliență.",
            importance = MessageImportance.High
        }
    };

    [Header("Flotă camioane la capacitate maximă")]
    public List<MentorMessage> fleetFullMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Flota e la capacitate maximă! 🚛\n\n" +
                   "Logistica e o barieră de creștere. În retail modern, " +
                   "companiile ca Amazon investesc masiv în propria infrastructură logistică " +
                   "tocmai pentru că a depinde de terți limitează scalabilitatea.",
            importance = MessageImportance.Medium
        }
    };

    [Header("Primul meniu de construire deschis")]
    public List<MentorMessage> buildMenuOpenedMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Atenție la podele! 🏪\n\n" +
                   "Ai două zone distincte: podeaua magazinului — unde circulă clienții " +
                   "și unde plasezi rafturile — și podeaua depozitului, zona ta de stocare. " +
                   "Planifică spațiul cu grijă: un depozit prea mic înseamnă că nu poți comanda " +
                   "în cantități mari, iar un magazin prea înghesuit alungă clienții.",
            importance = MessageImportance.Critical
        }
    };

    [Header("Preț setat sub costul de achiziție")]
    public List<MentorMessage> priceBelowCostMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Vinzi sub cost! Asta înseamnă pierdere la fiecare vânzare. 📉\n\n" +
                   "Marja brută = Preț vânzare - Cost achiziție. " +
                   "Dacă e negativă, cu cât vinzi mai mult, cu atât pierzi mai mult. " +
                   "Asigură-te că prețul acoperă cel puțin costul + TVA + cheltuieli operaționale.",
            importance = MessageImportance.Critical
        },
        new MentorMessage
        {
            text = "Prețul tău e sub costul de achiziție! ⚠️\n\n" +
                   "Strategia 'loss leader' (vânzare în pierdere) există în retail, " +
                   "dar doar pentru produse care atrag clienți ce cumpără și altele. " +
                   "Fără o strategie clară, vinzi în pierdere pur și simplu.",
            importance = MessageImportance.High
        }
    };

    [Header("Stoc epuizat repetat la același produs")]
    public List<MentorMessage> repeatedOutOfStockMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Același produs s-a epuizat de 3 ori! 📦\n\n" +
                   "Asta indică o cerere sistematic mai mare decât stocul tău. " +
                   "În logistică se numește 'par stoc' — cantitatea minimă sub care " +
                   "trebuie să reaprovizionezi automat. Comandă mai mult sau mai des!",
            importance = MessageImportance.High
        },
        new MentorMessage
        {
            text = "Rupturi de stoc repetate la același produs! 🔄\n\n" +
                   "Marile lanțuri folosesc algoritmi de prognoză a cererii — " +
                   "tu ai o metodă mai simplă: dacă se epuizează des, " +
                   "dublează cantitatea comenzii. Stocul în plus costă puțin, " +
                   "clienții pierduți costă mult.",
            importance = MessageImportance.Medium
        }
    };

    [Header("3 sau mai multe credite active simultan")]
    public List<MentorMessage> multipleLoansMessages = new List<MentorMessage>
    {
        new MentorMessage
        {
            text = "Ai 3 credite active simultan! Atenție la supraîndatorare. 🏦\n\n" +
                   "Raportul datorii/venituri (Debt-to-Income) peste 40% e considerat risc ridicat " +
                   "de orice bancă. Verifică dacă ratele lunare totale nu depășesc " +
                   "40% din veniturile tale zilnice medii.",
            importance = MessageImportance.Critical
        }
    };
}

public enum MessageImportance
{
    Low,      // Doar faza 1 (0-10 min)
    Medium,   // Faza 1-2 (0-20 min)
    High,     // Faza 1-3 (0-30 min)
    Critical  // Mereu activ
}