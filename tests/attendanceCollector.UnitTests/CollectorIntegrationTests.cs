using attendanceCollector.API.Services;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Services;
using attendanceCollector.Application.Validators;
using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Moq;

namespace attendanceCollector.UnitTests;

public sealed class CollectorIntegrationTests
{
    private static readonly Guid Device = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
    private static CollectorLogRequest Request(string? token = null, int size = 2) => new()
    { FromUtc = "2026-11-06T00:00:00Z", ToUtc = "2026-11-07T00:00:00Z", PageSize = size, PageToken = token };

    [Theory]
    [InlineData("2026-11-06T00:00:00", "2026-11-07T00:00:00Z", 1)]
    [InlineData("2026-11-06T00:00:00+07:00", "2026-11-07T00:00:00Z", 1)]
    [InlineData("2026-11-06T00:00:00Z", "2026-11-06T00:00:00Z", 1)]
    [InlineData("2026-11-06T00:00:00Z", "2026-11-05T00:00:00Z", 1)]
    [InlineData("2026-11-06T00:00:00Z", "2026-12-07T00:00:00.0000001Z", 1)]
    [InlineData("2026-11-06T00:00:00Z", "2026-11-07T00:00:00Z", 0)]
    [InlineData("2026-11-06T00:00:00Z", "2026-11-07T00:00:00Z", 501)]
    [InlineData("2026-02-30T00:00:00Z", "2026-11-07T00:00:00Z", 1)]
    public void RejectsInvalidFilter(string from, string to, int size)
    {
        var error = Assert.Throws<CollectorIntegrationException>(() => CollectorLogRequestValidator.Validate(Device.ToString("D"),
            new() { FromUtc = from, ToUtc = to, PageSize = size }));
        Assert.Equal(CollectorIntegrationConstant.InvalidRequest, error.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("clock-01")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void RequiresPublishedGuidDeviceId(string id) => Assert.Throws<CollectorIntegrationException>(() =>
        CollectorLogRequestValidator.Validate(id, Request()));

    [Fact]
    public void EquivalentUtcOffsetsHaveTheSameFilterHash()
    {
        var a = CollectorLogRequestValidator.Validate(Device.ToString("D"), Request());
        var b = CollectorLogRequestValidator.Validate(Device.ToString("D"), new() { FromUtc = "2026-11-06T00:00:00.0000000+00:00",
            ToUtc = "2026-11-07T00:00:00+00:00", PageSize = 2 });
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public void AcceptsBothPageBoundsAnd31Days(int size) => Assert.Equal(size,
        CollectorLogRequestValidator.Validate(Device.ToString("D"), new() { FromUtc = "2026-11-06T00:00:00Z",
            ToUtc = "2026-12-07T00:00:00Z", PageSize = size }).PageSize);

    [Theory]
    [InlineData("")]
    [InlineData("a\nb")]
    public void RejectsInvalidTokenText(string token) => Assert.Throws<CollectorIntegrationException>(() =>
        CollectorLogRequestValidator.Validate(Device.ToString("D"), Request(token)));

    [Fact]
    public void RejectsOversizeToken() => Assert.Throws<CollectorIntegrationException>(() =>
        CollectorLogRequestValidator.Validate(Device.ToString("D"), Request(new string('x', 2049))));

    [Fact]
    public void TokenIsProtectedAndRejectsModification()
    {
        var service = new CollectorPageTokenService(new EphemeralDataProtectionProvider());
        var cursor = new CollectorPageCursor(1, Guid.NewGuid(), Device, "hash", Guid.NewGuid(), 5, Now.AddMinutes(15));
        var token = service.Protect(cursor);
        Assert.Equal(cursor, service.Unprotect(token));
        Assert.True(token.Length <= 2048);
        Assert.DoesNotContain(cursor.SnapshotId.ToString(), token);
        Assert.Throws<CollectorIntegrationException>(() => service.Unprotect(token[..30] + "!" + token[31..]));
        Assert.Throws<CollectorIntegrationException>(() => new CollectorPageTokenService(new EphemeralDataProtectionProvider()).Unprotect(token));
    }

    [Fact]
    public async Task CredentialStoresOnlyVerifierAndRetainsLeadingZeroesInPayload()
    {
        var store = new Mock<IIntegrationStore>();
        var service = new IntegrationCredentialService(store.Object, new FixedClock(Now));
        var secret = service.Create(Guid.NewGuid(), null);
        var verifier = service.GetVerifier(secret.ApiKey);
        var key = IntegrationApiKey.Create(secret.KeyId, Guid.NewGuid(), verifier, Now, null);
        store.Setup(x => x.GetKeyAsync(secret.KeyId, It.IsAny<CancellationToken>())).ReturnsAsync(key);
        Assert.Equal(76, secret.ApiKey.Length);
        Assert.Equal(64, verifier.Length);
        Assert.DoesNotContain(secret.ApiKey, verifier);
        Assert.Equal(new IntegrationIdentity(key.ClientId, key.Id), await service.AuthenticateAsync(secret.ApiKey, default));
        Assert.Null(await service.AuthenticateAsync(secret.ApiKey[..^1] + "!", default));
        key.Revoke(Now);
        Assert.Null(await service.AuthenticateAsync(secret.ApiKey, default));
    }

    [Fact]
    public async Task ExpiredAndUnknownCredentialsAreRejected()
    {
        var store = new Mock<IIntegrationStore>();
        var service = new IntegrationCredentialService(store.Object, new FixedClock(Now));
        var secret = service.Create(Guid.NewGuid(), Now);
        store.Setup(x => x.GetKeyAsync(secret.KeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntegrationApiKey.Create(secret.KeyId, Guid.NewGuid(), service.GetVerifier(secret.ApiKey), Now, Now));
        Assert.Null(await service.AuthenticateAsync(secret.ApiKey, default));
        Assert.Null(await service.AuthenticateAsync(service.Create(Guid.NewGuid(), null).ApiKey, default));
        store.Verify(x => x.GetKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DeviceAuthorizationIsRecheckedBeforeReturningRaw()
    {
        var (service, store, reads, _, _) = ReadService();
        store.SetupSequence(x => x.HasAccessAsync(It.IsAny<IntegrationIdentity>(), Device, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true).ReturnsAsync(false);
        var error = await Assert.ThrowsAsync<CollectorIntegrationException>(() => service.ReadAsync(Device.ToString("D"), Request(), default));
        Assert.Equal(403, error.Status);
        reads.Verify(x => x.CreateSnapshotAsync(It.IsAny<Guid>(), It.IsAny<CollectorReadCriteria>(), Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnauthorizedDeviceNeverQueriesAttendance()
    {
        var (service, store, reads, _, _) = ReadService();
        store.Setup(x => x.HasAccessAsync(It.IsAny<IntegrationIdentity>(), Device, Now, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsAsync<CollectorIntegrationException>(() => service.ReadAsync(Device.ToString("D"), Request(), default));
        reads.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("client")]
    [InlineData("device")]
    [InlineData("filter")]
    [InlineData("version")]
    [InlineData("expired")]
    public async Task TokenCannotChangeItsReadBinding(string changed)
    {
        var (service, _, reads, tokens, identity) = ReadService();
        var criteria = CollectorLogRequestValidator.Validate(Device.ToString("D"), Request());
        var cursor = new CollectorPageCursor(changed == "version" ? 2 : 1, changed == "client" ? Guid.NewGuid() : identity.ClientId,
            changed == "device" ? Guid.NewGuid() : Device, changed == "filter" ? "wrong" : criteria.FilterHash,
            Guid.NewGuid(), 2, changed == "expired" ? Now : Now.AddMinutes(15));
        var error = await Assert.ThrowsAsync<CollectorIntegrationException>(() => service.ReadAsync(Device.ToString("D"), Request(tokens.Protect(cursor)), default));
        Assert.Equal(changed == "expired" ? CollectorIntegrationConstant.ExpiredToken : CollectorIntegrationConstant.InvalidToken, error.ErrorCode);
        reads.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadReturnsTheExactSixFieldsAndNextToken()
    {
        var (service, _, _, tokens, identity) = ReadService();
        var page = await service.ReadAsync(Device.ToString("D"), Request(), default);
        Assert.Equal("000123", page.Items[0].DeviceUserId);
        Assert.Equal(DateTimeKind.Unspecified, page.Items[0].OccurredAtLocal.Kind);
        Assert.Equal(identity.ClientId, tokens.Unprotect(page.NextPageToken!).ClientId);
    }

    private static (CollectorAttendanceReadService, Mock<IIntegrationStore>, Mock<ICollectorReadStore>, CollectorPageTokenService, IntegrationIdentity) ReadService()
    {
        var identity = new IntegrationIdentity(Guid.NewGuid(), Guid.NewGuid());
        var context = new Mock<IIntegrationRequestContext>();
        context.SetupGet(x => x.Identity).Returns(identity);
        var store = new Mock<IIntegrationStore>();
        store.Setup(x => x.HasAccessAsync(identity, Device, Now, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var reads = new Mock<ICollectorReadStore>();
        reads.Setup(x => x.CreateSnapshotAsync(identity.ClientId, It.IsAny<CollectorReadCriteria>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CollectorSnapshotPage(Guid.NewGuid(), Now.AddMinutes(15),
                [new("000123", 123, null, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), 1, 0)], 1, true));
        var tokens = new CollectorPageTokenService(new EphemeralDataProtectionProvider());
        return (new(context.Object, store.Object, reads.Object, tokens, new FixedClock(Now)), store, reads, tokens, identity);
    }
}

internal sealed class FixedClock(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(now);
}
