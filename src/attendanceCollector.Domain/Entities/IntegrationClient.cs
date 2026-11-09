namespace attendanceCollector.Domain.Entities;

public sealed class IntegrationClient
{
    private IntegrationClient() { }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public bool IsActive { get; private set; }
    public string Scope { get; private set; } = "";
    public Guid Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static IntegrationClient Create(string name, string scope, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(), Name = name, Scope = scope, IsActive = true,
        Version = Guid.NewGuid(), CreatedAtUtc = nowUtc
    };
    public void SetActive(bool active) { IsActive = active; Touch(); }
    public void Touch() => Version = Guid.NewGuid();
}

public sealed class IntegrationDeviceGrant
{
    public Guid ClientId { get; set; }
    public Guid DeviceId { get; set; }
}
