using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TableMasterApi.Tests.Services
{
    public class BrevoEmailServiceTests
    {
        [Fact]
        public async Task SendEmailAsync_ShouldCallBrevoTransactionalEmailEndpoint()
        {
            var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.brevo.com/v3/")
            };
            var service = new BrevoEmailService(
                httpClient,
                Options.Create(new BrevoOptions
                {
                    ApiKey = "test-api-key",
                    SenderEmail = "noreply@example.com",
                    SenderName = "TableMaster"
                }),
                NullLogger<BrevoEmailService>.Instance);

            var sent = await service.SendEmailAsync(new EmailMessage
            {
                ToEmail = "client@example.com",
                ToName = "Client",
                Subject = "Reservation",
                HtmlContent = "<p>Confirmee</p>",
                TextContent = "Confirmee"
            });

            sent.Should().BeTrue();
            handler.Request.Should().NotBeNull();
            handler.Request!.Method.Should().Be(HttpMethod.Post);
            handler.Request.RequestUri!.ToString().Should().Be("https://api.brevo.com/v3/smtp/email");
            handler.Request.Headers.GetValues("api-key").Should().ContainSingle("test-api-key");
            handler.Body.Should().Contain("\"sender\"");
            handler.Body.Should().Contain("\"to\"");
            handler.Body.Should().Contain("\"htmlContent\"");
        }

        private sealed class CapturingHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

            public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            public HttpRequestMessage? Request { get; private set; }
            public string Body { get; private set; } = string.Empty;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                Body = request.Content == null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                return _responseFactory(request);
            }
        }
    }
}
