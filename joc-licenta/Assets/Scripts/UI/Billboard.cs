using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera _mainCamera;

    private void Start()
    {
        // Găsim camera principală la începutul jocului
        _mainCamera = Camera.main;
    }

    // Folosim LateUpdate în loc de Update. 
    // De ce? Camera ta se mișcă în Update. Vrem ca UI-ul să se rotească DUPĂ ce camera s-a oprit din mișcare în frame-ul curent.
    // Asta previne efectul de "tremurat" (jitter) al UI-ului.
    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        // Metoda 1 (LookAt): Obiectul se uită fix spre centrul camerei.
        // Problema: La UI Toolkit sau Canvas, textul va apărea inversat (în oglindă) pentru că axa Z (fața) privește spre tine.
        // transform.LookAt(_mainCamera.transform);
        // transform.Rotate(0, 180, 0); // Ar trebui să îl întorci manual cu 180 grade.

        // Metoda 2 (Cea Profesională pentru UI):
        // Copiem exact rotația camerei. Astfel, panoul UI va fi MEREU perfect paralel cu ecranul monitorului, 
        // indiferent de unghiul din care privești. Este ideal pentru lizibilitatea textului.
        transform.forward = _mainCamera.transform.forward;
    }
}