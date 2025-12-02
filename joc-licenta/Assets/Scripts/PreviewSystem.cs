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
        previewObject.transform.localPosition = -bounds.center;

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
        ApplyFeedback(validity);
    }

    private void PrepareCursor(Vector2Int size)
    {
        if (size.x > 0 && size.y > 0)
        {
            cellIndicator.transform.localScale = new Vector3(size.x, 1, size.y);
            cellIndicatorRenderer.material.SetVector("_Tiling", new Vector2(size.x, size.y));
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

    private void ApplyFeedback(bool validity)
    {
        Color c = validity ? Color.white : Color.red;
        cellIndicatorRenderer.material.color = c;
        c.a = 0.5f;
        previewMaterialInstance.color = c;
    }

    private void MoveCursor(Vector3 position)
    {
        cellIndicator.transform.position = position;
    }

    private void MovePreview(Vector3 position)
    {
        if (previewRoot != null)
        {
            previewRoot.transform.position = new Vector3(
                position.x,
                position.y + previewYOffset,
                position.z
            );
        }
    }
}