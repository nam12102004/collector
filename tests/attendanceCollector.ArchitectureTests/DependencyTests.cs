using attendanceCollector.Application.Services;
using attendanceCollector.Domain.Entities;

namespace attendanceCollector.ArchitectureTests;

public sealed class DependencyTests
{
    [Fact]
    public void DomainAndApplicationRemainIndependentOfWebAndPersistence()
    {
        foreach (var assembly in new[] { typeof(AttendanceLog).Assembly, typeof(CollectorAttendanceReadService).Assembly })
        {
            var forbidden = assembly.GetReferencedAssemblies().Where(x => x.Name!.StartsWith("Microsoft.AspNetCore")
                || x.Name.StartsWith("Microsoft.EntityFrameworkCore") || x.Name.StartsWith("Npgsql") || x.Name.EndsWith(".Infrastructure"));
            Assert.Empty(forbidden);
        }
    }
}
