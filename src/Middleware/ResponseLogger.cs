
namespace Esp32EmuConsole.Middleware;

/// <summary>
/// ASP.NET Core middleware that logs each HTTP request and the status code of its
/// response to the in-memory HTTP log buffer (category: <c>"HTTP"</c>).
/// The log line is emitted <em>after</em> the rest of the pipeline has processed
/// the request so that the final status code is available.
/// </summary>
public class ResponseLogger
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseLogger> _logger;
    private readonly ILogger _loggerHttp;

    public ResponseLogger(RequestDelegate next, ILogger<ResponseLogger> logger, ILoggerFactory loggerFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));
        _loggerHttp = loggerFactory.CreateLogger("HTTP");
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        await _next(ctx);
        _loggerHttp.LogInformation("{Method} {Path} -> {StatusCode}", ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode);
    }
}