namespace attendanceCollector.Application.Contracts.Users;

public sealed record CurrentUserDto(
    string? Subject,
    string? Email,
    bool IsAuthenticated,
    bool IsBootstrapAdmin);
