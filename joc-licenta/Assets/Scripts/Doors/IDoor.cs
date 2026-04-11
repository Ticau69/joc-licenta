/// <summary>
/// Interfață comună pentru toate tipurile de uși.
/// Atât ClassicDoor cât și AutoSlidingDoor o implementează.
/// </summary>
public interface IDoor
{
    bool IsOpen    { get; }
    bool IsLocked  { get; }
    void Open();
    void Close();
    void RequestOpen(DoorInteractor requester);
    void RequestClose(DoorInteractor requester);
}