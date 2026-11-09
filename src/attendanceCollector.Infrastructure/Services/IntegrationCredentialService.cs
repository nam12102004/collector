using System.Security.Cryptography;
using System.Text;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;

namespace attendanceCollector.Infrastructure.Services;

public sealed class IntegrationCredentialService(IIntegrationStore store, TimeProvider clock) : IIntegrationCredentialService
{
    public IntegrationKeySecret Create(Guid keyId, DateTime? expiresAtUtc) =>
        new(keyId, keyId.ToString("N") + "." + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'), expiresAtUtc);

    public string GetVerifier(string apiKey) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    public async Task<IntegrationIdentity?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (apiKey.Length != 76 || apiKey[32] != '.' || apiKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_'))
            || !Guid.TryParseExact(apiKey[..32], "N", out var id)) return null;
        var key = await store.GetKeyAsync(id, cancellationToken);
        var supplied = Convert.FromHexString(GetVerifier(apiKey));
        // Even unknown key IDs perform a fixed-size verifier comparison.
        var expected = key is null ? new byte[32] : Convert.FromHexString(key.Verifier);
        var matches = CryptographicOperations.FixedTimeEquals(expected, supplied);
        if (!matches || key is null || key.RevokedAtUtc is not null || key.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) return null;
        return new(key.ClientId, key.Id);
    }
}
