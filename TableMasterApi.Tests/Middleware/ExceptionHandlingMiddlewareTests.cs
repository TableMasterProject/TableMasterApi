using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using TableMasterApi.Middleware;

namespace TableMasterApi.Tests.Middleware
{
    public class ExceptionHandlingMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_PassesThrough_WhenNoExceptionIsThrown()
        {
            var nextInvoked = false;
            var middleware = CreateMiddleware(_ =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            });
            var context = CreateContext();

            await middleware.InvokeAsync(context);

            nextInvoked.Should().BeTrue();
            context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task InvokeAsync_Returns401Json_OnUnauthorizedAccessException()
        {
            var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException("Acces refuse"));
            var context = CreateContext();

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
            context.Response.ContentType.Should().Be("application/json");
            var body = await ReadBody(context);
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("error").GetString().Should().Be("Acces refuse");
        }

        [Fact]
        public async Task InvokeAsync_Returns500Json_AndDoesNotLeakInternalMessage_OnGenericException()
        {
            var middleware = CreateMiddleware(_ => throw new InvalidOperationException("secret-internal-detail"));
            var context = CreateContext();

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
            context.Response.ContentType.Should().Be("application/json");
            var body = await ReadBody(context);
            body.Should().NotContain("secret-internal-detail",
                "le middleware ne doit jamais exposer le message brut d'une exception non maitrisee");
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("error").GetString().Should().Be("Une erreur interne est survenue.");
        }

        [Fact]
        public async Task InvokeAsync_DoesNotMutateResponse_WhenResponseAlreadyStarted()
        {
            var middleware = CreateMiddleware(ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status202Accepted;
                throw new InvalidOperationException("late failure");
            });
            var context = CreateContext(responseAlreadyStarted: true);

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(StatusCodes.Status202Accepted,
                "une fois la reponse demarree, le middleware ne doit pas la reecrire");
            var body = await ReadBody(context);
            body.Should().BeEmpty("le middleware ne doit rien ajouter au corps deja envoye");
        }

        private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
            => new(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        private static DefaultHttpContext CreateContext(bool responseAlreadyStarted = false)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            if (responseAlreadyStarted)
            {
                context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
            }
            return context;
        }

        private sealed class StartedResponseFeature : IHttpResponseFeature
        {
            public int StatusCode { get; set; } = StatusCodes.Status200OK;
            public string? ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get; set; } = Stream.Null;
            public bool HasStarted => true;
            public void OnStarting(Func<object, Task> callback, object state) { }
            public void OnCompleted(Func<object, Task> callback, object state) { }
        }

        private static async Task<string> ReadBody(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
    }
}
