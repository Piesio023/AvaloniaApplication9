namespace AvaloniaApplication2.Services;

public interface IBleService
{
    void StartServer(string serviceUuid, string charUuid, string newName);
    public void BleServer_SendNotification(string message);
    public void StopServer();
    string Status { get; }
}