namespace attendanceCollector.Application.Interfaces;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? Subject { get; }
    string? Email { get; }
    bool IsBootstrapAdmin { get; }
}
