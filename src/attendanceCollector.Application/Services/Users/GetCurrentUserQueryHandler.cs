using attendanceCollector.Application.Contracts.Users;
using attendanceCollector.Application.Interfaces;

namespace attendanceCollector.Application.Services.Users;

public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser)
{
    public CurrentUserDto Handle(GetCurrentUserQuery query) => new(
        currentUser.Subject,
        currentUser.Email,
        currentUser.IsAuthenticated,
        currentUser.IsBootstrapAdmin);
}

