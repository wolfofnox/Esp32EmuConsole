using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Esp32EmuConsole.Services.WebSocket;

public static class WSMapExtensions
{
    public static void MapWs(this IApplicationBuilder app, WebSocketService wsService)
    {
        app.UseWhen(
            context => context.WebSockets.IsWebSocketRequest,
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
