using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TableMasterApi.Tests.Integration;

public class SignalRAuthenticationIntegrationTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=tablemaster;Username=test;Password=test;Timeout=1;Command Timeout=1");
            builder.UseSetting("ConfigPerso:SecretKey", "signalr-authentication-test-secret-key-32-bytes");
            builder.UseSetting("ConfigPerso:Issuer", "TableMasterApi.Tests");
            builder.UseSetting("ConfigPerso:Audience", "TableMasterApi.Tests");
            builder.UseSetting("Cors:AllowedOrigins:0", "https://test.tablemaster.invalid");
            builder.UseSetting("Features:RunMigrationsOnStartup", "false");
            builder.UseSetting("Features:RunNotificationWorker", "false");
            builder.UseSetting("Sentry:Dsn", string.Empty);
        });

    [Fact]
    public async Task BrowserQueryTokenAuthenticatesHubNegotiation()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = factory.Services.GetRequiredService<JwtService>().GenerateAccessToken(42);

        using var response = await client.PostAsync(
            $"/reservationHub/negotiate?negotiateVersion=1&access_token={token}",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-token")]
    public async Task MissingOrInvalidQueryTokenCannotNegotiate(string token)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsync(
            $"/reservationHub/negotiate?negotiateVersion=1&access_token={token}",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
