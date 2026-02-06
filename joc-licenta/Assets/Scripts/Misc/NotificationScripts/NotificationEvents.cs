public enum NotificationType { Info, Warning, Error, Success }

public struct ShowNotificationEvent
{
    public string Title;
    public string Message;
    public NotificationType Type;
    public float Duration; // Cât timp stă pe ecran (ex: 5 secunde)

    public ShowNotificationEvent(string title, string message, NotificationType type = NotificationType.Info, float duration = 5f)
    {
        Title = title;
        Message = message;
        Type = type;
        Duration = duration;
    }
}