using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TableMasterApi.Service;

namespace TableMasterApi.Tests.Services;

public class HealthCheckResponseWriterTests
{
    [Fact]
    public async Task WriteAsync_ShouldReturnVersionedJsonResponse()
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APP_VERSION"] = "1.0.2+test"
                })
                .Build())
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["postgres"] = new(
                    HealthStatus.Healthy,
                    "PostgreSQL disponible.",
                    TimeSpan.FromMilliseconds(12),
                    null,
                    null)
            },
            TimeSpan.FromMilliseconds(12));

        await HealthCheckResponseWriter.WriteAsync(context, report);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var root = document.RootElement;

        context.Response.ContentType.Should().Be("application/json; charset=utf-8");
        root.GetProperty("status").GetString().Should().Be("Healthy");
        root.GetProperty("version").GetString().Should().Be("1.0.2+test");
        root.GetProperty("checks")[0].GetProperty("name").GetString().Should().Be("postgres");
    }
}
