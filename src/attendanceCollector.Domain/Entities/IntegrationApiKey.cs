namespace attendanceCollector.Domain.Entities;

public sealed class IntegrationApiKey
{
    private IntegrationApiKey() { }
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Verifier { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public static IntegrationApiKey Create(Guid id, Guid clientId, string verifier, DateTime nowUtc, DateTime? expiresAtUtc) => new()
    {
        Id = id, ClientId = clientId, Verifier = verifier, CreatedAtUtc = nowUtc, ExpiresAtUtc = expiresAtUtc
    };
    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;
}
