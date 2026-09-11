using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TableMasterApi.Middleware;

public sealed class ApiErrorResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { StatusCode: >= 400 } result)
        {
            var message = result.Value switch
            {
                string text => text,
                ValidationProblemDetails details => string.Join(" ", details.Errors.Values.SelectMany(x => x)),
                ProblemDetails details => details.Detail ?? details.Title ?? "Requête impossible.",
                _ => "Requête impossible."
            };
            result.Value = new { error = message, traceId = context.HttpContext.TraceIdentifier };
            result.DeclaredType = null;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
