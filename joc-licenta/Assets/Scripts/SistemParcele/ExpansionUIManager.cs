using UnityEngine;
using UnityEngine.UIElements;

public class ExpansionUIManager : MonoBehaviour
{
    [Header("Referințe Externe")]
    public UIDocument mainHUDDocument;

    private Button _expandButton;
    private bool _isExpandModeOn = false;
    private bool _isSelfPublishing = false;

    private void OnEnable()
    {
        if (mainHUDDocument == null) return;

        var root = mainHUDDocument.rootVisualElement;

        // Înlocuiește "ExpandButton" cu numele pe care i l-ai pus tu în UI Builder
        _expandButton = root.Q<Button>("ExpensionButton");

        if (_expandButton != null)
        {
            _expandButton.clicked += ToggleExpandMode;
        }

        // Ascultăm dacă un alt panou vrea să se deschidă, ca să ne închidem noi
        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Subscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void OnDisable()
    {
        if (_expandButton != null)
        {
            _expandButton.clicked -= ToggleExpandMode;
        }

        if (ServiceLocator.Instance != null && ServiceLocator.Instance.TryGet(out IEventBus eventBus))
        {
            eventBus.Unsubscribe<CloseAllUIEvent>(OnCloseAllUIReceived);
        }
    }

    private void ToggleExpandMode()
    {
        _isExpandModeOn = !_isExpandModeOn;

        if (_isExpandModeOn)
        {
            // Flagul previne ca OnCloseAllUIReceived să ne reseteze propria stare
            _isSelfPublishing = true;
            if (ServiceLocator.Instance.TryGet(out IEventBus eb))
                eb.Publish(new CloseAllUIEvent());
            _isSelfPublishing = false;
        }

        if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
            eventBus.Publish(new ToggleExpansionModeEvent(_isExpandModeOn));

        UpdateButtonVisuals();
    }

    private void OnCloseAllUIReceived(CloseAllUIEvent e)
    {
        // Ignorăm dacă NOI am publicat evenimentul
        if (_isSelfPublishing) return;

        if (_isExpandModeOn)
        {
            _isExpandModeOn = false;
            UpdateButtonVisuals();

            if (ServiceLocator.Instance.TryGet(out IEventBus eventBus))
                eventBus.Publish(new ToggleExpansionModeEvent(false));
        }
    }

    private void UpdateButtonVisuals()
    {
        if (_expandButton == null) return;

        // Când e activ, îi scădem opacitatea (sau poți schimba culoarea fundalului)
        // ca jucătorul să își dea seama că butonul e "apăsat"
        _expandButton.style.opacity = _isExpandModeOn ? 0.5f : 1f;
    }
}