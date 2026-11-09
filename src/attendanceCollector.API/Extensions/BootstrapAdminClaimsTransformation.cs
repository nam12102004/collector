using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace attendanceCollector.API.Extensions;

public sealed class BootstrapAdminClaimsTransformation(
    string email,
    string externalSubject) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated);
        var subject = principal.FindFirst("sub")?.Value;
        var tokenEmail = principal.FindFirst("email")?.Value;

        if (identity is not null
            && string.Equals(subject, externalSubject, StringComparison.Ordinal)
            && string.Equals(tokenEmail, email, StringComparison.OrdinalIgnoreCase)
            && !principal.IsInRole("BootstrapAdmin"))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "BootstrapAdmin"));
        }

        return Task.FromResult(principal);
    }
}
