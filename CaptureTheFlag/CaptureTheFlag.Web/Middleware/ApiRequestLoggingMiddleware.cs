using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CaptureTheFlag.Web.Middleware;

/// <summary>
/// One structured log when an <c>/api/*</c> request starts and one when it completes (status + duration).
/// Correlates with handler logs via <see cref="HttpContext.TraceIdentifier"/>.
/// </summary>
public static class ApiRequestLoggingMiddleware
{
    public static IApplicationBuilder UseApiRequestLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("CaptureTheFlag.Web.Http.Api");

            var sw = Stopwatch.StartNew();
            var traceId = context.TraceIdentifier;
            var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;

            logger.LogInformation(
                "ApiRequest start {Method} {Path}{Query} TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                query,
                traceId);

            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                sw.Stop();
                logger.LogInformation(
                    "ApiRequest end {Method} {Path} Status={StatusCode} ElapsedMs={ElapsedMs:F1} TraceId={TraceId}",
                    context.Request.Method,
                    context.Request.Path.Value ?? string.Empty,
                    context.Response.StatusCode,
                    sw.Elapsed.TotalMilliseconds,
                    traceId);
            }
        });
    }
}
