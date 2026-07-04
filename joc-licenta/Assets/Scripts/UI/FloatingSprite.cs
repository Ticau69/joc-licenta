using UnityEngine;

public class FloatingSprite : MonoBehaviour
{
    public float floatAmplitude = 0.2f; // Cât de mult urcă și coboară
    public float floatFrequency = 2f;   // Cât de repede se mișcă

    private Vector3 _startLocalPos;

    private void Start()
    {
        _startLocalPos = transform.localPosition;
    }

    private void Update()
    {
        // Calculăm doar mișcarea pe axa Y folosind curba matematică Sinus
        float newY = _startLocalPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.localPosition = new Vector3(_startLocalPos.x, newY, _startLocalPos.z);
    }
}