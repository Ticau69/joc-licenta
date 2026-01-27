using UnityEngine;
using UnityEditor;
using System.Collections.Generic; // Necesar pentru Dictionary

[CustomEditor(typeof(WorkStation))]
public class WorkStationEditor : Editor
{
    SerializedProperty stationType;
    SerializedProperty interactionPoint;
    SerializedProperty shelfVariant;
    SerializedProperty slot1Product;
    SerializedProperty slot1Stock;
    SerializedProperty maxProductsPerSlot;
    SerializedProperty doorController; // ✅ ADĂUGAT

    void OnEnable()
    {
        stationType = serializedObject.FindProperty("stationType");
        interactionPoint = serializedObject.FindProperty("interactionPoint");
        shelfVariant = serializedObject.FindProperty("shelfVariant");
        slot1Product = serializedObject.FindProperty("slot1Product");
        slot1Stock = serializedObject.FindProperty("slot1Stock");
        maxProductsPerSlot = serializedObject.FindProperty("maxProductsPerSlot");
        doorController = serializedObject.FindProperty("doorController"); // ✅ ADĂUGAT
    }

    public override void OnInspectorGUI()
    {
        // Preluăm referința directă către scriptul C# (pentru a accesa Dictionary-ul)
        WorkStation station = (WorkStation)target;

        serializedObject.Update();

        // 1. Tipul Stației
        EditorGUILayout.PropertyField(stationType);
        StationType currentType = (StationType)stationType.enumValueIndex;

        // 2. Navigație
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Navigație", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(interactionPoint);

        // ✅ 3. UȘĂ GLISANTĂ (afișăm pentru Shelf și Storage)
        if (currentType == StationType.Shelf || currentType == StationType.Storage)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Vizuale", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(doorController, new GUIContent("Door Controller"));

            // ✅ Validare vizuală
            if (doorController.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("⚠️ Ușa glisantă nu este setată! Angajații nu vor putea deschide ușa.", MessageType.Warning);
            }
        }

        // 4. LOGICA CONDIȚIONALĂ
        if (currentType == StationType.Shelf)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Configurare Raft", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(shelfVariant);
            EditorGUILayout.PropertyField(maxProductsPerSlot);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Stare Curentă", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(slot1Product);
            EditorGUILayout.PropertyField(slot1Stock);
        }
        else if (currentType == StationType.Storage)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Inventar Depozit (Live View)", EditorStyles.boldLabel);

            // --- VIZUALIZARE DICȚIONAR ---
            if (station.storageInventory != null && station.storageInventory.Count > 0)
            {
                // Desenăm o cutie în jurul listei
                EditorGUILayout.BeginVertical("box");

                // Cap de tabel
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Produs", EditorStyles.miniBoldLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField("Cantitate", EditorStyles.miniBoldLabel);
                EditorGUILayout.EndHorizontal();

                // Iterăm prin dicționar
                foreach (var item in station.storageInventory)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Nume Produs
                    EditorGUILayout.LabelField(item.Key.ToString(), GUILayout.Width(120));

                    // Cantitate (Read-only label)
                    EditorGUILayout.LabelField(item.Value.ToString());

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();

                // Buton de Debug pentru a curăța stocul (opțional)
                if (GUILayout.Button("Golește Depozitul (Debug)"))
                {
                    station.storageInventory.Clear();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Depozitul este gol.", MessageType.Info);
            }
            // -----------------------------
        }
        else if (currentType == StationType.CashRegister)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Casă de marcat activă.", MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();

        // IMPORTANT: Forțăm redesenarea inspectorului dacă jocul rulează, 
        // ca să vedem actualizările în timp real (fără să dăm click pe obiect mereu)
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}