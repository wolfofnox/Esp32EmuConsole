
namespace Esp32EmuConsole.Middleware;

/// <summary>
/// ASP.NET Core middleware that intercepts incoming HTTP requests and returns a
/// pre-configured static response when the request URI and method match a rule
/// defined in <c>rules.json</c>.
/// Requests that do not match any rule are forwarded to the next middleware in
/// the pipeline (typically the YARP reverse proxy targeting Vite).
/// </summary>
public class StaticResponse
{
    private readonly RequestDelegate _next;
    private readonly Services.IRules _ruleService;
    private readonly ILogger<StaticResponse> _logger;

    public StaticResponse(RequestDelegate next, Services.IRules ruleService, ILogger<StaticResponse> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (_ruleService.TryGetHttpResponse(ctx.Request.Method, ctx.Request.Path.ToString(), out var resp))
        {
            if (resp == null)
            {
                ctx.Response.StatusCode = 501;
                await ctx.Response.WriteAsync("No response defined for this endpoint.");
                _logger.LogWarning("No response defined for endpoint {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
                return;
            }

            ctx.Response.StatusCode = resp.StatusCode;
            var hasCtHeader = resp.Headers is not null && resp.Headers.ContainsKey("Content-Type");
            if (!hasCtHeader && !string.IsNullOrWhiteSpace(resp.ContentType))
                ctx.Response.ContentType = resp.ContentType!;

            if (resp.Headers is not null)
            {
                foreach (var kv in resp.Headers)
                    ctx.Response.Headers[kv.Key] = kv.Value;
            }

            await ctx.Response.WriteAsync(resp.Body ?? string.Empty);
            return;
        }

        await _next(ctx);
    }
}
