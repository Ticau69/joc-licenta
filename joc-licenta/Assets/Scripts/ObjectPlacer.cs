using UnityEngine;
using System.Collections.Generic;

public class ObjectPlacer : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> placedGameObjects = new();

    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation, bool isFurniture = false)
    {
        // 1. Creăm Root-ul la poziția FINALĂ din lume
        GameObject root = new GameObject(prefab.name + "_Root");
        root.transform.position = position;
        root.transform.rotation = rotation;

        // 2. Instanțiem obiectul vizual ca și copil
        GameObject newObject = Instantiate(prefab, root.transform);

        // 3. Resetăm poziția locală (ca să fim siguri că pleacă de la 0,0,0 față de root)
        newObject.transform.localPosition = Vector3.zero;
        newObject.transform.localRotation = Quaternion.identity;

        // 4. CALCULĂM CENTRAREA (Doar Local)
        // Nu folosim 'position' aici! Vrem doar offset-ul vizual.
        Bounds bounds = CalculateBounds(newObject);

        // Calculăm diferența dintre unde este Pivotul (root) și unde este Centrul Vizual (bounds)
        // 'transform.InverseTransformPoint' transformă un punct din Lume în Local
        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);

        // Aplicăm offset-ul invers pentru a centra obiectul
        newObject.transform.localPosition = new Vector3(-localCenter.x, 0, -localCenter.z);

        // --- SCHIMBARE LAYER AUTOMATĂ (NOU) ---
        // Setează aici numele layer-ului pe care îl dorești (ex: "Default" sau "Interactable")
        int targetLayer = LayerMask.NameToLayer("ObjectInteraction");

        // Aplicăm recursiv pe părinte (Root) și pe toți copiii (Mesh, Collider, RaftWorkStation_Pos etc.)
        SetLayerRecursively(root, targetLayer);

        // Opțional: Dacă vrei să păstrezi Y-ul original (să nu intre în pământ dacă pivotul e jos)
        // Comentează linia de mai sus și folosește:
        // newObject.transform.localPosition = new Vector3(-localCenter.x, 0, -localCenter.z);
        _ = StartCoroutine(AnimatePlacement(newObject.transform));


        placedGameObjects.Add(root);

        return placedGameObjects.Count - 1;
    }

    // Funcția care sapă prin toți copiii și le schimbă layer-ul
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    // Funcție ajutătoare pentru a găsi centrul vizual real
    private Bounds CalculateBounds(GameObject obj)
    {
        List<Renderer> validRenderers = new List<Renderer>();
        Renderer[] allRenderers = obj.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in allRenderers)
        {
            // NOU: Dacă renderer-ul ESTE un sistem de particule, îl ignorăm complet!
            if (r.enabled && !(r is ParticleSystemRenderer))
            {
                validRenderers.Add(r);
            }
        }

        if (validRenderers.Count == 0)
            return new Bounds(obj.transform.position, Vector3.zero);

        Bounds bounds = validRenderers[0].bounds;
        for (int i = 1; i < validRenderers.Count; i++)
        {
            bounds.Encapsulate(validRenderers[i].bounds);
        }
        return bounds;
    }

    // --- NOU: Efectul vizual de "Pop" la plasare ---
    private async Awaitable AnimatePlacement(Transform targetTransform)
    {
        float duration = 0.25f; // Cât de repede apare (0.25 secunde)
        float elapsed = 0f;
        Vector3 finalScale = targetTransform.localScale;

        // Începe de la mărimea 0 (invizibil)
        targetTransform.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            if (targetTransform == null) return;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Formulă matematică pentru "Ease Out Back" (sare un pic peste 100% și revine la normal)
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float easedT = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            // Prevenim valori negative accidentale
            if (easedT < 0) easedT = 0;

            targetTransform.localScale = finalScale * easedT;
            await Awaitable.NextFrameAsync();
        }

        if (targetTransform != null)
        {
            targetTransform.localScale = finalScale;
        }
    }

    public GameObject GetPlacedObject(int index)
    {
        if (index >= 0 && index < placedGameObjects.Count)
        {
            return placedGameObjects[index];
        }
        return null;
    }

    internal void RemoveObjectAt(int gameObjectIndex)
    {
        if (placedGameObjects.Count <= gameObjectIndex
            || placedGameObjects[gameObjectIndex] == null)
            return;
        Destroy(placedGameObjects[gameObjectIndex]);
        placedGameObjects[gameObjectIndex] = null;
    }
}