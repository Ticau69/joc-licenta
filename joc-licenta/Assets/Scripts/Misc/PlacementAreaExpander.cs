using UnityEngine;

public class PlacementAreaExpander : MonoBehaviour
{
    [SerializeField] private Transform placementPlane; // plane-ul de placement (Transform)
    [SerializeField] private bool keepCenterPosition = true;

    public void SetPlaneScaleXZ(Vector2 scaleXZ)
    {
        if (placementPlane == null) return;

        var s = placementPlane.localScale;
        s.x = scaleXZ.x;
        s.z = scaleXZ.y;
        placementPlane.localScale = s;

        // dacă vrei să păstrezi centrul exact, de obicei nu trebuie să faci nimic
        // plane-ul scalează din centru. keepCenterPosition e aici doar ca flag.
        if (keepCenterPosition)
            placementPlane.localPosition = placementPlane.localPosition;
    }
}