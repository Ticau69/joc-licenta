using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        // Luăm direcția în care se uită camera
        Vector3 lookDirection = _mainCamera.transform.forward;

        // ANULĂM direcția sus/jos pentru a forța rotația DOAR stânga-dreapta
        lookDirection.y = 0f;

        // Ne asigurăm că vectorul nu a devenit zero (ex: dacă te uiți perfect de sus în jos)
        if (lookDirection != Vector3.zero)
        {
            // Aplicăm direcția
            transform.forward = lookDirection;
        }
    }
}