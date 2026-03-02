using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Esp32EmuConsole.Services.WebSocket;

/// <summary>
/// Extension method that registers the WebSocket handler on the ASP.NET Core
/// middleware pipeline. All WebSocket upgrade requests whose path is not <c>"/"</c>
/// (which is reserved for Vite HMR) are accepted and handed to
/// <see cref="WebSocketService.HandleConnectionAsync"/>.
/// </summary>
public static class WSMapExtensions
{
    public static void MapWs(this IApplicationBuilder app, WebSocketService wsService)
    {
        app.UseWhen(
            context => context.WebSockets.IsWebSocketRequest && context.Request.Path.Value != "/",
            builder =>
            {
                builder.Run(async ctx =>
                {
                    var path = ctx.Request.Path.Value ?? "/";
                    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                    await wsService.HandleConnectionAsync(ws, path, ctx.RequestAborted);
                });
            });
    }
}
