using UnityEngine;
using System.Collections;

public class SimpleDoorController : MonoBehaviour
{
    [Header("Setări Ușă Glisantă")]
    public Transform doorTransform; // Obiectul 3D al ușii

    [Tooltip("Cât de mult și în ce direcție se mișcă. X este stânga/dreapta.")]
    public Vector3 slideOffset = new Vector3(-0.6f, 0, 0); // -0.6 pe X înseamnă "Stânga 60cm"

    public float speed = 2.0f; // Viteza de glisare

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private Coroutine runningCoroutine;

    void Start()
    {
        if (doorTransform != null)
        {
            // Salvăm poziția inițială (ÎNCHIS)
            closedPosition = doorTransform.localPosition;

            // Calculăm poziția finală (DESCHIS)
            openPosition = closedPosition + slideOffset;
        }
    }

    public void Open()
    {
        if (doorTransform == null) return;
        if (runningCoroutine != null) StopCoroutine(runningCoroutine);
        runningCoroutine = StartCoroutine(AnimateDoor(openPosition));
    }

    public void Close()
    {
        if (doorTransform == null) return;
        if (runningCoroutine != null) StopCoroutine(runningCoroutine);
        runningCoroutine = StartCoroutine(AnimateDoor(closedPosition));
    }

    private IEnumerator AnimateDoor(Vector3 targetPos)
    {
        // Mișcăm ușa până ajunge la destinație
        while (Vector3.Distance(doorTransform.localPosition, targetPos) > 0.001f)
        {
            doorTransform.localPosition = Vector3.MoveTowards(
                doorTransform.localPosition,
                targetPos,
                Time.deltaTime * speed
            );
            yield return null;
        }
        doorTransform.localPosition = targetPos; // Fixăm poziția finală perfect
    }
}