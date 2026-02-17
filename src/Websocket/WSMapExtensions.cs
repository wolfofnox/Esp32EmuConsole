using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Esp32EmuConsole.Services;

namespace Esp32EmuConsole;

public static class WSMapExtensions
{
    public static void MapWs(this IApplicationBuilder app, WebSocketService wsService)
    {
        app.Map("/ws", builder =>
        {
            builder.Run(async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400; 
                    return;
                }

                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                await wsService.HandleConnectionAsync(ws, ctx.Request.Path.Value ?? "/ws", ctx.RequestAborted);
            });
        });
    }
}
