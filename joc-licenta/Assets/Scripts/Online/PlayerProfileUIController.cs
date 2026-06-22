using UnityEngine;
using UnityEngine.UIElements;

public class PlayerProfileUIController : MonoBehaviour
{
    private VisualElement _playerCard;
    private Label _playerNameText;
    private IEventBus _eventBus;

    public void Initialize(VisualElement root, IEventBus eventBus)
    {
        _eventBus = eventBus;

        // Căutăm elementele în UI
        _playerCard = root.Q<VisualElement>("PlayerProfileCard");
        _playerNameText = root.Q<Label>("PlayerNameText");

        // Ne abonăm la evenimentul de logare (pe care îl trimiți din AuthManager)
        if (_eventBus != null)
        {
            _eventBus.Subscribe<UserAuthenticatedEvent>(OnUserAuthenticated);
        }
    }

    private void OnUserAuthenticated(UserAuthenticatedEvent e)
    {
        if (_playerCard == null || _playerNameText == null) return;

        // Dacă nu avem un username (displayName) setat, folosim partea dinaintea '@' din email
        string displayName = e.Username; // Presupunând că ai adăugat asta în event

        if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(e.Email))
        {
            displayName = e.Email.Split('@')[0];
        }

        // Punem litera mare la început pentru aspect
        if (!string.IsNullOrEmpty(displayName))
        {
            displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);
        }
        else
        {
            displayName = "Manager Magazin"; // Fallback
        }

        _playerNameText.text = displayName;

        // Afișăm cardul
        _playerCard.style.display = DisplayStyle.Flex;
    }

    void OnDestroy()
    {
        _eventBus?.Unsubscribe<UserAuthenticatedEvent>(OnUserAuthenticated);
    }
}