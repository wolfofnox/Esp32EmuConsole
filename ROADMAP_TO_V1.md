# Esp32EmuConsole - Roadmap to v1.0

## Executive Summary

Esp32EmuConsole is an ESP32 device emulator with Terminal UI for Windows that enables developers to test and debug web applications for ESP32 devices without physical hardware. This roadmap outlines a focused one-month path to v1.0, prioritizing critical bug fixes and essential features.

**Current State**: Alpha - Core functionality works but has critical bugs and missing documentation.

**Target**: v1.0 - Stable Windows tool with complete documentation and essential features for ESP32 web development.

**Timeline**: 4 weeks

---

## Table of Contents

1. [Existing Features & Current State](#1-existing-features--current-state)
2. [Critical Issues](#2-critical-issues)
3. [Roadmap to v1.0](#3-roadmap-to-v10)
4. [Implementation Notes](#4-implementation-notes)

---

## 1. Existing Features & Current State

### ✅ **Working Features**

| Feature | Status | Description |
|---------|--------|-------------|
| **HTTP Mock Server** | ✅ Fully Functional | Rules-based API mocking with custom responses, headers, status codes |
| **Vite Integration** | ✅ Fully Functional | Automatic Vite dev server management with reverse proxy |
| **Terminal UI** | ✅ Fully Functional | Real-time log monitoring with separate panels for App/HTTP/WebSocket |
| **In-Memory Logging** | ✅ Fully Functional | Pattern-based log routing with circular buffers (2000 lines) |
| **Response Middleware** | ✅ Fully Functional | Request logging and static response handling |
| **HTTP Methods** | ✅ Fully Functional | Support for GET, POST, PUT, DELETE, PATCH, and custom methods |
| **Content Types** | ✅ Fully Functional | JSON, HTML, text, and custom MIME types |

### ⚠️ **Needs Improvement**

| Feature | Status | Issues |
|---------|--------|--------|
| **Rules Hot-Reload** | ⚠️ Unsafe | File watcher for rules.json changes (has race condition) |
| **WebSocket Support** | ⚠️ Basic | Only sends hello message; no echo or rules support |
| **Configuration** | ⚠️ Limited | Hard-coded defaults in code |
| **Error Handling** | ⚠️ Poor | Empty catch blocks suppress exceptions without logging |

### ❌ **Missing**

- **Documentation**: No README, API docs, or getting started guide
- **WebSocket Rules**: No rules-based WebSocket responses
- **Configuration**: No appsettings.json integration for common settings

---

## 2. Critical Issues

### 🔴 **CB-1: Race Condition in Rules Hot-Reload**
- **Severity**: High
- **Impact**: Concurrent access to RuleList/RuleMap without synchronization
- **Affected Code**: `src/Services/Rules.cs` (line 30, LoadRules method)
- **User Impact**: Possible InvalidOperationException or stale data during reload
- **Fix Effort**: 1-2 days (add ReaderWriterLockSlim or immutable collections)

### 🔴 **CB-2: Resource Leaks**
- **Severity**: High
- **Impact**: Socket exhaustion, file handle leaks
- **Affected Code**:
  - `src/Services/Vite.cs` (line 191): HttpClient not disposed
  - `src/Program.cs` (line 24): StreamWriter not explicitly disposed
- **User Impact**: Memory/socket exhaustion over time, data loss on crash
- **Fix Effort**: 1 day (use static HttpClient, dispose StreamWriter)

### 🔴 **CB-3: Silent Exception Suppression**
- **Severity**: High
- **Impact**: Errors hidden without logging, debugging impossible
- **Affected Code**: 10+ empty catch blocks across Vite.cs, Rules.cs, InMemoryLoggerProvider.cs
- **User Impact**: Failed operations go unnoticed, difficult troubleshooting
- **Fix Effort**: 1-2 days (add logging to all catch blocks)

### 🟡 **HP-1: Missing Documentation**
- **Impact**: Steep learning curve, poor adoption
- **Gaps**: No README, API docs, rules.json schema documentation
- **Fix Effort**: 3-4 days

### 🟡 **HP-2: Basic Configuration**
- **Impact**: Hard-coded values make it inflexible
- **Gaps**: Ports, file paths hard-coded in code
- **Fix Effort**: 2-3 days

### 🟡 **HP-3: Basic WebSocket Support**
- **Impact**: Can't test bidirectional ESP32 communication properly
- **Gaps**: No message echo, no rules-based responses
- **Fix Effort**: 3-4 days

---

## 3. Roadmap to v1.0

**Total Timeline**: 4 weeks (20 working days)

### **Week 1: Critical Bug Fixes** (5 days)

#### Day 1-2: Fix Race Condition (CB-1)
- [ ] Add ReaderWriterLockSlim to Rules.cs
- [ ] Wrap LoadRules() in write lock
- [ ] Wrap StaticResponse middleware reads in read lock
- [ ] Add simple unit tests for concurrent access
- [ ] Test hot-reload with multiple rapid changes

#### Day 3: Fix Resource Leaks (CB-2)
- [ ] Make HttpClient static in Vite.cs
- [ ] Ensure StreamWriter disposal in Program.cs
- [ ] Review all IDisposable usage
- [ ] Test for memory leaks during long runs

#### Day 4-5: Add Exception Logging (CB-3)
- [ ] Replace all empty catch blocks with logging
- [ ] Use structured logging with exception objects
- [ ] Test error scenarios to verify logging works
- [ ] Document exception handling approach

---

### **Week 2: Documentation** (5 days)

#### Day 6-7: Write README.md
- [ ] Project overview and purpose
- [ ] Quick start guide (installation, first run)
- [ ] Basic usage examples
- [ ] Configuration instructions
- [ ] Troubleshooting section

#### Day 8-9: Rules.json Documentation
- [ ] Create rules schema documentation
- [ ] Document all fields (uri, method, response, etc.)
- [ ] Provide multiple examples (GET, POST, different content types)
- [ ] Document hot-reload behavior
- [ ] Add rules.schema.json for IDE autocomplete

#### Day 10: Additional Documentation
- [ ] Code comments for public APIs
- [ ] WebSocket protocol documentation
- [ ] CHANGELOG.md with version history
- [ ] Update existing template files

---

### **Week 3: Configuration & WebSocket** (5 days)

#### Day 11-13: Basic Configuration System (HP-2)
- [ ] Move ports to appsettings.json
- [ ] Move file paths to appsettings.json
- [ ] Add simple configuration loading in Program.cs
- [ ] Test with different configurations
- [ ] Document all configuration options

#### Day 14-15: Enhanced WebSocket (HP-3)
- [ ] Implement message echo functionality
- [ ] Add WebSocket rules to rules.json schema
- [ ] Support echo, static response, and pattern matching
- [ ] Add WebSocket examples to documentation
- [ ] Test bidirectional communication

---

### **Week 4: Testing & Polish** (5 days)

#### Day 16-17: Testing
- [ ] Add integration tests for middleware pipeline
- [ ] Add WebSocket integration tests
- [ ] Test all documented features
- [ ] Fix any bugs found during testing
- [ ] Verify existing unit tests still pass

#### Day 18-19: Final Polish
- [ ] Code cleanup and refactoring
- [ ] Review all documentation for accuracy
- [ ] Update CHANGELOG for v1.0
- [ ] Create release notes
- [ ] Tag v1.0-rc1 for testing

#### Day 20: Release
- [ ] Final testing of v1.0-rc1
- [ ] Address any critical issues
- [ ] Tag v1.0 release
- [ ] Update documentation with v1.0 specifics

---

## 4. Implementation Notes

### **Race-Safe Rules Hot-Reload**

**Approach**: Use ReaderWriterLockSlim for thread-safe access.

```csharp
public class Rules
{
    private readonly ReaderWriterLockSlim _lock = new();
    private List<Rule> _ruleList = new();
    private Dictionary<string, Rule> _ruleMap = new();
    
    public List<Rule> RuleList
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return new List<Rule>(_ruleList); // Return copy
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
    
    private void LoadRules()
    {
        _lock.EnterWriteLock();
        try
        {
            var json = File.ReadAllText(_path);
            var rules = JsonSerializer.Deserialize<List<Rule>>(json) ?? new();
            
            _ruleList = rules;
            _ruleMap = rules
                .Where(r => r.uri != null)
                .ToDictionary(r => GetKey(r.method, r.uri!), r => r);
            
            _logger.LogInformation($"Loaded {rules.Count} rules");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rules");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
```

### **Resource Leak Fixes**

```csharp
// In Vite.cs - Make HttpClient static
private static readonly HttpClient _httpClient = new();

public async Task<bool> WaitForViteAsync(TimeSpan timeout)
{
    // Use static _httpClient instead of new HttpClient()
    var response = await _httpClient.GetAsync(_viteUrl);
    // ...
}

// In Program.cs - Ensure disposal
var appLogWriter = new StreamWriter("app.log", append: true);
builder.Services.AddSingleton<TextWriter>(appLogWriter);

// Add shutdown hook
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() => appLogWriter.Dispose());
```

### **Exception Logging Pattern**

Replace all empty catch blocks:

```csharp
// Before
try
{
    // operation
}
catch { }

// After
try
{
    // operation
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to perform operation");
}
```

### **Basic Configuration System**

**appsettings.json**:
```json
{
  "Server": {
    "Port": 5000,
    "RulesFile": "rules.json",
    "ProxyConfigFile": "proxy.config.json"
  },
  "Vite": {
    "Port": 5173,
    "StartupTimeoutSeconds": 20
  },
  "Logging": {
    "MaxBufferLines": 2000,
    "LogFilePath": "app.log"
  }
}
```

**Simple loading**:
```csharp
var config = builder.Configuration.GetSection("Server");
int port = config.GetValue<int>("Port", 5000);
string rulesFile = config.GetValue<string>("RulesFile", "rules.json");
```

### **WebSocket Rules Schema**

**rules.json with WebSocket**:
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
    "path": "/ws",
    "type": "websocket",
    "behavior": "echo"
  },
  {
    "path": "/ws/sensor",
    "type": "websocket",
    "behavior": "static",
    "response": "{\"temp\":25.5,\"hum\":60}"
  }
]
```

**Implementation**:
- Echo: Send back received messages
- Static: Return predefined response for any message
- Pattern (optional): Match message pattern and return response

---

## Minimum Viable v1.0

To reach v1.0, the following are **absolutely required**:
1. ✅ Race condition fixed (CB-1)
2. ✅ Resource leaks fixed (CB-2)
3. ✅ Exception logging (CB-3)
4. ✅ Complete documentation (README, rules schema)
5. ✅ Basic configuration (appsettings.json for ports and paths)

**Highly recommended**:
- Enhanced WebSocket (echo and basic rules)
- Integration tests for middleware

---

## Success Metrics for v1.0

| Metric | Target | How to Measure |
|--------|--------|----------------|
| **Critical bugs** | 0 | All CB issues resolved |
| **Documentation** | Complete | README, rules schema, examples exist |
| **Configuration** | Basic | Ports and paths in appsettings.json |
| **WebSocket** | Enhanced | Echo and basic rules working |
| **Test coverage** | 50%+ | Existing tests + new integration tests |

---

## Conclusion

This simplified roadmap focuses on **critical fixes and essential features** for a stable v1.0 release in **4 weeks**:

1. **Week 1**: Fix critical bugs (race condition, resource leaks, exception logging)
2. **Week 2**: Write comprehensive documentation
3. **Week 3**: Add basic configuration and enhanced WebSocket
4. **Week 4**: Testing and release

**No scope creep**: Windows-only is acceptable, no security features needed, no complex testing infrastructure.

**Confidence Level**: **HIGH** - All items are achievable within 4 weeks with focused development effort.

---

*Document Version*: 2.0 (Simplified)  
*Created*: 2026-02-16  
*Updated*: 2026-02-16  
*Target*: v1.0 in 4 weeks
