using System.Collections.Generic;

namespace Esp32EmuConsole;

public record FixedResponse
{
    public required int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }
}

public record Rule
{
    public string? Uri { get; init; }
    public string? Method { get; init; }
    public FixedResponse? Response { get; init; }
    public string? Type { get; init; }
    public string? Path { get; init; }
    public string? Behavior { get; init; }
    public string? WebSocketResponse { get; init; }
}
