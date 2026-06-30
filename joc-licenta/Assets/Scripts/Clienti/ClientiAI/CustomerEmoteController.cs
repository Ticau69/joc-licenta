using System.Collections;
using UnityEngine;

/// <summary>
/// Gestionează feedback-ul vizual al clientului:
/// emote-uri sprite (ex: supărat pe preț) și animațiile Animator.
///
/// Este complet independent de logica de cumpărare și navigație —
/// poate fi extins cu orice emote nou fără a atinge CustomerAI.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CustomerNavigationHelper))]
public class CustomerEmoteController : MonoBehaviour
{
    // =========================================================================
    //  TIPURI
    // =========================================================================

    /// <summary>
    /// Stările vizuale pe care le poate afișa clientul.
    /// Oglindește CustomerAI.State, dar e separat intenționat —
    /// animațiile nu trebuie să cunoască logica de business.
    /// </summary>
    public enum VisualState
    {
        Idle,
        Walking,
        GoingToShelf,
        TakingProduct,
        GoingToRegister,
        InQueue,
        Leaving
    }

    // =========================================================================
    //  INSPECTOR
    // =========================================================================

    [Header("Emote Sprites")]
    [SerializeField] private SpriteRenderer emoteRenderer;
    [SerializeField] private Sprite angryPriceSprite;

    [Header("Emote Animație")]
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float displayDuration = 2.5f;

    // =========================================================================
    //  REFERINȚE
    // =========================================================================

    private Animator _animator;
    private CustomerNavigationHelper _nav;
    private Coroutine _activeEmoteCoroutine;

    // =========================================================================
    //  INIT
    // =========================================================================

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _nav = GetComponent<CustomerNavigationHelper>();

        // Ne asigurăm că emote-ul e ascuns la start
        if (emoteRenderer != null)
            emoteRenderer.enabled = false;
    }

    // =========================================================================
    //  API PUBLIC – Animații
    // =========================================================================

    /// <summary>
    /// Apelat din CustomerAI.Update() la fiecare frame cu starea curentă.
    /// Actualizează parametrii Animator în funcție de mișcare și stare.
    /// </summary>
    public void UpdateAnimations(VisualState state)
    {
        if (_animator == null) return;

        // Mersul se bazează pe viteza efectivă a agentului
        _animator.SetBool("isWalking", _nav.IsMoving());

        switch (state)
        {
            case VisualState.TakingProduct:
                _animator.SetBool("isGrabbing", true);
                _animator.SetBool("isRethinking", false);
                break;

            case VisualState.Idle:
                _animator.SetBool("isGrabbing", false);
                _animator.SetBool("isRethinking", true);
                break;

            // Toate stările de deplasare / așteptare pasivă
            case VisualState.GoingToShelf:
            case VisualState.GoingToRegister:
            case VisualState.InQueue:
            case VisualState.Leaving:
            default:
                _animator.SetBool("isGrabbing", false);
                _animator.SetBool("isRethinking", false);
                break;
        }
    }

    // =========================================================================
    //  API PUBLIC – Emote-uri
    // =========================================================================

    /// <summary>
    /// Afișează emote-ul de supărare pe preț cu animație de pop-up.
    /// Dacă un emote rulează deja, îl întrerupe și pornește de la zero.
    /// </summary>
    public void ShowAngryEmote()
    {
        if (emoteRenderer == null)
        {
            Debug.LogWarning($"[EmoteController] {name} – emoteRenderer nu este asignat!");
            return;
        }

        if (angryPriceSprite != null)
            emoteRenderer.sprite = angryPriceSprite;

        // Oprim emote-ul anterior dacă există
        if (_activeEmoteCoroutine != null)
            StopCoroutine(_activeEmoteCoroutine);

        _activeEmoteCoroutine = StartCoroutine(EmoteRoutine());
    }

    /// <summary>
    /// Ascunde imediat orice emote activ (ex: la ForceExit).
    /// </summary>
    public void HideEmote()
    {
        if (_activeEmoteCoroutine != null)
        {
            StopCoroutine(_activeEmoteCoroutine);
            _activeEmoteCoroutine = null;
        }

        if (emoteRenderer != null)
        {
            emoteRenderer.enabled = false;
            emoteRenderer.transform.localScale = Vector3.one;
        }
    }

    // =========================================================================
    //  COROUTINE ANIMATIE EMOTE
    // =========================================================================

    private IEnumerator EmoteRoutine()
    {
        var emoteTransform = emoteRenderer.transform;

        // 1. Pop-up: mărire de la 0 → 1 pentru efect de "juice"
        emoteRenderer.enabled = true;
        emoteTransform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            emoteTransform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, elapsed / popDuration);
            yield return null;
        }
        emoteTransform.localScale = Vector3.one;

        // 2. Afișare pentru displayDuration secunde
        yield return new WaitForSeconds(displayDuration);

        // 3. Stingere
        emoteRenderer.enabled = false;
        _activeEmoteCoroutine = null;
    }
}