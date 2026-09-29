using System.Net;

namespace NetWatch.API.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            var status = ex switch
            {
                KeyNotFoundException => HttpStatusCode.NotFound,
                ArgumentException => HttpStatusCode.BadRequest,
                InvalidOperationException => HttpStatusCode.Conflict,
                _ => HttpStatusCode.InternalServerError
            };
            if (status == HttpStatusCode.InternalServerError) logger.LogError(ex, "Unhandled API error");
            else logger.LogWarning(ex, "Handled API error: {Message}", ex.Message);
            context.Response.StatusCode = (int)status;
            var message = status == HttpStatusCode.InternalServerError
                ? "Ocurrió un error interno al procesar la solicitud."
                : ex.Message;
            await context.Response.WriteAsJsonAsync(new { error = message });
        }
    }
}
