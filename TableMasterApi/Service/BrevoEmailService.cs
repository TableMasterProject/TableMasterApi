using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public class BrevoEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly BrevoOptions _options;
        private readonly ILogger<BrevoEmailService> _logger;

        public BrevoEmailService(HttpClient httpClient, IOptions<BrevoOptions> options, ILogger<BrevoEmailService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Envoi email desactive par configuration.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.SenderEmail))
            {
                _logger.LogWarning("Configuration Brevo incomplete. Configurez Brevo:ApiKey et Brevo:SenderEmail.");
                return false;
            }

            var payload = new BrevoSendEmailRequest(
                new BrevoSender(_options.SenderEmail, _options.SenderName),
                [new BrevoRecipient(message.ToEmail, message.ToName)],
                message.Subject,
                message.HtmlContent,
                message.TextContent);

            using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
            {
                Content = JsonContent.Create(payload)
            };

            request.Headers.Add("api-key", _options.ApiKey);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Brevo a refuse l'email avec le statut {StatusCode}: {Body}", response.StatusCode, responseBody);
            return false;
        }

        private sealed record BrevoSendEmailRequest(
            [property: JsonPropertyName("sender")] BrevoSender Sender,
            [property: JsonPropertyName("to")] IReadOnlyCollection<BrevoRecipient> To,
            [property: JsonPropertyName("subject")] string Subject,
            [property: JsonPropertyName("htmlContent")] string HtmlContent,
            [property: JsonPropertyName("textContent")] string TextContent);

        private sealed record BrevoSender(
            [property: JsonPropertyName("email")] string Email,
            [property: JsonPropertyName("name")] string? Name);

        private sealed record BrevoRecipient(
            [property: JsonPropertyName("email")] string Email,
            [property: JsonPropertyName("name")] string? Name);
    }
}
