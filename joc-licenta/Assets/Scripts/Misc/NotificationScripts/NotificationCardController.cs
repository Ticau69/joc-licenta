using UnityEngine;
using UnityEngine.UIElements;

public class NotificationCardController
{
    private VisualElement _cardRoot;
    private IVisualElementScheduledItem _autoCloseSchedule;

    public NotificationCardController(VisualElement cardRoot, ShowNotificationEvent data)
    {
        Debug.Log($"Notification created | duration = {data.Duration}");

        _cardRoot = cardRoot;
        SetupUI(data);

        if (data.Duration > 0)
        {
            Debug.Log("Starting auto close...");
            StartAutoClose(data.Duration);
        }
    }


    private void SetupUI(ShowNotificationEvent data)
    {
        // Găsim elementele din UXML
        var notificationCard = _cardRoot.Q<VisualElement>("NotificationCard");
        var titleLabel = _cardRoot.Q<Label>("Title");
        var messageLabel = _cardRoot.Q<Label>("Message");
        var icon = _cardRoot.Q<VisualElement>("Icon");
        var closeButton = _cardRoot.Q<Button>("CloseButton");

        if (titleLabel != null) titleLabel.text = data.Title;
        if (messageLabel != null) messageLabel.text = data.Message;

        if (closeButton != null)
        {
            closeButton.clicked += Close;
        }

        // Setăm culoarea bazată pe tip
        Color borderColor = Color.white;

        switch (data.Type)
        {
            case NotificationType.Error:
                borderColor = new Color(0.9f, 0.2f, 0.2f); // Red
                break;
            case NotificationType.Warning:
                borderColor = new Color(1f, 0.8f, 0.2f); // Yellow/Orange
                break;
            case NotificationType.Success:
                borderColor = new Color(0.2f, 0.8f, 0.3f); // Green
                break;
            case NotificationType.Info:
                borderColor = new Color(0.3f, 0.6f, 1f); // Blue
                break;
        }

        notificationCard.style.borderLeftColor = borderColor;
        notificationCard.style.borderTopColor = borderColor;
        notificationCard.style.borderRightColor = borderColor;
        notificationCard.style.borderBottomColor = borderColor;

        // ========================================
        // ANIMAȚIE DE INTRARE
        // ========================================

        // Starea inițială
        _cardRoot.style.translate = new Translate(300, 0, 0);
        _cardRoot.style.opacity = 0;

        // Animăm la poziția finală după un delay mic
        _cardRoot.schedule.Execute(() =>
        {
            _cardRoot.style.translate = new Translate(0, 0, 0);
            _cardRoot.style.opacity = 1;
        }).StartingIn(50);
    }

    private void StartAutoClose(float duration)
    {
        _autoCloseSchedule?.Pause();
        Debug.Log($"AutoClose in {duration} sec");
        _autoCloseSchedule = _cardRoot.schedule
            .Execute(Close)
            .StartingIn(Mathf.RoundToInt(duration * 1000f));
    }

    private void Close()
    {
        if (_cardRoot == null || _cardRoot.parent == null) return;

        // Anulăm auto-close dacă încă rulează
        _autoCloseSchedule?.Pause();

        // Animație de ieșire
        _cardRoot.style.translate = new Translate(300, 0, 0);
        _cardRoot.style.opacity = 0;

        // Ștergem elementul după ce se termină animația
        _cardRoot.schedule.Execute(() =>
        {
            if (_cardRoot?.parent != null)
            {
                _cardRoot.parent.Remove(_cardRoot);
            }
        }).StartingIn(350); // 350ms - puțin mai mult decât tranziția
    }
}