using UnityEngine;
using System.Collections.Generic;



public class ObjectPlacer : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> placedGameObjects = new();
    public int PlaceObject(GameObject prefab, Vector3 position)
    {
        GameObject newObject = Instantiate(prefab);
        newObject.transform.position = position;
        placedGameObjects.Add(newObject);

        return placedGameObjects.Count - 1;
    }

    public GameObject GetPlacedObject(int index)
    {
        if (index >= 0 && index < placedGameObjects.Count)
        {
            return placedGameObjects[index];
        }
        return null;
    }
}
