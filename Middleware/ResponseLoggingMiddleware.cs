
namespace Esp32EmuConsole.Middleware;

public class ResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseLoggingMiddleware> _logger;
    private readonly ILogger _loggerHttp;

    public ResponseLoggingMiddleware(RequestDelegate next, ILogger<ResponseLoggingMiddleware> logger, ILoggerFactory loggerFactory)
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