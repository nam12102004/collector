using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;

namespace attendanceCollector.API.Services;

public sealed class HttpIntegrationRequestContext(IHttpContextAccessor accessor) : IIntegrationRequestContext
{
    public IntegrationIdentity? Identity => accessor.HttpContext?.User is { } user
        && user.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType == CollectorIntegrationConstant.Scheme)
        && Guid.TryParse(user.FindFirst(CollectorIntegrationConstant.ClientClaim)?.Value, out var clientId)
        && Guid.TryParse(user.FindFirst(CollectorIntegrationConstant.KeyClaim)?.Value, out var keyId) ? new(clientId, keyId) : null;
}
