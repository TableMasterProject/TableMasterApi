using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TableMasterApi.Service;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var configuredVersion = configuration["APP_VERSION"];
        var assemblyVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTimeOffset.UtcNow,
            version = string.IsNullOrWhiteSpace(configuredVersion)
                ? assemblyVersion ?? "unknown"
                : configuredVersion,
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2)
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
