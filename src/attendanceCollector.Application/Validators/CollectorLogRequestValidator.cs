using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;

namespace attendanceCollector.Application.Validators;

public static partial class CollectorLogRequestValidator
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|\+00:00)$", RegexOptions.CultureInvariant)]
    private static partial Regex UtcPattern();

    public static bool TryParseUtc(string? value, out DateTime utc)
    {
        utc = default;
        if (value is null || value.Length > 40 || !UtcPattern().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || parsed.Offset != TimeSpan.Zero || parsed == default) return false;
        utc = parsed.UtcDateTime;
        return true;
    }

    public static CollectorReadCriteria Validate(string deviceId, CollectorLogRequest request)
    {
        if (!Guid.TryParseExact(deviceId, "D", out var id) || id == Guid.Empty
            || !TryParseUtc(request.FromUtc, out var from) || !TryParseUtc(request.ToUtc, out var to)
            || to <= from || to - from > TimeSpan.FromDays(CollectorIntegrationConstant.MaximumRangeDays)
            || request.PageSize is < 1 or > CollectorIntegrationConstant.MaximumPageSize
            || request.PageToken is { } token && (token.Length is 0 or > CollectorIntegrationConstant.MaximumTokenLength || token.Any(char.IsControl)))
            throw new CollectorIntegrationException(400, CollectorIntegrationConstant.InvalidRequest, "Request values are invalid.");

        var canonical = FormattableString.Invariant($"{id:D}|{from.Ticks}|{to.Ticks}|{request.PageSize}");
        return new(id, from, to, request.PageSize, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    }
}
