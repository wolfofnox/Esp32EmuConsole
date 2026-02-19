using System.Collections.Generic;

namespace Esp32EmuConsole;

public record HttpResponse
{
    public required int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }
}

public record WebSocketResponse
{
    public string? Behavior { get; init; }
    public string? Text { get; init; }
    public string? Binary { get; init; }
    public int? IntervalMs { get; init; }
}

public record RuleResponse
{
    public HttpResponse? Http { get; init; }
    public WebSocketResponse? Ws { get; init; }
}

public record Rule
{
    public required string Uri { get; init; }
    public string? Method { get; init; }
    public RuleResponse? Response { get; init; }
}
