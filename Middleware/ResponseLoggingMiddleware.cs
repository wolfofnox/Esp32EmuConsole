using Microsoft.AspNetCore.Http;

namespace Esp32EmuConsole;

public class ResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public ResponseLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        await _next(ctx);
        Console.WriteLine($"{ctx.Request.Method} {ctx.Request.Path} -> {ctx.Response.StatusCode}");
    }
}