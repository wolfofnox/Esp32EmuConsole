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
    public required string Uri { get; init; }
    public required string Method { get; init; }
    public FixedResponse? Response { get; init; }
}
