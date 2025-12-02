using UnityEngine;

[ExecuteInEditMode]
public class ObjectCenteringTool : MonoBehaviour
{
    [Header("Click this to fix alignment")]
    public bool centerNow = false;

    void Update()
    {
        if (centerNow)
        {
            CenterChildren();
            centerNow = false;
        }
    }

    private void CenterChildren()
    {
        // 1. Găsim centrul geometric al tuturor copiilor (Mesh-uri)
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogError("Nu am găsit niciun Mesh Renderer pe copii!");
            return;
        }

        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        // 2. Calculăm diferența dintre Centrul Geometric și Pivotul actual (transform.position)
        Vector3 centerOffset = transform.position - bounds.center;

        // Ignorăm înălțimea (Y) dacă vrem să stea pe podea, centrăm doar X și Z
        centerOffset.y = 0;

        // 3. Aplicăm offset-ul invers tuturor copiilor
        foreach (Transform child in transform)
        {
            child.position += centerOffset;
        }

        Debug.Log($"<color=green>Obiect Centrat! Offset aplicat: {centerOffset}</color>");
    }
}