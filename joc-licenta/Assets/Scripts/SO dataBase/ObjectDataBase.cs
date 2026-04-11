using UnityEngine;
using System.Collections.Generic;
using System;

// --- NOU: Tipurile mari de obiecte ---
public enum ObjectCategory
{
    Equipment, // Rafturi, Frigidere, Case de marcat
    Structure, // Pereți, Podele, Uși
    Decoration // Plante, Tablouri (pe viitor)
}

[CreateAssetMenu(fileName = "ObjectDataBase", menuName = "ObjectDataBase", order = 0)]
public class ObjectDataBase : ScriptableObject
{
    public List<ObjectData> objectsData;
}

[Serializable]
public class ObjectData
{
    [field: Header("Info Vizuale (UI)")]
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField, TextArea(2, 4)] public string ObjDescription { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }

    [field: Header("Date Tehnice")]
    [field: SerializeField] public int ID { get; private set; }
    [field: SerializeField] public ObjectCategory Category { get; private set; } = ObjectCategory.Equipment; // <--- NOU
    [field: SerializeField] public GameObject Prefab { get; private set; }
    [field: SerializeField] public Vector2Int Size { get; private set; } = Vector2Int.one;
    [field: SerializeField] public bool IsDoor { get; private set; }

    [field: Header("Economie & Progresie")]
    [field: SerializeField] public int Cost { get; private set; }
    [field: SerializeField] public int PowerConsumption { get; private set; } = 0;
    [field: SerializeField] public int UnlockLevel { get; private set; } = 1;

    [field: Header("Funcționalitate (Doar pt. Equipment)")]
    [field: SerializeField] public StationType StationType { get; private set; } = StationType.Shelf;
    [field: SerializeField] public ShelfType ShelfVariant { get; private set; } = ShelfType.StandardShelf;
}