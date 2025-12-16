using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWall : MonoBehaviour
{
    private Mesh mesh;
    private List<Vector3> vertices;
    private List<int> triangles;
    private List<Vector2> uvs;

    // Setări Perete
    [SerializeField] private float height = 2.5f;
    [SerializeField] private float width = 0.2f;
    [SerializeField] private Material wallMaterial;

    // Referințe pentru coordonate
    private Vector3 worldStartPos;
    private Vector3 worldEndPos;

    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        mesh = new Mesh();
        mesh.name = "Procedural Wall Mesh";
        GetComponent<MeshFilter>().mesh = mesh;

        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (wallMaterial != null && meshRenderer != null)
        {
            meshRenderer.material = wallMaterial;
        }
    }

    public void GenerateWall(Vector3 startPos, Vector3 endPos)
    {
        float overlap = 0.1f;
        Vector3 direction = (endPos - startPos).normalized;

        startPos -= direction * overlap;
        endPos += direction * overlap;

        worldStartPos = startPos;
        worldEndPos = endPos;

        float distance = Vector3.Distance(startPos, endPos);
        if (distance < 0.01f)
        {
            Debug.LogWarning("ProceduralWall: Distanța prea mică!");
            return;
        }

        vertices = new List<Vector3>();
        triangles = new List<int>();
        uvs = new List<Vector2>();

        // Setăm pivotul la mijloc
        Vector3 midPoint = (startPos + endPos) / 2f;
        transform.position = midPoint;

        // Convertim în local space
        Vector3 startLocal = transform.InverseTransformPoint(startPos);
        Vector3 endLocal = transform.InverseTransformPoint(endPos);

        // Calculăm direcția și lungimea
        Vector3 wallDirection = (endLocal - startLocal).normalized;
        float length = Vector3.Distance(startLocal, endLocal);

        // Vector perpendicular pentru grosime (Cross Product: Up x Direction)
        // Rezultă un vector la DREAPTA când privești de la Start către End
        Vector3 rightVector = Vector3.Cross(Vector3.up, wallDirection).normalized;
        Vector3 thicknessOffset = rightVector * (width / 2f);

        // Definim cele 8 colțuri ale cuboidului
        // Convenție: Privind de la Start către End
        // - Partea STÂNGĂ (negativ pe rightVector) = EXTERIOR
        // - Partea DREAPTĂ (pozitiv pe rightVector) = INTERIOR

        Vector3 bottomStartOuter = startLocal - thicknessOffset;  // v0
        Vector3 bottomEndOuter = endLocal - thicknessOffset;      // v1
        Vector3 bottomStartInner = startLocal + thicknessOffset;  // v2
        Vector3 bottomEndInner = endLocal + thicknessOffset;      // v3

        Vector3 topStartOuter = bottomStartOuter + Vector3.up * height;  // v4
        Vector3 topEndOuter = bottomEndOuter + Vector3.up * height;      // v5
        Vector3 topStartInner = bottomStartInner + Vector3.up * height;  // v6
        Vector3 topEndInner = bottomEndInner + Vector3.up * height;      // v7

        // Construim cele 6 fețe
        // REGULA: Vertecșii în ordine COUNTER-CLOCKWISE când privești fața din EXTERIOR

        // 1. OUTER FACE (partea exterioară - stânga)
        AddQuad(bottomStartOuter, bottomEndOuter, topStartOuter, topEndOuter, length, height);

        // 2. INNER FACE (partea interioară - dreapta) - INVERSATĂ
        AddQuad(bottomEndInner, bottomStartInner, topEndInner, topStartInner, length, height);

        // 3. LEFT FACE (capătul de start)
        AddQuad(bottomStartInner, bottomStartOuter, topStartInner, topStartOuter, width, height);

        // 4. RIGHT FACE (capătul de end) - INVERSATĂ
        AddQuad(bottomEndOuter, bottomEndInner, topEndOuter, topEndInner, width, height);

        // 5. TOP FACE (partea de sus) - INVERSATĂ
        AddQuad(topStartOuter, topEndOuter, topStartInner, topEndInner, length, width);

        // 6. BOTTOM FACE (partea de jos)
        AddQuad(bottomStartInner, bottomEndInner, bottomStartOuter, bottomEndOuter, length, width);

        UpdateMesh();
    }

    /// <summary>
    /// Adaugă un quad (2 triunghiuri) la mesh
    /// Vertecșii trebuie să fie în ordine COUNTER-CLOCKWISE când privești din exterior
    /// bl = bottom-left, br = bottom-right, tl = top-left, tr = top-right
    /// </summary>
    private void AddQuad(Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, float uvWidth, float uvHeight)
    {
        int startIndex = vertices.Count;

        // Adăugăm cei 4 vertecși
        vertices.Add(bl);
        vertices.Add(br);
        vertices.Add(tl);
        vertices.Add(tr);

        // Primul triunghi: bl -> br -> tl (COUNTER-CLOCKWISE)
        triangles.Add(startIndex + 0); // bl
        triangles.Add(startIndex + 1); // br
        triangles.Add(startIndex + 2); // tl

        // Al doilea triunghi: br -> tr -> tl (COUNTER-CLOCKWISE)
        triangles.Add(startIndex + 1); // br
        triangles.Add(startIndex + 3); // tr
        triangles.Add(startIndex + 2); // tl

        // UV Coordinates
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(uvWidth, 0));
        uvs.Add(new Vector2(0, uvHeight));
        uvs.Add(new Vector2(uvWidth, uvHeight));
    }

    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Actualizăm collider-ul - ELIMINĂ CONDIȚIA meshCollider.enabled
        if (meshCollider == null)
        {
            meshCollider = GetComponent<MeshCollider>();
        }

        if (meshCollider != null && mesh.vertexCount >= 3 && mesh.triangles.Length >= 3)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            meshCollider.enabled = true; // Forțează activarea
        }
    }

    public void SetMaterial(Material mat)
    {
        if (meshRenderer != null)
        {
            meshRenderer.material = mat;
        }
    }

    public Material GetMaterial()
    {
        // FIX: Returnăm direct variabila setată în Inspector, nu așteptăm MeshRenderer-ul
        if (wallMaterial != null)
            return wallMaterial;

        // Fallback: Dacă nu e setat sus, încercăm să luăm de pe renderer
        return meshRenderer != null ? meshRenderer.sharedMaterial : null;
    }

    public Vector3 GetStartPosition() => worldStartPos;
    public Vector3 GetEndPosition() => worldEndPos;
    public float GetLength() => Vector3.Distance(worldStartPos, worldEndPos);

    // Context menu pentru debugging
    [ContextMenu("Debug - Show Face Normals")]
    public void ShowFaceNormals()
    {
        if (mesh == null) return;

        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] tris = mesh.triangles;

        Debug.Log("=== FACE NORMALS ===");
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v0 = verts[tris[i]];
            Vector3 v1 = verts[tris[i + 1]];
            Vector3 v2 = verts[tris[i + 2]];

            Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            Vector3 worldNormal = transform.TransformDirection(faceNormal);

            Debug.Log($"Triunghi {i / 3}: Normal local={faceNormal:F3}, world={worldNormal:F3}");
        }
        Debug.Log("===================");
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || mesh == null) return;

        // Puncte start/end
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(worldStartPos, 0.15f);
        Gizmos.DrawSphere(worldStartPos, 0.08f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(worldEndPos, 0.15f);
        Gizmos.DrawSphere(worldEndPos, 0.08f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(worldStartPos, worldEndPos);

        // Desenăm normalele fiecărei fețe
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;

        if (verts != null && normals != null)
        {
            // La fiecare 4 vertecși (o față quad)
            for (int i = 0; i < verts.Length; i += 4)
            {
                if (i + 3 >= verts.Length) break;

                // Centrul feței
                Vector3 faceCenter = (verts[i] + verts[i + 1] + verts[i + 2] + verts[i + 3]) / 4f;
                Vector3 worldFaceCenter = transform.TransformPoint(faceCenter);

                // Normala medie a feței
                Vector3 faceNormal = (normals[i] + normals[i + 1] + normals[i + 2] + normals[i + 3]) / 4f;
                Vector3 worldNormal = transform.TransformDirection(faceNormal).normalized;

                // Desenăm
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(worldFaceCenter, 0.06f);

                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(worldFaceCenter, worldNormal * 0.5f);
                Gizmos.DrawSphere(worldFaceCenter + worldNormal * 0.5f, 0.04f);
            }
        }
    }
}