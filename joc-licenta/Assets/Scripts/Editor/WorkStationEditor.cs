using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorkStation))]
public class WorkStationEditor : Editor
{
    private SerializedProperty _stationType;
    private SerializedProperty _interactionPoint;
    private SerializedProperty _shelfVariant;
    private SerializedProperty _productDatabase;
    private SerializedProperty _slotProduct;
    private SerializedProperty _pendingProduct;
    private SerializedProperty _slotStock;
    private SerializedProperty _maxStock;
    private SerializedProperty _storageRacks;

    private GUIStyle _headerStyle;
    private GUIStyle _boxStyle;

    private static readonly Color ColorShelf = new Color(0.35f, 0.75f, 0.35f, 0.15f);
    private static readonly Color ColorStorage = new Color(0.35f, 0.55f, 0.95f, 0.15f);
    private static readonly Color ColorCash = new Color(0.95f, 0.75f, 0.25f, 0.15f);

    void OnEnable()
    {
        _stationType = serializedObject.FindProperty("stationType");
        _interactionPoint = serializedObject.FindProperty("interactionPoint");
        _shelfVariant = serializedObject.FindProperty("shelfVariant");
        _productDatabase = serializedObject.FindProperty("productDatabase");
        _slotProduct = serializedObject.FindProperty("slotProduct");
        _pendingProduct = serializedObject.FindProperty("pendingProduct");
        _slotStock = serializedObject.FindProperty("slotStock");
        _maxStock = serializedObject.FindProperty("maxStock");
        _storageRacks = serializedObject.FindProperty("storageRacks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        InitStyles();

        WorkStation station = (WorkStation)target;

        // ── Intotdeauna vizibil ───────────────────────────────────────────────
        DrawSectionHeader("Station Configuration", Color.white);
        DrawProp(_stationType);
        DrawProp(_interactionPoint);
        EditorGUILayout.Space(6);

        // ── Conditionat pe tip ────────────────────────────────────────────────
        switch ((StationType)_stationType.enumValueIndex)
        {
            case StationType.Shelf: DrawShelfSection(station); break;
            case StationType.Storage: DrawStorageSection(station); break;
            case StationType.CashRegister: DrawCashSection(); break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ── SHELF ─────────────────────────────────────────────────────────────────

    private void DrawShelfSection(WorkStation station)
    {
        DrawColoredBox(ColorShelf, () =>
        {
            DrawSectionHeader("Shelf Configuration", ColorShelf * 4f);
            DrawProp(_shelfVariant);
            DrawProp(_productDatabase);

            EditorGUILayout.Space(4);
            DrawSectionHeader("Slot", ColorShelf * 4f);
            DrawProp(_slotProduct);
            DrawProp(_pendingProduct);
            DrawProp(_slotStock);
            DrawProp(_maxStock);

            // Progress bar
            if (_slotStock != null && _maxStock != null)
            {
                int stock = _slotStock.intValue;
                int max = Mathf.Max(1, _maxStock.intValue);
                float ratio = (float)stock / max;

                Rect barRect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                barRect = EditorGUI.IndentedRect(barRect);
                EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 0.6f));
                Rect fill = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
                Color barColor = ratio > 0.66f ? new Color(0.3f, 0.85f, 0.3f) :
                                 ratio > 0.33f ? new Color(0.95f, 0.75f, 0.1f) :
                                                 new Color(0.9f, 0.25f, 0.25f);
                EditorGUI.DrawRect(fill, barColor);
                EditorGUI.LabelField(barRect, $"  {stock} / {max}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.Space(4);

            EditorGUILayout.Space(4);
            DrawShelfWarnings(station);
        });
    }

    // ── STORAGE ───────────────────────────────────────────────────────────────

    private void DrawStorageSection(WorkStation station)
    {
        DrawColoredBox(ColorStorage, () =>
        {
            DrawSectionHeader("Storage Configuration", ColorStorage * 4f);
            DrawProp(_storageRacks, includeChildren: true);

            if (Application.isPlaying && station.storageRacks != null && station.storageRacks.Count > 0)
            {
                EditorGUILayout.Space(4);
                DrawSectionHeader("Stoc curent (Runtime)", ColorStorage * 4f);
                foreach (ProductType type in System.Enum.GetValues(typeof(ProductType)))
                {
                    if (type == ProductType.None) continue;
                    int s = station.GetStorageStock(type);
                    if (s > 0)
                        EditorGUILayout.LabelField(type.ToString(), s.ToString(), EditorStyles.miniLabel);
                }
            }

            if (station.storageRacks == null || station.storageRacks.Count == 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Niciun StorageRacks asignat. Adauga manual sau asigura-te ca exista componente StorageRacks in copii.",
                    MessageType.Warning);
            }
        });
    }

    // ── CASH ──────────────────────────────────────────────────────────────────

    private void DrawCashSection()
    {
        DrawColoredBox(ColorCash, () =>
        {
            DrawSectionHeader("Cash Register", ColorCash * 4f);
            EditorGUILayout.HelpBox("Casa de marcat nu are configuratii suplimentare.", MessageType.Info);
        });
    }

    // ── WARNINGS ──────────────────────────────────────────────────────────────

    private void DrawShelfWarnings(WorkStation station)
    {
        if (_slotProduct == null) return;

        if ((ProductType)_slotProduct.enumValueIndex == ProductType.None)
        {
            EditorGUILayout.HelpBox("Niciun produs configurat in slot.", MessageType.Warning);
            return;
        }

        if (_productDatabase != null &&
            _productDatabase.objectReferenceValue != null &&
            !station.IsProductAllowed((ProductType)_slotProduct.enumValueIndex))
        {
            EditorGUILayout.HelpBox(
                $"{(ProductType)_slotProduct.enumValueIndex} nu e compatibil cu {(ShelfType)_shelfVariant.enumValueIndex}.",
                MessageType.Error);
        }

    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    /// <summary>PropertyField cu null-guard — nu crapa daca FindProperty a esuat.</summary>
    private void DrawProp(SerializedProperty prop, bool includeChildren = false)
    {
        if (prop == null) return;
        EditorGUILayout.PropertyField(prop, includeChildren);
    }

    private void DrawSectionHeader(string title, Color color)
    {
        GUI.color = color;
        EditorGUILayout.LabelField(title, _headerStyle);
        GUI.color = Color.white;
    }

    private void DrawColoredBox(Color bgColor, System.Action content)
    {
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = bgColor;
        EditorGUILayout.BeginVertical(_boxStyle);
        GUI.backgroundColor = prev;
        content?.Invoke();
        EditorGUILayout.EndVertical();
    }

    private void InitStyles()
    {
        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel);
            _headerStyle.fontSize = 11;
        }

        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle("helpbox");
            _boxStyle.padding = new RectOffset(10, 10, 8, 8);
        }
    }
}