using UnityEngine;

public class PreviewSystem : MonoBehaviour
{
    [SerializeField] private float previewYOffset = 0.06f;
    [SerializeField] private GameObject cellIndicator;

    private GameObject previewObject;
    private GameObject previewRoot; // ROOT pentru rotaÈ›ie

    [SerializeField] private Material previewMaterialPrefab;
    private Material previewMaterialInstance;

    private Renderer cellIndicatorRenderer;
    private Vector2Int currentSize = Vector2Int.one;
    private Quaternion currentRotation = Quaternion.identity;

    private void Start()
    {
        previewMaterialInstance = new Material(previewMaterialPrefab);
        cellIndicator.SetActive(false);
        cellIndicatorRenderer = cellIndicator.GetComponentInChildren<Renderer>();
    }

    public void StartShowingPlacementPreview(GameObject prefab, Vector2Int size)
    {
        // CreÄƒm ROOT-ul pentru rotaÈ›ie
        previewRoot = new GameObject("PreviewRoot");

        // Instantiem obiectul ca È™i copil
        previewObject = Instantiate(prefab, previewRoot.transform);

        // CalculÄƒm bounds È™i centrÄƒm obiectul
        Bounds bounds = CalculateBounds(previewObject);
        previewObject.transform.localPosition = new Vector3(-bounds.center.x, 0, -bounds.center.z);

        PreparePreviewObject(previewObject);

        currentSize = size;
        currentRotation = Quaternion.identity;

        PrepareCursor(size);
        cellIndicator.SetActive(true);
    }

    public void StopShowingPreview()
    {
        cellIndicator.SetActive(false);
        if (previewRoot != null)
        {
            Destroy(previewRoot);
            previewRoot = null;
        }
        previewObject = null;
    }

    // METODÄ‚ NOUÄ‚: RoteÈ™te preview-ul
    public void RotatePreview()
    {
        if (previewRoot == null) return;

        // Rotim cu 90 de grade
        currentRotation *= Quaternion.Euler(0, 90, 0);
        previewRoot.transform.rotation = currentRotation;

        // IMPORTANT: InversÄƒm dimensiunile la rotaÈ›ie
        currentSize = new Vector2Int(currentSize.y, currentSize.x);
        PrepareCursor(currentSize);
    }

    // METODÄ‚ NOUÄ‚: ReturneazÄƒ rotaÈ›ia curentÄƒ
    public Quaternion GetCurrentRotation()
    {
        return currentRotation;
    }

    // METODÄ‚ NOUÄ‚: ReturneazÄƒ dimensiunea curentÄƒ
    public Vector2Int GetCurrentSize()
    {
        return currentSize;
    }

    public GameObject GetPreviewObject()
    {
        return previewObject;
    }

    public void UpdatePosition(Vector3 position, bool validity)
    {
        // CalculÄƒm centrul pentru poziÈ›ionare
        Vector3 centerPosition = new Vector3(
            position.x + (currentSize.x / 2f),
            position.y,
            position.z + (currentSize.y / 2f)
        );

        MovePreview(centerPosition);
        MoveCursor(position);
        ApplyFeedbackToCursor(validity);
        ApplyFeedbackToPreview(validity);
    }

    public void UpdateWallPreview(Vector3 position, Quaternion rotation, bool validity)
    {
        // 1. Mutăm obiectul 3D exact la coordonata primită (deja calculată în State)
        if (previewRoot != null)
        {
            MovePreview(position, rotation);
            //previewRoot.transform.position = position;
            //previewRoot.transform.rotation = rotation;
        }


        // 4. Aplicăm culoarea (Roșu/Alb)
        ApplyFeedbackToPreview(validity);
    }

    public void ToggleCursorVisibility(bool isVisible)
    {
        cellIndicator.SetActive(isVisible);
    }

    private void PrepareCursor(Vector2Int size)
    {
        if (size.x > 0 && size.y > 0)
        {
            cellIndicator.transform.localScale = new Vector3(size.x, 1, size.y);
            cellIndicatorRenderer.material.SetVector("_Tiling", new Vector2(size.x, size.y));
        }
    }

    public void SetCursorSize(Vector2Int newSize)
    {
        // Modificăm scara obiectului alb
        if (newSize.x > 0 && newSize.y > 0)
        {
            cellIndicator.transform.localScale = new Vector3(newSize.x, 1, newSize.y);

            // Actualizăm și tiling-ul texturii ca să arate a grilă, nu a pătrat întins
            cellIndicatorRenderer.material.SetVector("_Tiling", new Vector2(newSize.x, newSize.y));
        }
    }

    private void PreparePreviewObject(GameObject previewObject)
    {
        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = previewMaterialInstance;
            }
            renderer.materials = materials;
        }
    }

    private Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private void ApplyFeedbackToPreview(bool validity)
    {
        Color c = validity ? Color.white : Color.red;
        c.a = 0.5f;
        previewMaterialInstance.color = c;
    }

    private void ApplyFeedbackToCursor(bool validity)
    {
        Color c = validity ? Color.white : Color.red;

        c.a = 0.5f;
        cellIndicatorRenderer.material.color = c;
    }

    private void MoveCursor(Vector3 position)
    {
        cellIndicator.transform.position = position;
    }

    private void MovePreview(Vector3 position, Quaternion? rotation = null)
    {
        if (previewRoot != null)
        {
            previewRoot.transform.position = new Vector3(
                position.x,
                position.y + previewYOffset,
                position.z
            );
            if (rotation.HasValue)
            {
                previewRoot.transform.rotation = rotation.Value;
            }
        }
    }

    public void StartShowingRemovePreview()
    {
        cellIndicator.SetActive(true);
        PrepareCursor(Vector2Int.one);
        ApplyFeedbackToCursor(false);
    }
}