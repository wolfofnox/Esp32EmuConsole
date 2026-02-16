# ESP32 Web Development with Esp32EmuConsole

## Quick Reference for ESP32 Developers

This guide helps ESP32 developers use Esp32EmuConsole to test and debug web applications before deploying to physical hardware.

---

## Table of Contents

1. [Why Use Esp32EmuConsole?](#why-use-esp32emuconsole)
2. [Current Capabilities](#current-capabilities)
3. [Common ESP32 Use Cases](#common-esp32-use-cases)
4. [Workarounds for Missing Features](#workarounds-for-missing-features)
5. [Best Practices](#best-practices)

---

## Why Use Esp32EmuConsole?

### Problems It Solves
- ❌ **Hardware availability**: No need to have ESP32 device connected
- ❌ **Flash cycles**: Instant feedback without uploading firmware
- ❌ **Debugging**: Can't easily debug on-device web interfaces
- ❌ **Iteration speed**: Slow compile-flash-test cycles

### What You Get
- ✅ **Instant feedback**: Change code, see results immediately
- ✅ **Local development**: Full dev tools in browser
- ✅ **Network inspection**: See all HTTP traffic in TUI
- ✅ **Mock responses**: Simulate any API response

---

## Current Capabilities

### ✅ **Fully Supported**

#### 1. REST API Simulation
Mock any ESP32 REST API endpoint:

```json
// rules.json
[
  {
    "uri": "/api/status",
    "method": "GET",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"temperature\":25.5,\"humidity\":60}"
    }
  },
  {
    "uri": "/api/led",
    "method": "POST",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"status\":\"ok\",\"led\":\"on\"}"
    }
  }
]
```

**Supported**:
- All HTTP methods (GET, POST, PUT, DELETE, PATCH)
- Custom status codes
- JSON, HTML, plain text responses
- Custom headers
- Hot-reload (edit rules.json while running)

#### 2. Web Interface Development
Develop HTML/CSS/JS interfaces with Vite:

```javascript
// Your frontend code
fetch('/api/status')
  .then(r => r.json())
  .then(data => {
    console.log('Temperature:', data.temperature);
  });
```

**Supported**:
- Hot module replacement (HMR)
- Modern JavaScript (ES6+)
- TypeScript support
- CSS frameworks (Tailwind, Bootstrap)
- Reverse proxy to Vite dev server

#### 3. Real-Time Monitoring
Terminal UI shows:
- All HTTP requests with status codes
- WebSocket connections and messages
- Application logs
- Separate panels for different log types

### ⚠️ **Partially Supported**

#### WebSocket Communication
**Current Status**: Basic implementation

**What Works**:
- Connect to `/ws` endpoint
- Receive hello message: `{"type":"hello","msg":"Connected"}`
- Send text messages (logged in TUI)

**What Doesn't Work**:
- No automatic echo or response
- No rules-based WebSocket responses
- No binary message support
- No multiple WebSocket endpoints

**Workaround**: Manually add echo logic in code (see [Workarounds](#workarounds-for-missing-features))

### ❌ **Not Supported**

The following ESP32 features are **not currently available**:
- HTTPS/TLS connections
- CORS configuration
- Authentication/authorization
- File upload simulation
- MQTT protocol
- Bluetooth/BLE simulation
- Hardware GPIO simulation
- OTA update simulation

---

## Common ESP32 Use Cases

### Use Case 1: Temperature Sensor Dashboard

**Typical ESP32 Code**:
```cpp
// ESP32 firmware
server.on("/api/sensor", HTTP_GET, []() {
  float temp = readTemperature();
  float hum = readHumidity();
  String json = "{\"temp\":" + String(temp) + ",\"hum\":" + String(hum) + "}";
  server.send(200, "application/json", json);
});
```

**Emulator Setup** (`rules.json`):
```json
[
  {
    "uri": "/api/sensor",
    "method": "GET",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"temp\":22.5,\"hum\":65}"
    }
  }
]
```

**Frontend** (runs in browser):
```javascript
setInterval(async () => {
  const response = await fetch('/api/sensor');
  const data = await response.json();
  document.getElementById('temp').textContent = data.temp + '°C';
  document.getElementById('hum').textContent = data.hum + '%';
}, 1000);
```

### Use Case 2: LED Control Panel

**Typical ESP32 Code**:
```cpp
server.on("/api/led", HTTP_POST, []() {
  String body = server.arg("plain");
  // Parse JSON, set LED state
  digitalWrite(LED_PIN, state ? HIGH : LOW);
  server.send(200, "application/json", "{\"status\":\"ok\"}");
});
```

**Emulator Setup** (`rules.json`):
```json
[
  {
    "uri": "/api/led",
    "method": "POST",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"status\":\"ok\",\"message\":\"LED updated\"}"
    }
  },
  {
    "uri": "/api/led/status",
    "method": "GET",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"led\":\"on\",\"brightness\":80}"
    }
  }
]
```

### Use Case 3: WiFi Configuration Page

**Typical ESP32 Code**:
```cpp
// Serve HTML form
server.on("/", HTTP_GET, []() {
  server.send(200, "text/html", htmlConfigPage);
});

// Handle form submission
server.on("/api/wifi", HTTP_POST, []() {
  String ssid = server.arg("ssid");
  String password = server.arg("password");
  // Save to EEPROM, restart
  server.send(200, "application/json", "{\"status\":\"saved\"}");
});
```

**Emulator Setup** (`rules.json`):
```json
[
  {
    "uri": "/api/wifi",
    "method": "GET",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"ssid\":\"MyNetwork\",\"connected\":true,\"ip\":\"192.168.1.100\"}"
    }
  },
  {
    "uri": "/api/wifi",
    "method": "POST",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"status\":\"saved\",\"restart\":true}"
    }
  },
  {
    "uri": "/api/networks",
    "method": "GET",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "[{\"ssid\":\"Network1\",\"rssi\":-50},{\"ssid\":\"Network2\",\"rssi\":-70}]"
    }
  }
]
```

### Use Case 4: Real-Time WebSocket Data

**Typical ESP32 Code**:
```cpp
// WebSocket handler
void onWebSocketEvent(uint8_t num, WStype_t type, uint8_t * payload, size_t length) {
  if (type == WStype_TEXT) {
    String message = String((char*)payload);
    // Handle command, send response
    webSocket.sendTXT(num, "{\"status\":\"ok\"}");
  }
}
```

**Current Limitation**: No rules-based WebSocket responses yet.

**Workaround**: See [WebSocket Workaround](#websocket-workaround) below.

---

## Workarounds for Missing Features

### WebSocket Workaround

Since rules-based WebSocket responses are not yet implemented, you can modify the code temporarily:

**Option 1: Edit WSMapExtensions.cs**

```csharp
// src/Websocket/WSMapExtensions.cs
// Add after line 22 (hello message):

while (ws.State == WebSocketState.Open)
{
    var receiveResult = await ws.ReceiveAsync(buffer, CancellationToken.None);
    
    if (receiveResult.MessageType == WebSocketMessageType.Close)
        break;
    
    var message = Encoding.UTF8.GetString(buffer.Array!, 0, receiveResult.Count);
    logger.LogInformation($"WS Received: {message}");
    
    // WORKAROUND: Echo back or send custom response
    string response = message; // Or customize based on message
    var bytes = Encoding.UTF8.GetBytes(response);
    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}
```

**Option 2: Use Frontend-Only Testing**

Test WebSocket UI without real backend:

```javascript
// Mock WebSocket for testing
class MockWebSocket {
  constructor(url) {
    setTimeout(() => {
      this.onopen?.();
      // Simulate sensor data
      setInterval(() => {
        this.onmessage?.({ data: JSON.stringify({ temp: 20 + Math.random() * 10 }) });
      }, 1000);
    }, 100);
  }
  
  send(data) {
    console.log('Sent:', data);
    // Echo back
    setTimeout(() => {
      this.onmessage?.({ data });
    }, 100);
  }
}

// Use in development
const ws = DEVELOPMENT ? new MockWebSocket('ws://localhost:5000/ws') : new WebSocket('ws://localhost:5000/ws');
```

### HTTPS Workaround

ESP32 often uses HTTPS for security. To test:

**Option 1**: Disable HTTPS in development:
```javascript
// Use HTTP in development, HTTPS in production
const API_URL = process.env.NODE_ENV === 'production' 
  ? 'https://esp32.local/api'
  : 'http://localhost:5000/api';
```

**Option 2**: Use browser dev tools to override CSP:
- Open DevTools → Application → Service Workers → Bypass for network
- Or use browser extensions to disable HTTPS requirements

### CORS Workaround

If testing with external frontend (not Vite):

**Option 1**: Add CORS middleware manually (temporary):
```csharp
// src/Services/WebServer/WebServer.cs
// Add before line 27 (ResponseLogger):
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE");
    context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
    
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        return;
    }
    
    await next();
});
```

**Option 2**: Use browser with disabled security (development only):
```bash
# Chrome with CORS disabled (UNSAFE - dev only!)
chrome --disable-web-security --user-data-dir=/tmp/chrome-dev
```

---

## Best Practices

### 1. **Organize Rules by Feature**

Instead of one large rules.json, organize by feature areas:

```
project/
├── rules.json           # Load this one
├── rules/
│   ├── sensor.json     # Sensor endpoints
│   ├── led.json        # LED control
│   └── wifi.json       # WiFi config
```

**Load merged rules** (currently requires code change):
```csharp
// Future feature: support rule includes
{
  "includes": ["rules/sensor.json", "rules/led.json"]
}
```

### 2. **Use Realistic Data**

Match ESP32 response formats exactly:

```json
// ESP32 typically uses compact JSON (no whitespace)
{"temp":25.5,"hum":60}

// Not pretty-printed
{
  "temp": 25.5,
  "hum": 60
}
```

### 3. **Test Error Cases**

Don't just test happy paths:

```json
[
  {
    "uri": "/api/sensor",
    "method": "GET",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"temp\":25.5}"
    }
  },
  {
    "uri": "/api/sensor/error",
    "method": "GET",
    "response": {
      "statusCode": 503,
      "contentType": "application/json",
      "body": "{\"error\":\"Sensor not available\"}"
    }
  }
]
```

### 4. **Monitor the TUI**

Watch the Terminal UI for:
- ✅ Expected requests hitting the right endpoints
- ⚠️ 404s for missing rules
- ⚠️ 501s for rules without responses
- ⚠️ Unexpected request patterns

### 5. **Hot-Reload Rules**

Edit `rules.json` while running to iterate quickly:
1. Make changes to rules.json
2. Save file
3. File watcher automatically reloads
4. Refresh browser to test

### 6. **Use HTTP Methods Correctly**

Match ESP32 conventions:
- **GET**: Read data (idempotent)
- **POST**: Create or action (non-idempotent)
- **PUT**: Update (idempotent)
- **DELETE**: Remove (idempotent)

```json
[
  {"uri": "/api/led", "method": "GET"},      // Read state
  {"uri": "/api/led", "method": "POST"},     // Set state
  {"uri": "/api/led/brightness", "method": "PUT"},  // Update brightness
  {"uri": "/api/led", "method": "DELETE"}    // Turn off
]
```

### 7. **Document Your APIs**

Add comments to rules.json (future versions will support):

```json
[
  {
    "// description": "Returns current sensor readings",
    "uri": "/api/sensor",
    "method": "GET",
    "response": {
      "statusCode": 200,
      "contentType": "application/json",
      "body": "{\"temp\":25.5,\"hum\":60}"
    }
  }
]
```

---

## Limitations & Gotchas

### 1. **No State Management**
Rules return static responses. For stateful behavior:

**Problem**: POST to `/api/led` doesn't affect GET response
```json
// These are independent:
{"uri": "/api/led", "method": "POST", "response": {...}}
{"uri": "/api/led", "method": "GET", "response": {...}}
```

**Workaround**: Test state management in frontend only, or manually update rules.json

### 2. **No Request Body Matching**
Cannot return different responses based on POST body content.

**Problem**: Can't simulate "invalid input" errors
```javascript
fetch('/api/led', {
  method: 'POST',
  body: JSON.stringify({ brightness: 150 })  // Over max (100)
});
// Always returns same response, can't check body
```

**Workaround**: Create separate endpoints for error cases:
```json
[
  {"uri": "/api/led/valid", "method": "POST", "response": {...}},
  {"uri": "/api/led/invalid", "method": "POST", "response": {"statusCode": 400, ...}}
]
```

### 3. **No Query Parameters**
Rules don't match query strings.

**Problem**: `/api/led?brightness=50` treated as `/api/led`
```json
// This rule:
{"uri": "/api/led", ...}
// Matches both:
// - /api/led
// - /api/led?brightness=50
// - /api/led?color=red
```

**Workaround**: Use path parameters instead:
```json
{"uri": "/api/led/50", ...}      // Instead of ?brightness=50
{"uri": "/api/led/red", ...}     // Instead of ?color=red
```

### 4. **Windows Only (Currently)**
Application uses Windows-specific code for process management.

**Impact**: Crashes on Linux/macOS
**Status**: Critical bug, fix planned for v0.5
**Workaround**: Use Windows, WSL, or wait for cross-platform fix

---

## Migration to Real ESP32

### Checklist

When moving from emulator to ESP32:

- [ ] **API Endpoints**: Ensure all `/api/*` routes exist in ESP32 code
- [ ] **HTTP Methods**: Match method types (GET, POST, etc.)
- [ ] **Response Format**: Match JSON structure exactly
- [ ] **Status Codes**: Use same status codes (200, 404, 500, etc.)
- [ ] **Headers**: Add any custom headers used in rules
- [ ] **WebSocket Protocol**: Implement same message format
- [ ] **Error Handling**: Add real error handling (not just mock errors)
- [ ] **Authentication**: Add security (not in emulator)
- [ ] **Hardware Integration**: Connect sensors, LEDs, etc.

### Code Comparison

**Emulator Rule**:
```json
{
  "uri": "/api/sensor",
  "method": "GET",
  "response": {
    "statusCode": 200,
    "contentType": "application/json",
    "body": "{\"temp\":25.5,\"hum\":60}"
  }
}
```

**ESP32 Implementation**:
```cpp
server.on("/api/sensor", HTTP_GET, []() {
  float temp = dht.readTemperature();  // Real sensor
  float hum = dht.readHumidity();
  
  String json = "{\"temp\":" + String(temp) + ",\"hum\":" + String(hum) + "}";
  server.send(200, "application/json", json);
});
```

**Key Differences**:
- ESP32 reads from actual hardware
- ESP32 handles errors (sensor failures)
- ESP32 may have authentication
- ESP32 serves HTML from SPIFFS/LittleFS

---

## Getting Help

### Known Issues
See [ROADMAP_TO_V1.md](ROADMAP_TO_V1.md) for:
- Current bugs and limitations
- Planned features
- Workarounds

### Feature Requests
If you need a feature for ESP32 development:
1. Check roadmap first
2. Open GitHub issue with:
   - ESP32 use case description
   - Example code from ESP32
   - Desired emulator behavior

### Debug Tips

**Problem**: Rule not matching
- ✅ Check URI path starts with `/`
- ✅ Verify method is uppercase in rules.json
- ✅ Watch TUI for 404s
- ✅ Check for typos in path

**Problem**: Response not showing
- ✅ Ensure `response` object exists in rule
- ✅ Check `statusCode` is present
- ✅ Verify browser dev tools → Network tab
- ✅ Look for 501 status (rule exists but no response)

**Problem**: Vite not starting
- ✅ Run `npm install` first
- ✅ Check port 5173 is not in use
- ✅ Ensure `package.json` exists
- ✅ Check TUI for Vite startup errors

**Problem**: WebSocket not connecting
- ✅ Use correct URL: `ws://localhost:5000/ws`
- ✅ Check browser console for errors
- ✅ Verify WebSocket traffic in TUI
- ✅ Ensure protocol is `ws://` not `wss://`

---

## Example Project Structure

```
my-esp32-project/
├── firmware/               # ESP32 Arduino code
│   └── main.ino
├── web/                   # Frontend code
│   ├── index.html
│   ├── style.css
│   └── app.js
├── rules.json            # Emulator API rules
├── vite.config.js        # Vite configuration
├── package.json          # Frontend dependencies
└── README.md            # Project docs
```

**Development Workflow**:
1. Start emulator: `dotnet run --project /path/to/Esp32EmuConsole/src`
2. Edit `web/` files → auto-reload via Vite
3. Edit `rules.json` → auto-reload API responses
4. Test in browser: `http://localhost:5000`
5. Port to ESP32: Copy logic from `web/` and `rules.json`

---

## Next Steps

1. ✅ **Review** this guide
2. ✅ **Check** [ROADMAP_TO_V1.md](ROADMAP_TO_V1.md) for upcoming features
3. ✅ **Start** developing with current capabilities
4. ✅ **Report** issues and feature requests
5. ✅ **Contribute** to the project (see CONTRIBUTING.md when available)

---

*Document Version*: 1.0  
*Created*: 2026-02-16  
*Target Audience*: ESP32 Developers  
*Prerequisite Knowledge*: Basic ESP32 programming, web development
