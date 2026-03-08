using System.Text.Json;

namespace Esp32EmuConsole.Services;

/// <summary>
/// Loads, caches, and hot-reloads the mock-server rules defined in <c>rules.json</c>.
/// Rules are indexed by a <c>"METHOD /path"</c> key for O(1) HTTP look-ups.
/// A <see cref="FileSystemWatcher"/> automatically reloads the file when it changes on disk.
/// All public members are thread-safe; concurrent readers are handled via a
/// <see cref="ReaderWriterLockSlim"/>.
/// </summary>
public class Rules : IRules
{
    private readonly string _rulesPath;
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer? _reloadTimer;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private readonly ILogger<Rules> _logger;
    private readonly ReaderWriterLockSlim _lock = new();
    private List<Rule> _ruleList = new();
    private Dictionary<string, HttpResponse> _httpRuleMap = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<WebSocketResponse>> _wsRuleMap = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<WebSocketResponse>> _wsIntervalRuleMap = new(StringComparer.OrdinalIgnoreCase);
    public Rules(string workingDirectory, ILogger<Rules> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rulesPath = Path.Combine(workingDirectory, "rules.json");
        
        _lock.EnterWriteLock();
        try
        {
            LoadRulesInternal();
            _logger.LogInformation("Loaded {Count} rules from {RulesPath}", _ruleList.Count, _rulesPath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        if (File.Exists(_rulesPath))
        {
            _watcher = new FileSystemWatcher(workingDirectory, "rules.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };

            // Debounce rapid file system events; many editors save via temp+rename.
            _reloadTimer = new System.Timers.Timer(200) { AutoReset = false };
            _reloadTimer.Elapsed += (_, _) =>
            {
                try
                {
                    ReloadRules();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload rules from timer");
                }
            };

            FileSystemEventHandler onChange = (_, _) =>
            {
                try
                {
                    _reloadTimer?.Stop();
                    _reloadTimer?.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scheduling rules reload");
                }
            };

            _watcher.Changed += onChange;
            _watcher.Created += onChange;
            _watcher.Renamed += new RenamedEventHandler((s, e) => onChange(s, e));
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void LoadRulesInternal()
    {
        if (!File.Exists(_rulesPath))
        {
            _ruleList = new List<Rule>();
            _httpRuleMap = new Dictionary<string, HttpResponse>(StringComparer.OrdinalIgnoreCase);
            _wsRuleMap = new Dictionary<string, List<WebSocketResponse>>(StringComparer.OrdinalIgnoreCase);
            return;
        }
        // Read the file with retries because editors often lock the file while saving.
        string jsonText = string.Empty;
        const int maxAttempts = 6;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var fs = new FileStream(_rulesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                jsonText = sr.ReadToEnd();
                break;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error reading rules file");
                jsonText = string.Empty;
                break;
            }
        }

        if (string.IsNullOrEmpty(jsonText))
        {
            _logger.LogWarning("rules.json is empty or could not be read: {RulesPath}", _rulesPath);
        }
        List<Rule> rules;
        try
        {
            rules = JsonSerializer.Deserialize<List<Rule>>(jsonText, _jsonOptions) ?? new List<Rule>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing rules.json");
            rules = new List<Rule>();
        }

        var httpRuleMap = new Dictionary<string, HttpResponse>(StringComparer.OrdinalIgnoreCase);
        var wsRuleMap = new Dictionary<string, List<WebSocketResponse>>(StringComparer.OrdinalIgnoreCase);
        var wsIntervalRuleMap = new Dictionary<string, List<WebSocketResponse>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rules)
        {
            // Process HTTP rules - either has Response.Http or has no response at all
            if ((r.Response?.Ws == null && r.Response?.Http == null) || r.Response?.Http != null)
            {
                var path = r.Uri?.Trim();
                if (string.IsNullOrWhiteSpace(path)) continue;
                var method = string.IsNullOrWhiteSpace(r.Method) ? "GET" : r.Method.Trim().ToUpperInvariant();
                var key = MakeKey(method, path);
                
                // If response is null or empty, create default 501 response
                if (r.Response?.Http == null)
                {
                    httpRuleMap[key] = new HttpResponse 
                    { 
                        StatusCode = 501,
                        ContentType = "text/plain",
                        Body = "Not Implemented"
                    };
                }
                else
                {
                    httpRuleMap[key] = r.Response.Http;
                }
            }
            if (r.Response?.Ws != null)
            {
                var path = r.Uri?.Trim();
                if (string.IsNullOrWhiteSpace(path)) continue;

                var key = path.StartsWith("/") ? path : "/" + path;

                foreach (var wsResp in r.Response.Ws)
                {
                    if (string.IsNullOrWhiteSpace(wsResp.Behavior)) continue;
                    if (string.Equals(wsResp.Behavior, "interval", StringComparison.OrdinalIgnoreCase) && !wsResp.IntervalMs.HasValue)
                    {
                        _logger.LogWarning("WebSocket rule for path {Path} has 'interval' behavior but no IntervalMs defined. Skipping.", key);
                        continue;
                    }
                    if (string.Equals(wsResp.Behavior, "interval", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(wsResp.Match)) _logger.LogWarning("WebSocket rule for path {Path} has 'interval' behavior but also defines a Match pattern. Match will be ignored.", key);
                        if (!wsIntervalRuleMap.ContainsKey(key))
                        {
                            wsIntervalRuleMap[key] = new List<WebSocketResponse>();
                        }
                        wsIntervalRuleMap[key].Add(wsResp);
                        continue;
                    }
                    if (!wsRuleMap.ContainsKey(key))
                    {
                        wsRuleMap[key] = new List<WebSocketResponse>();
                    }
                    wsRuleMap[key].Add(wsResp);
                }
            }
        }

        _ruleList = rules;
        _httpRuleMap = httpRuleMap;
        _wsRuleMap = wsRuleMap;
        _wsIntervalRuleMap = wsIntervalRuleMap;
    }

    public IReadOnlyList<Rule> GetRules()
    {
        _lock.EnterReadLock();
        try
        {
            return new List<Rule>(_ruleList);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool TryGetHttpResponse(string method, string path, out HttpResponse? response)
    {
        _lock.EnterReadLock();
        try
        {
            var key = MakeKey(method, path);
            return _httpRuleMap.TryGetValue(key, out response);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void ReloadRules()
    {
        _lock.EnterWriteLock();
        try
        {
            LoadRulesInternal();
            _logger.LogInformation("Reloaded {Count} rules from {RulesPath}", _ruleList.Count, _rulesPath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TryGetWebSocketResponse(string path, string incomingMessage, out List<WebSocketResponse>? responses)
    {
        _lock.EnterReadLock();
        try
        {
            if (_wsRuleMap.TryGetValue(path, out var wsResponses))
            {
                responses = new();
                foreach (var wsResp in wsResponses)
                {
                    if (wsResp.Match == null || System.Text.RegularExpressions.Regex.IsMatch(incomingMessage, wsResp.Match))
                    {
                        responses.Add(wsResp);
                    }
                }

                if (responses != null && responses.Count > 0)  
                {  
                    return true;  
                } 
            }

            responses = null;
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool TryGetWebSocketIntervalResponse(string path, out List<WebSocketResponse>? responses)
    {
        _lock.EnterReadLock();
        try
        {
            if (_wsIntervalRuleMap.TryGetValue(path, out var wsResponses))
            {
                responses = new List<WebSocketResponse>(wsResponses);
                return true;
            }
            responses = null;
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static string MakeKey(string method, string path) => $"{method.Trim().ToUpperInvariant()} {(path.Trim().StartsWith("/") ? path.Trim() : "/" + path.Trim())}";

    public void Dispose()
    {
        _watcher?.Dispose();
        _reloadTimer?.Dispose();
        _lock.Dispose();
    }
}
