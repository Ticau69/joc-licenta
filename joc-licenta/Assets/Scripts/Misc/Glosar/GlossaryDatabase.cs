using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un singur termen din glosar. Serializabil, editabil din Inspector.
/// </summary>
[Serializable]
public class GlossaryTermData
{
    public string id;
    public string name;
    public string category;
    [TextArea(2, 5)] public string definition;
    [TextArea(2, 5)] public string example;
    public string formula;
}

/// <summary>
/// Baza de date a glosarului economic. Creează instanța via
/// Assets → Create → Glossary → Database, apoi populeaz-o din Inspector.
/// Ține datele complet separate de logica UI (GlossaryController).
/// </summary>
[CreateAssetMenu(fileName = "GlossaryDatabase", menuName = "Glossary/Database")]
public class GlossaryDatabase : ScriptableObject
{
    public List<GlossaryTermData> terms = new List<GlossaryTermData>();
}