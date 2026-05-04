using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Controller UI pentru mentorul din colțul dreapta-jos.
/// Avatar + speech bubble cu animație de intrare/ieșire.
/// </summary>
public class MentorUIController : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite spriteHappy;   // Imagine 1 — brațe deschise (bun venit)
    [SerializeField] private Sprite spriteActive;  // Imagine 2 — din lateral, mână ridicată (default)
    [SerializeField] private Sprite spriteAngry;   // Imagine 3 — supărat (crize)

    [Header("Timing")]
    [SerializeField] private float displayDuration = 8f;
    [SerializeField] private float typewriterSpeed = 0.03f; // secunde per caracter

    // =========================================================================
    // PRIVATE UI
    // =========================================================================

    private VisualElement _mentorContainer;
    private VisualElement _avatarEl;
    private VisualElement _avatarElBack;   // al doilea layer pentru crossfade
    private Label _messageLabel;
    private Label _phaseLabel;
    private Button _closeBtn;
    private VisualElement _typingIndicator;

    private Sprite _currentSprite;
    private Coroutine _crossfadeCoroutine;

    private Coroutine _displayCoroutine;
    private Coroutine _typewriterCoroutine;

    private readonly System.Text.StringBuilder _sb = new();

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        _mentorContainer = root.Q("MentorContainer");
        _avatarEl = root.Q("MentorAvatar");
        _messageLabel = root.Q<Label>("MentorMessage");
        _phaseLabel = root.Q<Label>("MentorPhaseLabel");
        _closeBtn = root.Q<Button>("MentorCloseBtn");
        _typingIndicator = root.Q("MentorTypingDots");

        // ── Creăm al doilea layer de avatar pentru crossfade ──────────────
        if (_avatarEl != null)
        {
            _avatarElBack = new VisualElement();
            _avatarElBack.style.position = Position.Absolute;
            _avatarElBack.style.top = 0;
            _avatarElBack.style.left = 0;
            _avatarElBack.style.width = Length.Percent(100);
            _avatarElBack.style.height = Length.Percent(100);
            _avatarElBack.style.opacity = 0f;
            _avatarEl.Add(_avatarElBack);
        }

        // Sprite default — cel activ (din lateral)
        SetAvatarSprite(spriteActive, instant: true);

        if (_closeBtn != null)
            _closeBtn.clicked += HideImmediate;

        // Ascuns la start
        SetVisible(false, instant: true);
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public void ShowMessage(string text, MentorEventType eventType = MentorEventType.Welcome)
    {
        if (_mentorContainer == null) return;

        // Schimbăm sprite-ul în funcție de eveniment
        Sprite targetSprite = GetSpriteForEvent(eventType);
        SetAvatarSprite(targetSprite, instant: false);

        if (_displayCoroutine != null) StopCoroutine(_displayCoroutine);
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);

        _displayCoroutine = StartCoroutine(DisplayRoutine(text));
    }

    public void HideImmediate()
    {
        if (_displayCoroutine != null) { StopCoroutine(_displayCoroutine); _displayCoroutine = null; }
        if (_typewriterCoroutine != null) { StopCoroutine(_typewriterCoroutine); _typewriterCoroutine = null; }
        SetVisible(false, instant: false);
    }

    // =========================================================================
    // COROUTINES
    // =========================================================================

    private IEnumerator DisplayRoutine(string text)
    {
        // 1. Indicator typing (3 puncte animate)
        if (_messageLabel != null) _messageLabel.text = "";
        if (_typingIndicator != null) _typingIndicator.style.display = DisplayStyle.Flex;

        SetVisible(true, instant: false);
        UpdatePhaseLabel();

        yield return new WaitForSecondsRealtime(0.6f);

        // 2. Ascundem typing indicator
        if (_typingIndicator != null) _typingIndicator.style.display = DisplayStyle.None;

        // 3. Typewriter effect
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(text));
        yield return _typewriterCoroutine;

        // 4. Așteptăm displayDuration
        yield return new WaitForSecondsRealtime(displayDuration);

        // 5. Fade out
        SetVisible(false, instant: false);
        _displayCoroutine = null;
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        if (_messageLabel == null) yield break;

        _sb.Clear();
        bool inRichTag = false;

        foreach (char c in fullText)
        {
            // Sărim peste tag-urile rich text (le adăugăm dintr-odată)
            if (c == '<') inRichTag = true;
            if (c == '>') inRichTag = false;

            _sb.Append(c);
            _messageLabel.text = _sb.ToString();

            if (!inRichTag && c != ' ')
                yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        _typewriterCoroutine = null;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void SetVisible(bool visible, bool instant)
    {
        if (_mentorContainer == null) return;

        if (instant)
        {
            _mentorContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _mentorContainer.style.opacity = visible ? 1f : 0f;
            return;
        }

        if (visible)
        {
            _mentorContainer.style.display = DisplayStyle.Flex;
            // Slide in din dreapta + fade
            _mentorContainer.style.translate = new StyleTranslate(new Translate(Length.Percent(0), 0));
            _mentorContainer.style.opacity = 1f;
        }
        else
        {
            // Slide out spre dreapta + fade
            _mentorContainer.style.translate = new StyleTranslate(new Translate(Length.Percent(110), 0));
            _mentorContainer.style.opacity = 0f;

            // Ascundem după tranziție
            StartCoroutine(HideAfterDelay(0.4f));
        }
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (_mentorContainer != null)
            _mentorContainer.style.display = DisplayStyle.None;
    }

    // =========================================================================
    // SPRITE CROSSFADE
    // =========================================================================

    private Sprite GetSpriteForEvent(MentorEventType eventType)
    {
        switch (eventType)
        {
            // Supărat — crize, probleme grave
            case MentorEventType.EmployeeResigned:
            case MentorEventType.LoanMissed:
            case MentorEventType.SanitaryFine:
            case MentorEventType.LowFunds:
            case MentorEventType.UnprofitableDay:
            case MentorEventType.SupplierAngry:
            case MentorEventType.CompetitorCheaper:
                return spriteAngry ?? spriteActive;

            // Fericit — bun venit, succese
            case MentorEventType.Welcome:
            case MentorEventType.SanitaryPass:
            case MentorEventType.ProfitableDay:
            case MentorEventType.PlayerCheapest:
                return spriteHappy ?? spriteActive;

            // Default activ — informații neutre
            default:
                return spriteActive;
        }
    }

    private void SetAvatarSprite(Sprite sprite, bool instant)
    {
        if (_avatarEl == null || sprite == null) return;
        if (sprite == _currentSprite) return;

        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);

        if (instant)
        {
            _avatarEl.style.backgroundImage = new StyleBackground(sprite);
            _currentSprite = sprite;
            return;
        }

        _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(sprite));
    }

    private IEnumerator CrossfadeRoutine(Sprite newSprite)
    {
        if (_avatarElBack == null) yield break;

        // Punem sprite-ul NOU pe layer-ul din spate
        _avatarElBack.style.backgroundImage = new StyleBackground(newSprite);
        _avatarElBack.style.opacity = 0f;

        float duration = 0.3f;
        float elapsed = 0f;

        // Fade: back layer 0→1, front layer 1→0
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            _avatarElBack.style.opacity = t;
            _avatarEl.style.opacity = 1f - t;

            yield return null;
        }

        // Swap — front preia sprite-ul nou, back se ascunde
        _avatarEl.style.backgroundImage = new StyleBackground(newSprite);
        _avatarEl.style.opacity = 1f;
        _avatarElBack.style.opacity = 0f;

        _currentSprite = newSprite;
        _crossfadeCoroutine = null;
    }

    private void UpdatePhaseLabel()
    {
        if (_phaseLabel == null || MentorSystem.Instance == null) return;

        var phase = MentorSystem.Instance.GetCurrentPhase();
        float remaining = MentorSystem.Instance.GetRemainingActiveMinutes();

        switch (phase)
        {
            case MentorPhase.VeryActive:
                _phaseLabel.text = "Mentor activ";
                _phaseLabel.style.color = new StyleColor(new Color(0.3f, 0.85f, 0.3f));
                break;
            case MentorPhase.Moderate:
                _phaseLabel.text = $"Mentor — {remaining:F0} min rămase";
                _phaseLabel.style.color = new StyleColor(new Color(1f, 0.8f, 0.2f));
                break;
            case MentorPhase.Rare:
                _phaseLabel.text = "Ultimele sfaturi...";
                _phaseLabel.style.color = new StyleColor(new Color(1f, 0.5f, 0.2f));
                break;
            default:
                _phaseLabel.text = "";
                break;
        }
    }
}