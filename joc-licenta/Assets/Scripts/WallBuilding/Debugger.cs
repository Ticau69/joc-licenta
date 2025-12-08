using UnityEngine;

/// <summary>
/// Tool de debugging pentru a vizualiza normalele și orientarea fețelor pereților
/// Atașează acest script pe un perete pentru a vedea normalele în Scene View
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class WallFaceDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool showNormals = true;
    [SerializeField] private bool showFaceColors = true;
    [SerializeField] private float normalLength = 0.5f;
    [SerializeField] private Color normalColor = Color.cyan;

    [Header("Face Colors")]
    [SerializeField] private Color frontFaceColor = Color.green;
    [SerializeField] private Color backFaceColor = Color.red;
    [SerializeField] private Color sideFaceColor = Color.yellow;
    [SerializeField] private Color topBottomColor = Color.blue;

    private Mesh mesh;
    private MeshFilter meshFilter;

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            mesh = meshFilter.sharedMesh;
        }
    }

    private void OnDrawGizmos()
    {
        if (mesh == null || !showNormals) return;

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        if (vertices == null || normals == null) return;

        // Desenăm normalele pentru fiecare vertex
        Gizmos.color = normalColor;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldVertex = transform.TransformPoint(vertices[i]);
            Vector3 worldNormal = transform.TransformDirection(normals[i]).normalized;
            Gizmos.DrawRay(worldVertex, worldNormal * normalLength);
            Gizmos.DrawSphere(worldVertex, 0.02f);
        }

        // Desenăm și center-ul fiecărei fețe cu culori diferite
        if (showFaceColors)
        {
            for (int i = 0; i < triangles.Length; i += 6) // La fiecare 2 triunghiuri (o față quad)
            {
                if (i + 5 >= triangles.Length) break;

                // Luăm primul triunghi din quad
                Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);

                // Calculăm centrul și normala
                Vector3 center = (v0 + v1 + v2) / 3f;
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                // Determinăm culoarea bazată pe orientarea normalei
                Color faceColor = DetermineFaceColor(normal);
                Gizmos.color = faceColor;
                Gizmos.DrawSphere(center, 0.08f);

                // Desenăm și normala feței
                Gizmos.DrawRay(center, normal * normalLength * 1.5f);
            }
        }
    }

    private Color DetermineFaceColor(Vector3 normal)
    {
        // Convertim normala în local space pentru analiză
        Vector3 localNormal = transform.InverseTransformDirection(normal);

        // Verificăm care axă e predominantă
        float absX = Mathf.Abs(localNormal.x);
        float absY = Mathf.Abs(localNormal.y);
        float absZ = Mathf.Abs(localNormal.z);

        if (absY > absX && absY > absZ)
        {
            // Față de sus/jos
            return topBottomColor;
        }
        else if (absZ > absX)
        {
            // Față față/spate (normala pe Z)
            return localNormal.z > 0 ? frontFaceColor : backFaceColor;
        }
        else
        {
            // Față laterală (normala pe X)
            return sideFaceColor;
        }
    }

    /// <summary>
    /// Verifică dacă mesh-ul are probleme cu orientarea
    /// </summary>
    [ContextMenu("Check Mesh Orientation")]
    public void CheckMeshOrientation()
    {
        if (mesh == null)
        {
            Debug.LogError("Nu există mesh!");
            return;
        }

        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        if (normals == null || triangles == null)
        {
            Debug.LogError("Mesh incomplet!");
            return;
        }

        Debug.Log("=== WALL MESH ANALYSIS ===");
        Debug.Log($"Total vertices: {mesh.vertexCount}");
        Debug.Log($"Total triangles: {triangles.Length / 3}");
        Debug.Log($"Total faces (quads): {triangles.Length / 6}");

        // Analizăm normalele
        int frontFacing = 0;
        int backFacing = 0;
        int upFacing = 0;
        int downFacing = 0;

        for (int i = 0; i < normals.Length; i++)
        {
            Vector3 worldNormal = transform.TransformDirection(normals[i]);

            if (worldNormal.y > 0.7f) upFacing++;
            else if (worldNormal.y < -0.7f) downFacing++;
            else if (worldNormal.z > 0.5f) frontFacing++;
            else if (worldNormal.z < -0.5f) backFacing++;
        }

        Debug.Log($"Normals facing UP: {upFacing}");
        Debug.Log($"Normals facing DOWN: {downFacing}");
        Debug.Log($"Normals facing FRONT (+Z): {frontFacing}");
        Debug.Log($"Normals facing BACK (-Z): {backFacing}");
        Debug.Log("======================");
    }

    /// <summary>
    /// Inversează toate normalele (pentru debugging rapid)
    /// </summary>
    [ContextMenu("Flip All Normals")]
    public void FlipAllNormals()
    {
        if (mesh == null)
        {
            Debug.LogError("Nu există mesh!");
            return;
        }

        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }
        mesh.normals = normals;

        Debug.Log("Toate normalele au fost inversate!");
    }
}