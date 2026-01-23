using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Esp32EmuConsole;

public static class WSMapExtensions
{
    public static void MapWs(this IApplicationBuilder app)
    {
        app.Map("/ws", builder =>
        {
            builder.Run(async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400; return;
                }

                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                var hello = System.Text.Json.JsonSerializer.Serialize(new { type = "hello", msg = "Connected" });
                var helloBytes = System.Text.Encoding.UTF8.GetBytes(hello);
                await ws.SendAsync(helloBytes, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);

                var buffer = new byte[4096];
                while (ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                        break;

                    var msg = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received WS message: {msg}");
                }
            });
        });
    }
}
