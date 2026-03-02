# Esp32EmuConsole

A **Windows-only** localhost development tool that emulates an ESP32 device to let you test web applications without physical hardware.

It combines an HTTP mock server, a WebSocket emulator, an embedded Vite dev server (with reverse proxy), and a real-time Terminal UI (TUI) — all in a single .NET executable.

---

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [How It Works](#how-it-works)
- [Configuring Rules (`rules.json`)](#configuring-rules-rulesjson)
  - [HTTP Rules](#http-rules)
  - [WebSocket Rules](#websocket-rules)
  - [Supported WebSocket Behaviors](#supported-websocket-behaviors)
- [Terminal UI](#terminal-ui)
- [Configuration Reference](#configuration-reference)

---

## Features

| Feature | Description |
|---|---|
| **HTTP Mock Server** | Rules-based API mocking with custom responses, headers, and status codes |
| **WebSocket Emulator** | Echo, static, and interval-based WebSocket response behaviors |
| **Vite Integration** | Automatic Vite dev server management with reverse proxy for all unmatched requests |
| **Live Rules Reload** | Edit `rules.json` while the app is running; changes are picked up automatically |
| **Terminal UI** | Real-time log monitoring with separate panels for App, HTTP, and WebSocket traffic |
| **Log Filtering** | Search and filter logs by text or minimum log level |
| **In-Memory Logging** | Pattern-based log routing with circular buffers (2,000 lines per channel) |

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) (LTS recommended) and **npm** — required to run the embedded Vite dev server

---

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/wolfofnox/Esp32EmuConsole.git
   cd Esp32EmuConsole
   ```

2. **Run the application**

   ```bash
   dotnet run --project src
   ```

   On first launch, the app copies template files (`rules.json`, `vite.config.js`, `package.json`, `index.html`) into the **current working directory** if they do not already exist.

3. **Install front-end dependencies** (once, in the same directory)

   ```bash
   npm install
   ```

   > The Vite dev server is started automatically by Esp32EmuConsole, so you do not need to run `npm run dev` manually.

4. **Open your browser**

   Navigate to `http://localhost:5000`. Static API endpoints are served by the mock server; everything else is proxied to Vite at `http://localhost:5173`.

---

## How It Works

```
Browser / ESP32 client
        │
        ▼
┌───────────────────────┐
│   Esp32EmuConsole     │  localhost:5000
│  ┌─────────────────┐  │
│  │ ResponseLogger  │  │  Logs every request/response
│  │   Middleware    │  │
│  └────────┬────────┘  │
│           │           │
│  ┌────────▼────────┐  │
│  │ StaticResponse  │  │  Matches rules.json → returns fixed response
│  │   Middleware    │  │
│  └────────┬────────┘  │
│           │ (no match)│
│  ┌────────▼────────┐  │
│  │  WebSocket Hub  │  │  Handles WS connections for non-"/" paths
│  └────────┬────────┘  │
│           │ (no match)│
│  ┌────────▼────────┐  │
│  │  YARP Reverse   │  │  Proxies remaining requests to Vite
│  │     Proxy       │  │
│  └─────────────────┘  │
└───────────────────────┘
```

---

## Configuring Rules (`rules.json`)

`rules.json` lives in the working directory from which you run Esp32EmuConsole.
A default template is created automatically on first run.

The file is a JSON array of **rule objects**. It is watched for changes at runtime — saving the file reloads all rules immediately without restarting the application.

> **Tip:** The JSON parser accepts `// line comments` and `/* block comments */` as well as trailing commas, so you can annotate your rules file freely.

### HTTP Rules

```json
[
  {
    // Required. The request path this rule matches (case-insensitive).
    "uri": "/api/hello",

    // Optional. HTTP method to match. Defaults to "GET".
    // Supported values: "GET", "POST", "PUT", "DELETE", "PATCH", or any custom verb.
    "method": "GET",

    "response": {
      "http": {
        // Required. The HTTP status code to return (e.g., 200, 404, 501).
        "statusCode": 200,

        // Optional. Sets the Content-Type response header.
        // Ignored if the same header is also listed under "headers".
        "contentType": "application/json",

        // Optional. Additional response headers as key/value pairs.
        "headers": {
          "Cache-Control": "no-cache",
          "X-Custom-Header": "value"
        },

        // Optional. The response body as a plain string.
        "body": "{\"status\":\"ok\"}"
      }
    }
  }
]
```

**Stub-only rule (no response body)**

Omit the `response` field entirely to register an endpoint that returns `501 Not Implemented`.
This is useful as a placeholder while you are building out the backend.

```json
{
  "uri": "/api/not-ready-yet",
  "method": "POST"
}
```

### WebSocket Rules

WebSocket rules use the `response.ws` object instead of (or in addition to) `response.http`.
The `uri` field is the WebSocket path (e.g., `/ws/sensor`).

> **Note:** The path `"/"` is reserved for Vite Hot Module Replacement and cannot be used for WebSocket rules.

```json
{
  "uri": "/ws/example",
  "response": {
    "ws": {
      // Required. One of: "echo", "static", "interval". See below.
      "behavior": "static",

      // Optional (used by "static" and "interval" behaviors).
      // Plain text payload to send.
      "text": "{\"temp\":25.5,\"humidity\":60}",

      // Optional. Binary payload as an uppercase hex string (e.g., "48656C6C6F").
      // When both "text" and "binary" are present, "binary" takes priority.
      "binary": null,

      // Required for "interval" behavior.
      // How often (in milliseconds) to push the payload to the client.
      "intervalMs": 1000
    }
  }
}
```

### Supported WebSocket Behaviors

| `behavior` | Description |
|---|---|
| `echo` | Reflects every incoming message back to the sender (same type: text or binary). |
| `static` | Sends a fixed `text` or `binary` payload in response to each incoming message. |
| `interval` | Pushes a `text` or `binary` payload to the client every `intervalMs` milliseconds, regardless of incoming messages. |

---

## Terminal UI

The TUI is automatically launched when the application starts.

| Panel | Description |
|---|---|
| **App Logs** | General application log messages |
| **HTTP Logs** | One line per HTTP request/response |
| **WebSocket Logs** | WebSocket connection events and message traffic |
| **Connected Clients** | *(Planned)* |
| **Stats** | Server URL, Vite URL, start time, and live uptime |

### Keyboard Shortcuts

Use **Alt** to activate the menu bar.

| Menu | Action |
|---|---|
| **File → Quit** | Stop the server and exit |
| **View → Logs / HTTP / WebSocket / Clients / Stats** | Toggle individual panels on/off |
| **View → Clear logs** | Flush all in-memory log buffers |
| **Search → Search / Filter Logs** | Filter log output by text and/or minimum log level |
| **Search → Clear Filter** | Remove active log filter |

---

## Configuration Reference

The following settings are currently hard-coded in the source and can be changed by editing [`src/Services/WebServer/Configuration.cs`](src/Services/WebServer/Configuration.cs):

| Property | Default | Description |
|---|---|---|
| `port` | `5000` | Port the mock server listens on |
| `vitePort` | `5173` | Port the embedded Vite dev server uses |

TUI panel visibility defaults are in [`src/Tui/Configuration.cs`](src/Tui/Configuration.cs):

| Property | Default | Description |
|---|---|---|
| `showAppLogs` | `false` | Show the App Logs panel on startup |
| `showHttpTraffic` | `true` | Show the HTTP Logs panel on startup |
| `showWebSocketTraffic` | `true` | Show the WebSocket Logs panel on startup |
| `showClients` | `false` | Show the Connected Clients panel on startup |
| `showStats` | `false` | Show the Stats panel on startup |
