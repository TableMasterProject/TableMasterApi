using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TableMasterApi.Tests.Integration;

public class HealthEndpointsIntegrationTests
{
    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=tablemaster;Username=test;Password=test;Timeout=1;Command Timeout=1");
            builder.UseSetting("ConfigPerso:SecretKey", "health-endpoints-test-secret-key-32-bytes");
            builder.UseSetting("ConfigPerso:Issuer", "TableMasterApi.Tests");
            builder.UseSetting("ConfigPerso:Audience", "TableMasterApi.Tests");
            builder.UseSetting("Cors:AllowedOrigins:0", "https://test.tablemaster.invalid");
            builder.UseSetting("Features:RunMigrationsOnStartup", "false");
            builder.UseSetting("APP_VERSION", "1.0.2+integration-test");
            builder.UseSetting("Sentry:Dsn", string.Empty);
            builder.ConfigureTestServices(services =>
            {
                services.AddHealthChecks().AddCheck(
                    "forced-unhealthy",
                    () => HealthCheckResult.Unhealthy("Indisponibilité PostgreSQL simulée."),
                    tags: ["ready"]);
            });
        });
    }

    [Fact]
    public async Task Health_ShouldStayHealthy_WhenPostgresIsUnavailable()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        document.RootElement.GetProperty("version").GetString().Should().Be("1.0.2+integration-test");
        document.RootElement.GetProperty("checks").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Ready_ShouldReturnServiceUnavailable_WhenPostgresIsUnavailable()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready");
        var responseContent = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseContent);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, responseContent);
        document.RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
        document.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Any(check => check.GetProperty("name").GetString() == "forced-unhealthy"
                && check.GetProperty("status").GetString() == "Unhealthy")
            .Should()
            .BeTrue();
    }
}
