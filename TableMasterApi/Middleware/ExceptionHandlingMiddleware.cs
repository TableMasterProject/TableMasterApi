using System.Net;
using Npgsql;
using System.Text.Json;

namespace TableMasterApi.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (UnauthorizedAccessException exception)
            {
                await WriteErrorAsync(context, HttpStatusCode.Unauthorized, exception.Message);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await WriteErrorAsync(context, HttpStatusCode.Conflict, "Une ressource identique existe déjà.");
            }
            catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation)
            {
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, "Les données ne respectent pas les contraintes de cette ressource.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Erreur non geree pendant le traitement HTTP.");
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "Une erreur interne est survenue.");
            }
        }

        private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string message)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message, traceId = context.TraceIdentifier }));
        }
    }
}
