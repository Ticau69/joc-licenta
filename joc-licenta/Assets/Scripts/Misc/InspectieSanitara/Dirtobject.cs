using UnityEngine;

/// <summary>
/// Component pe prefab-ul de gunoi/pată.
/// Se autodistruge când e curățat de Janitor.
/// </summary>
public class DirtObject : MonoBehaviour
{
    [Tooltip("Timp de curățare (secunde) — cât stă Janitorul lângă el.")]
    public float cleanDuration = 1.5f;

    private bool _isBeingCleaned = false;
    private float _cleanTimer = 0f;

    void Update()
    {
        if (!_isBeingCleaned) return;

        _cleanTimer += Time.deltaTime;
        if (_cleanTimer >= cleanDuration)
            FinishCleaning();
    }

    /// <summary>Janitorul a ajuns și începe curățarea.</summary>
    public void StartCleaning()
    {
        if (_isBeingCleaned) return;
        _isBeingCleaned = true;
        _cleanTimer = 0f;
    }

    /// <summary>Janitorul a plecat înainte să termine.</summary>
    public void CancelCleaning()
    {
        _isBeingCleaned = false;
        _cleanTimer = 0f;
    }

    private void FinishCleaning()
    {
        CleanlinessManager.Instance?.OnDirtCleaned(this);
        Destroy(gameObject);
    }
}