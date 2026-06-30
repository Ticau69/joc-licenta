using UnityEngine;

public class CursorChanger : MonoBehaviour
{
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Texture2D hoverCursorTexture;
    [SerializeField] private Vector2 hotSpot = Vector2.zero;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
    }

    public void OnMouseEnter() => Cursor.SetCursor(hoverCursorTexture, hotSpot, CursorMode.Auto);
    public void OnMouseExit() => Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
}
