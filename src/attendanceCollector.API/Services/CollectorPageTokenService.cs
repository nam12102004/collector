using System.Security.Cryptography;
using System.Text.Json;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace attendanceCollector.API.Services;

public sealed class CollectorPageTokenService(IDataProtectionProvider provider) : ICollectorPageTokenService
{
    private readonly IDataProtector protector = provider.CreateProtector("attendanceCollector.HrmAttendancePages.v1");
    public string Protect(CollectorPageCursor cursor) => protector.Protect(JsonSerializer.Serialize(cursor));
    public CollectorPageCursor Unprotect(string token)
    {
        if (token.Length is 0 or > CollectorIntegrationConstant.MaximumTokenLength || token.Any(char.IsControl)) throw Invalid();
        try
        {
            return JsonSerializer.Deserialize<CollectorPageCursor>(protector.Unprotect(token)) ?? throw Invalid();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException or ArgumentException)
        {
            throw Invalid();
        }
    }
    private static CollectorIntegrationException Invalid() => new(400, CollectorIntegrationConstant.InvalidToken, "The page token is invalid.");
}
