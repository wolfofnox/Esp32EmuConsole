using System.Collections.Generic;

namespace Esp32EmuConsole;

/// <summary>Describes the fixed HTTP response that the mock server should return for a matched rule.</summary>
public record HttpResponse
{
    /// <summary>HTTP status code to return (e.g. 200, 404, 501).</summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Value for the <c>Content-Type</c> response header.
    /// Ignored when <see cref="Headers"/> already contains a <c>Content-Type</c> entry.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>Additional response headers as key/value pairs.</summary>
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>Response body returned as a plain string.</summary>
    public string? Body { get; init; }
}

/// <summary>Describes the WebSocket behavior that the emulator should apply for a matched rule.</summary>
public record WebSocketResponse
{
    /// <summary>
    /// Controls how the server reacts to incoming WebSocket messages.
    /// Supported values: <c>"echo"</c>, <c>"static"</c>, <c>"interval"</c>.
    /// </summary>
    public string? Behavior { get; init; }

    /// <summary>Plain-text payload used by the <c>static</c> and <c>interval</c> behaviors.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// Binary payload as an uppercase hex string (e.g. <c>"48656C6C6F"</c>), used by the
    /// <c>static</c> and <c>interval</c> behaviors. Takes priority over <see cref="Text"/>
    /// when both are present.
    /// </summary>
    public string? Binary { get; init; }

    /// <summary>Push interval in milliseconds; required when <see cref="Behavior"/> is <c>"interval"</c>.</summary>
    public int? IntervalMs { get; init; }
}

/// <summary>Container that holds the optional HTTP and/or WebSocket response definitions for a rule.</summary>
public record RuleResponse
{
    /// <summary>Fixed HTTP response returned when an HTTP request matches this rule.</summary>
    public HttpResponse? Http { get; init; }

    /// <summary>WebSocket behavior applied when a WebSocket connection is opened on the rule's URI.</summary>
    public WebSocketResponse? Ws { get; init; }
}

/// <summary>
/// A single entry in <c>rules.json</c> that maps an HTTP method + URI (or a WebSocket path)
/// to a fixed response or behavior.
/// </summary>
public record Rule
{
    /// <summary>Request path to match (case-insensitive), e.g. <c>"/api/data"</c>.</summary>
    public required string Uri { get; init; }

    /// <summary>HTTP verb to match. Defaults to <c>"GET"</c>.</summary>
    public string Method { get; init; } = "GET";

    /// <summary>
    /// The response or WebSocket behavior to apply.
    /// When <see langword="null"/>, matched HTTP requests return <c>501 Not Implemented</c>.
    /// </summary>
    public RuleResponse? Response { get; init; }
}
