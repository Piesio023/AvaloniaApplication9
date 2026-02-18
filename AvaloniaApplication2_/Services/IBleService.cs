namespace AvaloniaApplication2.Services;

public interface IBleService
{
    void StartServer();
    string Status { get; }
}