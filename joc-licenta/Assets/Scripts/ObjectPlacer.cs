using UnityEngine;
using System.Collections.Generic;

public class ObjectPlacer : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> placedGameObjects = new();

    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation)
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

        // Opțional: Dacă vrei să păstrezi Y-ul original (să nu intre în pământ dacă pivotul e jos)
        // Comentează linia de mai sus și folosește:
        // newObject.transform.localPosition = new Vector3(-localCenter.x, 0, -localCenter.z);

        placedGameObjects.Add(root);

        return placedGameObjects.Count - 1;
    }

    // Funcție ajutătoare pentru a găsi centrul vizual real
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

    public GameObject GetPlacedObject(int index)
    {
        if (index >= 0 && index < placedGameObjects.Count)
        {
            return placedGameObjects[index];
        }
        return null;
    }

    public void RemoveObjectAt(int index)
    {
        if (index >= 0 && index < placedGameObjects.Count && placedGameObjects[index] != null)
        {
            Destroy(placedGameObjects[index]);
            placedGameObjects[index] = null;
        }
    }
}