using Microsoft.Extensions.Diagnostics.HealthChecks;
using TableMasterApi.Model;

namespace TableMasterApi.Tests.Services
{
    public class PostgresHealthCheckTests
    {
        [Fact]
        public async Task CheckHealthAsync_ShouldReturnUnhealthy_WhenConnectionCannotBeOpened()
        {
            var healthCheck = new PostgresHealthCheck(new ConfigPerso
            {
                ConnectionString = "Host=localhost;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1"
            });

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Be("PostgreSQL indisponible.");
            result.Exception.Should().NotBeNull();
        }
    }
}
