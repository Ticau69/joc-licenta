using UnityEngine;

/// <summary>
/// Indicator vizual pentru punctul de start al peretelui (când începi să tragi)
/// </summary>
public class WallStartIndicator : MonoBehaviour
{
    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField] private float indicatorHeight = 0.05f;
    [SerializeField] private float indicatorSize = 0.3f;
    [SerializeField] private Color indicatorColor = Color.green;

    private GameObject activeIndicator;

    private void Start()
    {
        // Creăm indicator-ul dacă nu există prefab
        if (indicatorPrefab == null)
        {
            CreateDefaultIndicator();
        }
    }

    /// <summary>
    /// Arată indicator-ul la poziția de start
    /// </summary>
    public void ShowIndicator(Vector3 position)
    {
        if (activeIndicator == null)
        {
            activeIndicator = Instantiate(indicatorPrefab, transform);
        }

        activeIndicator.SetActive(true);
        activeIndicator.transform.position = position + Vector3.up * indicatorHeight;

        // Pulsare pentru vizibilitate
        StartCoroutine(PulseIndicator());
    }

    /// <summary>
    /// Ascunde indicator-ul
    /// </summary>
    public void HideIndicator()
    {
        if (activeIndicator != null)
        {
            StopAllCoroutines();
            activeIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// Actualizează culoarea indicator-ului
    /// </summary>
    public void SetIndicatorColor(Color color)
    {
        if (activeIndicator != null)
        {
            Renderer renderer = activeIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }
    }

    private void CreateDefaultIndicator()
    {
        // Creăm un cilindru simplu ca indicator
        indicatorPrefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicatorPrefab.transform.localScale = new Vector3(indicatorSize, 0.05f, indicatorSize);

        // Material transparent
        Renderer renderer = indicatorPrefab.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = indicatorColor;
        renderer.material.SetFloat("_Mode", 3); // Transparent mode
        renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        renderer.material.SetInt("_ZWrite", 0);
        renderer.material.DisableKeyword("_ALPHATEST_ON");
        renderer.material.EnableKeyword("_ALPHABLEND_ON");
        renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        renderer.material.renderQueue = 3000;

        // Ștergem collider-ul
        Collider collider = indicatorPrefab.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        indicatorPrefab.SetActive(false);
    }

    private System.Collections.IEnumerator PulseIndicator()
    {
        Renderer renderer = activeIndicator.GetComponent<Renderer>();
        if (renderer == null) yield break;

        float time = 0;
        Color baseColor = indicatorColor;

        while (true)
        {
            time += Time.deltaTime * 2f;
            float alpha = Mathf.Lerp(0.3f, 0.8f, (Mathf.Sin(time) + 1f) / 2f);

            Color newColor = baseColor;
            newColor.a = alpha;
            renderer.material.color = newColor;

            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (activeIndicator != null)
        {
            Destroy(activeIndicator);
        }
    }
}